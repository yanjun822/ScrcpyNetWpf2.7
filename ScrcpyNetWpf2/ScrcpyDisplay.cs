using ScrcpyNet;
using Serilog;
using System;
using System.Diagnostics;
using System.Drawing.Imaging;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace ScrcpyNetWpf2
{
    /// <summary>
    /// Follow steps 1a or 1b and then 2 to use this custom control in a XAML file.
    ///
    /// Step 1a) Using this custom control in a XAML file that exists in the current project.
    /// Add this XmlNamespace attribute to the root element of the markup file where it is 
    /// to be used:
    ///
    ///     xmlns:MyNamespace="clr-namespace:ScrcpyNet.Wpf"
    ///
    ///
    /// Step 1b) Using this custom control in a XAML file that exists in a different project.
    /// Add this XmlNamespace attribute to the root element of the markup file where it is 
    /// to be used:
    ///
    ///     xmlns:MyNamespace="clr-namespace:ScrcpyNet.Wpf;assembly=ScrcpyNet.Wpf"
    ///
    /// You will also need to add a project reference from the project where the XAML file lives
    /// to this project and Rebuild to avoid compilation errors:
    ///
    ///     Right click on the target project in the Solution Explorer and
    ///     "Add Reference"->"Projects"->[Select this project]
    ///
    ///
    /// Step 2)
    /// Go ahead and use your control in the XAML file.
    ///
    ///     <MyNamespace:ScrcpyDisplay/>
    ///
    /// </summary>
    public delegate void ScrcpyDisplayMouseDown(double x, double y);
    public delegate void ScrcpyDisplayMouseUp(double x, double y);
    public delegate void ScrcpyDisplayMouseMove(double x, double y, MouseButtonState left);
    public delegate void ScrcpyDisplayMouseLeave();
    public delegate void ScrcpyDisplayMouseEnter();
    public delegate void ScrcpySize(double w, double h);

    public class ScrcpyDisplay : Control
    {
        public ScrcpyDisplay()
        {
            Focusable = true; // 允许获得焦点
            FocusVisualStyle = null; // 可选：去除默认焦点虚线
        }
        public event ScrcpyDisplayMouseDown OnScrcpyDisplayMouseDown;
        public event ScrcpyDisplayMouseUp OnScrcpyDisplayMouseUp;
        public event ScrcpyDisplayMouseLeave OnScrcpyDisplayMouseLeave;
        public event ScrcpyDisplayMouseEnter OnScrcpyDisplayMouseEnter;
        public event ScrcpySize OnScrcpySize;
        public double ScrcpyWidth { set; get; }
        public double ScrcpyHeight { set; get; }

        private void UpdateSize(double w, double h)
        {
            if (ScrcpyWidth != w || ScrcpyHeight != h)
            {
                ScrcpyWidth = w;
                ScrcpyHeight = h;
                OnScrcpySize?.Invoke(w, h);
            }
        }


        //  public event ScrcpyDisplayMouseMove OnScrcpyDisplayMouseMove;

        private static readonly ILogger log = Log.ForContext<ScrcpyDisplay>();

        public static readonly DependencyProperty ScrcpyProperty = DependencyProperty.Register(
            nameof(Scrcpy),
            typeof(Scrcpy),
            typeof(ScrcpyDisplay),
            new PropertyMetadata(OnScrcpyChanged));

        private Image renderTarget;
        private WriteableBitmap bmp;

        static ScrcpyDisplay()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(ScrcpyDisplay), new FrameworkPropertyMetadata(typeof(ScrcpyDisplay)));
        }

        public Scrcpy Scrcpy
        {
            get => (Scrcpy)GetValue(ScrcpyProperty);
            set => SetValue(ScrcpyProperty, value);
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            if (GetTemplateChild("PART_RenderTargetImage") is Image img)
                renderTarget = img;
        }
        protected override void OnMouseLeave(MouseEventArgs e)
        {
            OnScrcpyDisplayMouseLeave?.Invoke();
            base.OnMouseLeave(e);
        }
        protected override void OnMouseEnter(MouseEventArgs e)
        {
            OnScrcpyDisplayMouseEnter?.Invoke();
            base.OnMouseEnter(e);
        }
        protected override void OnMouseDown(MouseButtonEventArgs e)
        {
            // For some reason WPF doesn't focus the control on click??
            //Focus();

            //if (Scrcpy != null)
            //{
            //    if (e.RightButton == MouseButtonState.Pressed)
            //    {
            //        e.Handled = true;
            //        Scrcpy.SendControlCommand(new BackOrScreenOnControlMessage() { Action = AndroidKeyEventAction.AKEY_EVENT_ACTION_DOWN });
            //        Scrcpy.SendControlCommand(new BackOrScreenOnControlMessage() { Action = AndroidKeyEventAction.AKEY_EVENT_ACTION_UP });
            //    }
            //    else if (e.LeftButton == MouseButtonState.Pressed)
            //    {
            //        if (OnScrcpyDisplayMouseDown != null)
            //        {
            //            var pos = GetScrcpyMousePosition(e);
            //            OnScrcpyDisplayMouseDown?.Invoke(pos.Point.X, pos.Point.Y);
            //        }

            //        e.Handled = true;

            //        SendTouchCommand(AndroidMotionEventAction.AMOTION_EVENT_ACTION_DOWN, e);
            //    }
            //}

            //base.OnMouseDown(e);
        }
        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
           
            
            if (Scrcpy != null)
            {
                try
                {
                    Focus();
                    if (e.RightButton == MouseButtonState.Pressed)
                    {
                        e.Handled = true;
                        Scrcpy.SendControlCommand(new BackOrScreenOnControlMessage() { Action = AndroidKeyEventAction.AKEY_EVENT_ACTION_DOWN });
                        Scrcpy.SendControlCommand(new BackOrScreenOnControlMessage() { Action = AndroidKeyEventAction.AKEY_EVENT_ACTION_UP });
                    }
                    else if (e.LeftButton == MouseButtonState.Pressed)
                    {
                        if (OnScrcpyDisplayMouseDown != null)
                        {
                            var pos = GetScrcpyMousePosition(e);
                            OnScrcpyDisplayMouseDown?.Invoke(pos.Point.X, pos.Point.Y);
                        }

                        e.Handled = true;

                        SendTouchCommand(AndroidMotionEventAction.AMOTION_EVENT_ACTION_DOWN, e);
                    }
                }
                catch (Exception)
                {

                    throw;
                }
              
            }

            //base.OnMouseDown(e);
            base.OnMouseLeftButtonDown(e);
        }
        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            if (Scrcpy != null)
            {

                if (OnScrcpyDisplayMouseUp != null)
                {
                    var pos = GetScrcpyMousePosition(e);
                    OnScrcpyDisplayMouseUp?.Invoke(pos.Point.X, pos.Point.Y);
                }
                e.Handled = true;

                SendTouchCommand(AndroidMotionEventAction.AMOTION_EVENT_ACTION_UP, e);

            }

            base.OnMouseLeftButtonUp(e);
        }
        protected override void OnMouseUp(MouseButtonEventArgs e)
        {
            //if (Scrcpy != null)
            //{
               
            //    if (OnScrcpyDisplayMouseUp != null)
            //    {
            //        var pos = GetScrcpyMousePosition(e);
            //        OnScrcpyDisplayMouseUp?.Invoke(pos.Point.X, pos.Point.Y);
            //    }
            //    e.Handled = true;

            //    SendTouchCommand(AndroidMotionEventAction.AMOTION_EVENT_ACTION_UP, e);

            //}

            //base.OnMouseUp(e);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (Scrcpy != null && renderTarget != null)
            {
                var point = e.GetPosition(renderTarget);
                if (e.LeftButton == MouseButtonState.Pressed && point.X >= 0 && point.Y >= 0)
                {
                    // Do we need to set e.Handled?
                    SendTouchCommand(AndroidMotionEventAction.AMOTION_EVENT_ACTION_MOVE, e);
                }
            }

            base.OnMouseMove(e);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (Scrcpy != null)
            {
                e.Handled = true;

                var msg = new KeycodeControlMessage();
                msg.KeyCode = KeycodeHelper.ConvertKey(e.Key);
                msg.Metastate = KeycodeHelper.ConvertModifiers(e.KeyboardDevice.Modifiers);
                Scrcpy.SendControlCommand(msg);
            }

            base.OnKeyDown(e);
        }

        protected override void OnKeyUp(KeyEventArgs e)
        {
            if (Scrcpy != null)
            {
                e.Handled = true;

                var msg = new KeycodeControlMessage();
                msg.Action = AndroidKeyEventAction.AKEY_EVENT_ACTION_UP;
                msg.KeyCode = KeycodeHelper.ConvertKey(e.Key);
                msg.Metastate = KeycodeHelper.ConvertModifiers(e.KeyboardDevice.Modifiers);
                Scrcpy.SendControlCommand(msg);
            }

            base.OnKeyUp(e);
        }

        protected override void OnMouseWheel(MouseWheelEventArgs e)
        {

            var pos = GetScrcpyMousePosition(e);
            if (Scrcpy != null && pos != null)
            {
                e.Handled = true;

                var msg = new ScrollEventControlMessage();
                msg.Position = pos;
                msg.VerticalScroll = Math.Max(Math.Min(e.Delta / 120f, 1f), -1f); // Random guess
                msg.HorizontalScroll = 0; // TODO: Can we implement this?
                Scrcpy.SendControlCommand(msg);
            }

            base.OnMouseWheel(e);
        }

        protected void SendTouchCommand(AndroidMotionEventAction action, MouseEventArgs e)
        {
            var pos = GetScrcpyMousePosition(e);
            if (Scrcpy != null && pos != null)
            {
                var msg = new TouchEventControlMessage();
                msg.Action = action;
                msg.Position = pos;


                Scrcpy.SendControlCommand(msg);


                log.Debug("Sending {Action} for position {PositionX}, {PositionY}", action, msg.Position.Point.X, msg.Position.Point.Y);
            }
        }

        private Position GetScrcpyMousePosition(MouseEventArgs e)
        {
            if (Scrcpy == null || renderTarget == null) return null;

            var point = e.GetPosition(renderTarget);

            var pos = new Position();
            pos.Point = new ScrcpyNet.Point { X = (int)point.X, Y = (int)point.Y };
            pos.ScreenSize.Width = (ushort)renderTarget.ActualWidth;
            pos.ScreenSize.Height = (ushort)renderTarget.ActualHeight;
            TouchHelper.ScaleToScreenSize(pos, Scrcpy.Width, Scrcpy.Height);

            return pos;
        }
        private readonly object bmpLock = new object();

        private unsafe void OnFrame(object sender, FrameData frameData)
        {
            if (renderTarget != null)
            {
                // This probably isn't the best way to do this.
                try
                {
                    // The timeout is required. Otherwise this will block forever when the application is about to exit but the videoThread sends a last frame.
                    // The DispatcherPriority has been randomly selected, so it might not be the optimal value.
                    UpdateSize(frameData.Width, frameData.Height);

                    Dispatcher.Invoke(() =>
                    {
                        lock (bmpLock)
                        {


                            if (bmp == null || bmp.Width != frameData.Width || bmp.Height != frameData.Height)
                            {
                                bmp = new WriteableBitmap(frameData.Width, frameData.Height, 96, 96, System.Windows.Media.PixelFormats.Bgra32, null);
                                renderTarget.Source = bmp;
                            }
                            if (!isGetImageSource)
                            {
                                try
                                {
                                    bmp.Lock();
                                    var dest = new Span<byte>(bmp.BackBuffer.ToPointer(), frameData.Data.Length);
                                    frameData.Data.CopyTo(dest);
                                    bmp.AddDirtyRect(new Int32Rect(0, 0, frameData.Width, frameData.Height));
                                }
                                catch (Exception ex)
                                {

                                    Console.WriteLine("OnFrame: " + ex.Message);
                                }
                                finally
                                {
                                    bmp.Unlock();
                                }
                            }
                        }
                    }, DispatcherPriority.Send, default, TimeSpan.FromMilliseconds(2000));
                }
                catch (TimeoutException)
                {
                    //log.Debug("Ignoring TimeoutException inside OnFrame.");
                }
                catch (TaskCanceledException)
                {
                    log.Debug("Ignoring TaskCanceledException inside OnFrame.");
                }
                catch (Exception ex)
                {
                    log.Debug(ex.Message);
                }
            }
        }
        private bool isGetImageSource = false;
        private System.Drawing.Bitmap getImageSourceBitmap = null;
        private bool getImageSourceToFileNameAction = false;

        public System.Drawing.Bitmap GetImageSource(int time = 1000)
        {
            if (Scrcpy == null || bmp == null)
                throw new Exception($"设备({Name})未打开");
            try
            {
                isGetImageSource = true;
                return ConvertWriteableBitmapToBitmap(bmp);
            }
            finally
            {
                isGetImageSource = false;
            }

            //try
            //{
            //    getImageSourceBitmap = null;
            //    getImageSourceToFileNameAction = true;
            //    var task = Task.Run(() =>
            //    {
            //        Stopwatch stopwatch = new Stopwatch();
            //        stopwatch.Start();
            //        while (getImageSourceToFileNameAction)
            //        {
            //            if (stopwatch.ElapsedMilliseconds > time)
            //            {
            //                break;
            //            }
            //            else
            //            {
            //                Task.Delay(10);
            //            }
            //        }
            //    });
            //    task.Wait();
            //    if (getImageSourceBitmap == null)
            //    {
            //        throw new TimeoutException("获取超时");
            //    }
            //    return getImageSourceBitmap;
            //}
            //finally
            //{
            //    getImageSourceBitmap = null;
            //    getImageSourceToFileNameAction = false;
            //}


        }
        // 将 WriteableBitmap 转换为 Bitmap
        private System.Drawing.Bitmap ConvertWriteableBitmapToBitmap(WriteableBitmap writeableBitmap)
        {
            int width = writeableBitmap.PixelWidth;
            int height = writeableBitmap.PixelHeight;


            // 获取 WriteableBitmap 的像素数据
            byte[] pixels = new byte[width * height * 4]; // BGRA32 格式，每个像素 4 字节
            writeableBitmap.CopyPixels(pixels, width * 4, 0);  // 复制像素数据到字节数组

            // 创建一个 Bitmap 对象
            System.Drawing.Bitmap bitmap = new System.Drawing.Bitmap(width, height, PixelFormat.Format32bppArgb);

            // 使用 LockBits 锁定位图数据
            BitmapData bitmapData = bitmap.LockBits(
                new System.Drawing.Rectangle(0, 0, width, height),
                ImageLockMode.WriteOnly,
                PixelFormat.Format32bppArgb
            );

            // 将像素数据直接复制到 Bitmap 的内存中
            System.Runtime.InteropServices.Marshal.Copy(pixels, 0, bitmapData.Scan0, pixels.Length);

            // 解锁位图数据
            bitmap.UnlockBits(bitmapData);


            /*
            // 将字节数组的数据写入 Bitmap
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int index = (y * width + x) * 4;
                    byte b = pixels[index];     // 蓝色
                    byte g = pixels[index + 1]; // 绿色
                    byte r = pixels[index + 2]; // 红色
                    byte a = pixels[index + 3]; // 透明度

                    // 设置 Bitmap 上每个像素的颜色
                    System.Drawing.Color color = System.Drawing.Color.FromArgb(a, r, g, b);
                    bitmap.SetPixel(x, y, color);
                }
            }
            */
            return bitmap;
        }
        private static void OnScrcpyChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            if (sender is ScrcpyDisplay display)
            {
                // Unsubscribe on the old scrcpy
                if (e.OldValue is Scrcpy old && old != null)
                    old.VideoStreamDecoder.OnFrame -= display.OnFrame;

                // Subscribe on the new scrcpy
                if (e.NewValue is Scrcpy value && value != null)
                {
                    value.VideoStreamDecoder.OnFrame += display.OnFrame;
                }
                else
                {
                    display.Dispatcher.Invoke(() =>
                    {

                        if (display.bmp != null)
                        {
                            try
                            {
                                display.bmp.Lock();
                                FillWhite(display.bmp);
                            }
                            finally
                            {
                                display.bmp.Unlock();
                            }
                        }

                    }, DispatcherPriority.Send, default, TimeSpan.FromMilliseconds(200));
                }
            }
        }
        public void SetWhite()
        {
            this.Dispatcher.Invoke(() =>
            {

                if (this.bmp != null)
                {
                    try
                    {
                        this.bmp.Lock();
                        FillWhite(this.bmp);
                    }
                    finally
                    {
                        this.bmp.Unlock();
                    }
                }

            }, DispatcherPriority.Send, default, TimeSpan.FromMilliseconds(200));
        }
        private static void FillWhite(WriteableBitmap writeableBitmap)
        {
            // 获取图像的像素数据
            int width = writeableBitmap.PixelWidth;
            int height = writeableBitmap.PixelHeight;

            // 创建一个 BGRA 白色的颜色值
            // int white = 0xFFFFFFFF;  // 白色的 BGRA 表示方式（Alpha = 255, Red = 255, Green = 255, Blue = 255）

            // 使用 WriteableBitmap 的 WritePixels 方法设置所有像素为白色
            writeableBitmap.WritePixels(
                new Int32Rect(0, 0, width, height),
                new int[width * height],  // 用白色填充所有像素
                width * 4,                // 每个像素占 4 个字节（BGRA32）
                0);                       // 每行的偏移量为 0
        }
    }
}
