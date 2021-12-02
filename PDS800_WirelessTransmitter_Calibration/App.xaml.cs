using Lierda.WPFHelper;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace PDS800_WirelessTransmitter_Calibration
{
    /// <summary>
    /// App.xaml 的交互逻辑
    /// </summary>
    public partial class App : Application
    {
        public LierdaCracker Cracker { get; set; } = new LierdaCracker();

        protected override void OnStartup(StartupEventArgs e)
        {
            Cracker.Cracker(500);//垃圾回收间隔时间
            base.OnStartup(e);
        }
    }
}
