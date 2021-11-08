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

namespace PDS800_WirelessTransmitter_Calibration
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (true)
            {
                softWareMainWindow.Width = 800;
                softWareMainWindow.Height = 600;
            }
        }

        private void CalibrationBaseUserControl_Loaded(object sender, RoutedEventArgs e)
        {

        }

        private void DataDisplay_Loaded(object sender, RoutedEventArgs e)
        {

        }
    }
}