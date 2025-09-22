using Serilog;
using SharpAdbClient;
using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace ScrcpyNet
{
    public delegate void ScrcpyConnectedChanged(bool conn);
    public class ScrcpyException : Exception
    {
        public ScrcpyException(string message) : base(message) { }
    }
    public class Scrcpy : IDisposable
    {
        public event ScrcpyConnectedChanged OnSrcpyConnected;

        private bool conn;
        public string DeviceName { get; private set; } = "";
        public int Width { get; internal set; }
        public int Height { get; internal set; }
        public long Bitrate { get; set; } = 2000000;
        public string ScrcpyServerFile { get; set; } = "libs\\scrcpy-server";
        public  string version = "2.7";

        public string Display_id { get; set; } = "display_id=0";
        public bool Connected
        {
            set
            {
                conn = value;
                OnSrcpyConnected?.Invoke(value);
            }
            get
            {
                return conn;
            }
        }
        public VideoStreamDecoder VideoStreamDecoder { get; }

        private Thread videoThread;
        private Thread controlThread;
        private TcpClient videoClient;
        private TcpClient controlClient;
        private TcpListener listener; 
        private TcpListener listener_ctr;
        private CancellationTokenSource cts;

        private readonly AdbClient adb;
        private readonly DeviceData device;
        private BlockingCollection<IControlMessage> controlChannel;
        private static readonly ArrayPool<byte> pool = ArrayPool<byte>.Shared;
        private static readonly ILogger log = Log.ForContext<VideoStreamDecoder>();

        private int port;
        private static Random random = new Random();
        public Scrcpy(DeviceData device, string displayId, VideoStreamDecoder videoStreamDecoder = null)
        {
            adb = new AdbClient();
            this.device = device;
            VideoStreamDecoder = videoStreamDecoder ?? new VideoStreamDecoder();
            VideoStreamDecoder.Scrcpy = this;
            Display_id = "display_id=" + displayId;

        }

        //public void SetDecoder(VideoStreamDecoder videoStreamDecoder)
        //{
        //    this.videoStreamDecoder = videoStreamDecoder;
        //    this.videoStreamDecoder.Scrcpy = this;
        //}

        /// <summary>
        /// 启动
        /// </summary>
        /// <param name="timeoutMs">连接超时</param>
        /// <exception cref="Exception"></exception>
        public void Start(long timeoutMs = 5000)
        {
            if (Connected)
                throw new Exception("Already connected.");
            controlChannel = new BlockingCollection<IControlMessage>();

            port = random.Next(30000, 40000);
            MobileServerSetup();

            listener = new TcpListener(IPAddress.Loopback, port);
            listener.Start();

            MobileServerStart();

            int waitTimeMs = 0;
            while (!listener.Pending())
            {
                Thread.Sleep(10);
                waitTimeMs += 10;

                if (waitTimeMs > timeoutMs)
                    throw new ScrcpyException("Timeout while waiting for server to connect.");
            }

            videoClient = listener.AcceptTcpClient();
            log.Debug("Video socket connected.");

            if (!listener.Pending())
                throw new ScrcpyException("Server is not sending a second connection request. Is 'control' disabled?");

            controlClient = listener.AcceptTcpClient();
            log.Debug("Control socket connected.");

            ReadDeviceInfo();

            cts = new CancellationTokenSource();

            videoThread = new Thread(VideoMain) { Name = "ScrcpyNet Video" };
            controlThread = new Thread(ControllerMain) { Name = "ScrcpyNet Controller" };

            videoThread.Start();
            controlThread.Start();

            Connected = true;

            MobileServerCleanup();
        }

        public void Stop()
        {
            //if (!Connected)
            //    throw new Exception("Not connected.");
            try
            {
                   controlChannel?.CompleteAdding();

                Connected = false;
                cts?.Cancel();

                videoThread?.Join();
                controlThread?.Join();
                listener?.Stop();
                listener_ctr?.Stop();
                controlClient?.Dispose();
                Connected = false;
            }
            catch (Exception ex)
            {
                log.Debug("scrcpy stop error: " + ex.ToString());       
            }
         
        }

        public void SendControlCommand(IControlMessage msg)
        {
            if (controlClient == null)
                log.Warning("SendControlCommand() called, but controlClient is null.");
            else
                // controlChannel.Writer.TryWrite(msg);
                if (controlChannel != null && !controlChannel.IsAddingCompleted)
                controlChannel.Add(msg);
        }

        private void ReadDeviceInfo()
        {
            if (videoClient == null) throw new ScrcpyException("Can't read device info when videoClient is null.");

            var infoStream = videoClient.GetStream();
            infoStream.ReadTimeout = 2000;
            var deviceInfoBuf = pool.Rent(128);
            int total = 0;
            while (total < 68)
            {
                int read;
                try
                {
                    read = infoStream.Read(deviceInfoBuf, 0, 128);
                    Console.WriteLine(read);
                }
                catch (IOException ex)
                {
                    if (ex.InnerException is SocketException se && se.SocketErrorCode == SocketError.TimedOut)
                    {
                        // allow loop to retry until timeoutMs handled outside (we are during startup, so keep trying briefly)
                        continue;
                    }
                    pool.Return(deviceInfoBuf);
                    throw;
                }
                if (read == 0)
                {
                    pool.Return(deviceInfoBuf);
                    throw new ScrcpyException($"Unexpected end of stream while reading device info. Got {total} bytes.");
                }
                total += read;
                if (read < (68 - total))
                {
                    log.Debug("Partial device header read: {Read} (total {Total}/68)", read, total);
                }
            }
            //if (total != 68)
            //{
            //    pool.Return(deviceInfoBuf);
            //    throw new ScrcpyException($"Expected to read exactly 68 bytes, but got {total} bytes.");
            //}

            var deviceInfoSpan = new ReadOnlySpan<byte>(deviceInfoBuf, 0, 68);
            DeviceName = Encoding.UTF8.GetString(deviceInfoSpan.Slice(0, 64).ToArray()).TrimEnd('\0');
            Width = BinaryPrimitives.ReadInt16BigEndian(deviceInfoSpan.Slice(64));
            Height = BinaryPrimitives.ReadInt16BigEndian(deviceInfoSpan.Slice(66));
         
            log.Debug("Device name: {DeviceName} size: {Width}x{Height}", DeviceName, Width, Height);
            Console.WriteLine(BitConverter.ToString(deviceInfoBuf));
            Console.WriteLine(Encoding.UTF8.GetString(deviceInfoSpan.ToArray()));
            pool.Return(deviceInfoBuf);
        }

        private void VideoMain()
        {
            try
            {


                // Both of these should never happen.
                if (videoClient == null) throw new Exception("videoClient is null.");
                if (cts == null) throw new Exception("cts is null.");

                var videoStream = videoClient.GetStream();
                videoStream.ReadTimeout = 2000;

                int bytesRead;
                var metaBuf = pool.Rent(12);

                Stopwatch sw = new Stopwatch();

                while (!cts.Token.IsCancellationRequested)
                {
                    // Read metadata (each packet starts with some metadata)
                    try
                    {
                        bytesRead = videoStream.Read(metaBuf, 0, 12);
                    }
                    catch (IOException ex)
                    {
                        // Ignore timeout errors.
                        if (ex.InnerException is SocketException x && x.SocketErrorCode == SocketError.TimedOut)
                            continue;
                        // throw ex;
                        continue;
                    }
                    catch (Exception ex)
                    {
                        if (ex.InnerException is SocketException x && x.SocketErrorCode == SocketError.TimedOut)
                            continue;
                        continue;
                    }

                    if (bytesRead != 12)
                    {
                        
                        //拔出数据线报错，调用结束
                        Task.Run(() =>
                        {
                            Stop();
                        });
                        break;
                        //throw new Exception($"Expected to read exactly 12 bytes, but got {bytesRead} bytes.");
                    }

                    sw.Restart();

                    // Decode metadata
                    var metaSpan = metaBuf.AsSpan();
                    var presentationTimeUs = BinaryPrimitives.ReadInt64BigEndian(metaSpan);
                    var packetSize = BinaryPrimitives.ReadInt32BigEndian(metaSpan.Slice(8));

                    // Read the whole frame, this might require more than one .Read() call.
                    var packetBuf = pool.Rent(packetSize);
                    var pos = 0;
                    var bytesToRead = packetSize;

                    while (bytesToRead != 0 && !cts.Token.IsCancellationRequested)
                    {
                        bytesRead = videoStream.Read(packetBuf, pos, bytesToRead);

                        if (bytesRead == 0)
                        {

                            // throw new Exception("Unable to read any bytes.");
                            log.Debug("Exception Unable to read any bytes.");
                            Task.Run(() =>
                            {
                                Stop();
                            });
                            break;
                        }


                        pos += bytesRead;
                        bytesToRead -= bytesRead;
                    }

                    if (!cts.Token.IsCancellationRequested)
                    {
                        try
                        {
                            //Log.Verbose($"Presentation Time: {presentationTimeUs}us, PacketSize: {packetSize} bytes");
                            VideoStreamDecoder?.Decode(packetBuf, presentationTimeUs);
                            //log.Debug("Received and decoded a packet in {@ElapsedMilliseconds} ms", sw.ElapsedMilliseconds);
                        }
                        catch (Exception ex)
                        {
                            log.Debug("VideoStreamDecoder: " + ex.ToString());
                            Task.Run(() =>
                            {
                                Stop();
                            });
                        }
                    }

                    sw.Stop();

                    pool.Return(packetBuf);
                }
            }
            catch (Exception ex)
            {
                log.Debug($"VideoMain:   {ex}");
              
            }
        }

        private void ControllerMain()
        {
            // Both of these should never happen.
            //if (controlClient == null) throw new ScrcpyException("controlClient is null.");
            //if (cts == null) throw new ScrcpyException("cts is null.");
            if (controlClient == null) return;
            if (cts == null) return;

            try
            {
                var stream = controlClient.GetStream();
                while (cts != null && !cts.IsCancellationRequested)
                {
                  
                    foreach (var cmd in controlChannel.GetConsumingEnumerable(cts.Token))
                    {
                        if (cts.IsCancellationRequested)
                        {
                            break; // 取消处理
                        }
                        try
                        {
                            ControllerSend(stream, cmd); // 处理命令
                          //  ControlMessageExtensions.WriteToStream(cmd, stream);
                        }
                        catch (Exception ex)
                        {
                            log.Debug(ex.ToString());
                        }

                    }
                }


            }
            catch (OperationCanceledException ex)
            {
                log.Debug("OperationCanceledException: " + ex.ToString());
            }
            catch (Exception ex)
            {
                log.Debug("OperationCanceledException: " + ex.ToString());


            }
        }

        // This needs to be in a separate method, because we can't use a Span<byte> inside an async function.
        private void ControllerSend(NetworkStream stream, IControlMessage cmd)
        {
            log.Debug("Sending control message: {@ControlMessage}", cmd.Type);
            var bytes = cmd.ToBytes();
            Console.WriteLine(Encoding.ASCII.GetString(bytes.ToArray() )) ;
            stream.Write(bytes.ToArray(), 0, bytes.Length);
        }

        private void MobileServerSetup()
        {
            MobileServerCleanup();

            // Push scrcpy-server.jar
            UploadMobileServer();

            // Create port reverse rule
       
           adb.CreateReverseForward(device, $"localabstract:scrcpy", $"tcp:{port}", true);
        
        }

        /// <summary>
        /// Remove ADB forwards/reverses.
        /// </summary>
        private void MobileServerCleanup()
        {
            // Remove any existing network stuff.
            //adb.RemoveAllForwards(device);
            //adb.RemoveAllReverseForwards(device);
        }

        /// <summary>
        /// Start the scrcpy server on the android device.
        /// </summary>
        /// <param name="bitrate"></param>
        private void MobileServerStart()
        {
            log.Debug("Starting scrcpy server...");

            var cts = new CancellationTokenSource();
            var receiver = new SerilogOutputReceiver();

            int maxFramerate = 30;
            ScrcpyLockVideoOrientation orientation = ScrcpyLockVideoOrientation.Unlocked; // -1 means allow rotate
            bool control = true;
            bool showTouches = false;
            bool stayAwake = false;

            var cmds = new List<string>
            {
                "CLASSPATH=/data/local/tmp/scrcpy-server.jar",
                "app_process",

                // Unused
                "/",
                // App entry point, or something like that.
                "com.genymobile.scrcpy.Server",

                version,
                "log_level=debug",
                $"video-bit-rate={Bitrate}"
            };

            if (maxFramerate != 0)
                cmds.Add($"max-fps={maxFramerate}");

            if (orientation != ScrcpyLockVideoOrientation.Unlocked)
                cmds.Add($"lock_video_orientation={(int)orientation}");

            cmds.Add("tunnel_forward=false");
            //cmds.Add("crop=-");
            cmds.Add($"control={control}");
            cmds.Add("audio=false");
            //cmds.Add($"control_socket_name=scrcpy_control");
            //cmds.Add("display_id=0");
            cmds.Add(Display_id);
            cmds.Add($"show_touches={showTouches}");
            cmds.Add($"stay_awake={stayAwake}");
            cmds.Add("power_off_on_close=false");
            cmds.Add("downsize_on_error=true");
            cmds.Add("cleanup=true");

            // cmds.Add("--no-audio");
            string command = string.Join(" ", cmds);

            log.Debug("Start command: " + command);
            _ = adb.ExecuteRemoteCommandAsync(command, device, receiver, cts.Token);
        }

        private void UploadMobileServer()
        {
            using (SyncService service = new SyncService(new AdbSocket(new IPEndPoint(IPAddress.Loopback, AdbClient.AdbServerPort)), device))
            {
                using (Stream stream = File.OpenRead(ScrcpyServerFile))
                {

                    service.Push(stream, "/data/local/tmp/scrcpy-server.jar", 444, DateTime.Now, null, CancellationToken.None);
               
                }
            }

        }

        public void Dispose()
        {
            if (conn)
            {
                Stop();
            }
            VideoStreamDecoder?.Dispose();
        }
    }
}
