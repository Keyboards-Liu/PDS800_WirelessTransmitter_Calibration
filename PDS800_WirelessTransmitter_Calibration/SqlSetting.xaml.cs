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
using System.Windows.Shapes;


namespace PDS800_WirelessTransmitter_Calibration
{
    /// <summary>
    /// SqlSetting.xaml 的交互逻辑
    /// </summary>
    public partial class SqlSetting : Window
    {
        public string setSqlServer, setSqlIntegratedSecurity, setSqlDatabase, setWorkSheet;
        public SqlSetting()
        {
            InitializeComponent();
            this.Closing += Window_Closing;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {

            this.Close();
        }
        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            setSqlServer = SqlServer.Text.Replace("\\\\","\\");
            setSqlIntegratedSecurity = SqlIntegratedSecurity.Text;
            setSqlDatabase = SqlDatabase.Text;
            setWorkSheet = WorkSheet.Text;
        }
    }
}
