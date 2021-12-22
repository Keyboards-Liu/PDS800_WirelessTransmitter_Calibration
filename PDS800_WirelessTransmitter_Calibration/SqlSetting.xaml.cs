using System.Windows;


namespace PDS800_WirelessTransmitter_Calibration
{
    /// <summary>
    ///     SqlSetting.xaml 的交互逻辑
    /// </summary>
    public partial class SqlSetting : Window
    {
        public SqlSetting() => this.InitializeComponent();

        public string SetSqlServer { get; set; }
        public string SetSqlIntegratedSecurity { get; set; }
        public string SetSqlDatabase { get; set; }
        public string SetWorkSheet { get; set; }
        public bool SetFlag { get; set; }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            this.SetSqlServer = this.SqlServer.Text.Replace("\\\\", "\\");
            this.SetSqlIntegratedSecurity = this.SqlIntegratedSecurity.Text;
            this.SetSqlDatabase = this.SqlDatabase.Text;
            this.SetWorkSheet = this.WorkSheet.Text;
            this.SetFlag = true;
            this.Close();
        }
    }
}
