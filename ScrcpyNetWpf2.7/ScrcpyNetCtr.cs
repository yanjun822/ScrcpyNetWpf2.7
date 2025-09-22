
using FFmpeg.AutoGen;
using ScrcpyNet;
using SharpAdbClient;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ScrcpyNetWpf2._7
{
  
    public class ScrcpyNetCtr :IDisposable, System.ComponentModel.INotifyPropertyChanged
    {
        public ScrcpyConnectedChanged ScrcpyConnectedChanged;
        private bool isConnected = false;
        public bool IsConnected
        {
            set
            {
                isConnected = value;
                ScrcpyConnectedChanged?.Invoke(isConnected);
            }
            get
            {
                return isConnected;
            }
        }
   
        private Scrcpy scrcpy;
        public Scrcpy  Scrcpy
        {
            private set
            {
                scrcpy = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Scrcpy"));
            }
            get
            {
                return scrcpy;
            }
        }
        public double BitrateKb { get; set; } = 1;

        public event PropertyChangedEventHandler PropertyChanged;

   
        public long Bitrate { get; set; } = 2000000;
        public void Connect(string serial, string disId="0")
        {
            if (IsConnected) return;
            
            AdbClient adbClient = new AdbClient();
            var list = adbClient.GetDevices();
            var device = list.Where(i=>i.Serial== serial).FirstOrDefault();
            if (device == null)
            {
                throw new Exception("设备不存在");
            }
            if (Scrcpy == null) 
            {
               
                Scrcpy = new Scrcpy(device,disId);
              
                Scrcpy.OnSrcpyConnected += Scrcpy_OnSrcpyConnected;
          
            }
            scrcpy.Bitrate = Bitrate;
            Scrcpy.Start();
            IsConnected =true;
            //Thread.Sleep(500);
        }

        private void Scrcpy_OnSrcpyConnected(bool conn)
        {
            IsConnected = conn;
        }

        public void Disconnect()
        {
            if (Scrcpy != null)
            {
                Scrcpy.Stop();
                IsConnected = false;
             
                Thread.Sleep(500);
            }
        }

        public void Dispose()
        {
            if (IsConnected)
            {
                
                Scrcpy.Dispose();
                Scrcpy = null;
            }
           
        }
    }
}
