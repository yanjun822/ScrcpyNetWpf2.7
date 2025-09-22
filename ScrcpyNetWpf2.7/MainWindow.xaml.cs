
using FFmpeg.AutoGen;
using Serilog;
using SharpAdbClient;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;


namespace ScrcpyNetWpf2._7
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            Log.Logger = new LoggerConfiguration()
           .MinimumLevel.Debug()
           .WriteTo.Sink(new LogServiceSink())
           .CreateLogger();


            AdbServer server = new AdbServer();
            StartServerResult result = 
                server.StartServer(@"libs\adb.exe", restartServerIfNewer: true);
            if (result != StartServerResult.Started)
            {
                Console.WriteLine("无法启动 ADB 服务");

            }
            ffmpeg.RootPath = "libs";
            //    System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "libs");



            ScrcpyNetCtr = new ScrcpyNetCtr();
 

            this.DataContext = this;
            this.Closed += MainWindow_Closed;
        }

        private void MainWindow_Closed(object sender, EventArgs e)
        {
            ScrcpyNetCtr?.Disconnect();
        }

        public ScrcpyNetCtr ScrcpyNetCtr { get; set; }
        private void Button_Click(object sender, RoutedEventArgs e)
        {

            AdbClient adbClient = new AdbClient();
            var devices = adbClient.GetDevices();
            if (devices.Count == 0)
            {
                MessageBox.Show("No device connected");
                return;
            }
            var device = devices[0];
            ScrcpyNetCtr.Connect(device.Serial);
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            ScrcpyNetCtr.Disconnect();
        }
    }
}
