using System;

namespace PDS800_WirelessTransmitter_Calibration
{
    /// <summary>
    ///     MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow
    {
        public MainWindow()
        {
            try
            {
                InitializeComponent();
            }
            catch (Exception ex)
            {
                var str = ex.StackTrace;
                Console.WriteLine(str);
            }
        }
    }
}