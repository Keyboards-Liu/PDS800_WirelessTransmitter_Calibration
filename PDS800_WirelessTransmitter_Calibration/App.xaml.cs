using Lierda.WPFHelper;
using System.Windows;
using System.Windows.Threading;

namespace PDS800_WirelessTransmitter_Calibration
{
    /// <summary>
    /// App.xaml 的交互逻辑
    /// </summary>
    public partial class App
    {
        void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            MessageBox.Show(e.Exception.ToString());
        }
    }
}