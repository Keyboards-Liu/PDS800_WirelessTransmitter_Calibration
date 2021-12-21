using System;
using System.Data.SqlClient;
using System.Globalization;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Research.DynamicDataDisplay;
using Microsoft.Research.DynamicDataDisplay.DataSources;

namespace PDS800_WirelessTransmitter_Calibration
{
    /// <summary>
    ///     CalibrationBase.xaml 的交互逻辑
    /// </summary>
    public partial class CalibrationBase
    {
        #region 基本定义

        // 串行端口
        public SerialPort NewSerialPort { get; } = new SerialPort();

        // 自动发送定时器
        public DispatcherTimer AutoSendTimer { get; } = new DispatcherTimer();

        // 自动检测定时器
        public DispatcherTimer AutoDetectionTimer { get; } = new DispatcherTimer();

        // 自动获取当前时间定时器
        public DispatcherTimer GetCurrentTimer { get; } = new DispatcherTimer();

        // 字符编码设定
        public Encoding SetEncoding { get; } = Encoding.Default;

        // 变量定义
        // 日期
        public string DateStr { get; private set; }

        // 时刻
        public string TimeStr { get; private set; }

        //// 发送和接收队列
        //private Queue receiveData = new Queue();
        //private Queue sendData = new Queue();
        // 发送和接收字节数
        public uint ReceiveBytesCount { get; private set; }

        public uint SendBytesCount { get; private set; }

        // 发送和接收次数
        //private uint receiveCount;

        //private uint sendCount;

        // 帧头
        public string FrameHeader { get; private set; }

        // 长度域
        public string FrameLength { get; private set; }

        // 命令域
        public string FrameCommand { get; private set; }

        // 数据地址域
        public string FrameAddress { get; private set; }

        // 非解析数据
        public string FrameUnparsed { get; private set; }

        // 数据内容域
        public string FrameContent { get; private set; }

        // 校验码
        public string FrameCyclicRedundancyCheck { get; private set; }

        // 实时数据
        public double RealTimeData { get; private set; }

        // Lora标志
        public bool IsLoRaFlag { get; private set; }

        #endregion

        #region 串口初始化/串口变更检测

        /// <summary>
        ///     串口初始化
        /// </summary>
        public CalibrationBase()
        {
            // 初始化组件
            this.InitializeComponent();
            // 检测和添加串口
            this.AddPortName();
            // 开启串口检测定时器，并设置自动检测1秒1次
            this.AutoDetectionTimer.Tick += this.AutoDetectionTimer_Tick;
            this.AutoDetectionTimer.Interval = new TimeSpan(0, 0, 0, 1, 0);
            this.AutoDetectionTimer.Start();
            // 开启当前时间定时器，并设置自动检测100毫秒1次
            this.GetCurrentTimer.Tick += this.GetCurrentTime;
            this.GetCurrentTimer.Interval = new TimeSpan(0, 0, 0, 0, 100);
            this.GetCurrentTimer.Start();
            // 设置自动发送定时器，并设置自动检测100毫秒1次
            this.AutoSendTimer.Tick += this.AutoSendTimer_Tick;
            // 设置定时时间，开启定时器
            this.AutoSendTimer.Interval = new TimeSpan(0, 0, 0, 0, Convert.ToInt32(this.AutoSendCycleTextBox.Text));
            // 设置状态栏提示
            this.StatusTextBlock.Text = "准备就绪";
        }

        /// <summary>
        ///     显示当前时间
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public void GetCurrentTime(object sender, EventArgs e)
        {
            this.DateStr = DateTime.Now.ToString("yyyy-MM-dd");
            this.TimeStr = DateTime.Now.ToString("HH:mm:ss");
            this.OperationTime.Text = this.DateStr + " " + this.TimeStr;
        }

        /// <summary>
        ///     在初始化串口时进行串口检测和添加
        /// </summary>
        private void AddPortName()
        {
            // 检测有效串口，去掉重复串口
            var serialPortName = SerialPort.GetPortNames().Distinct().ToArray();
            // 在有效串口号中遍历当前打开的串口号
            foreach (var name in serialPortName)
            {
                // 如果检测到的串口不存在于portNameComboBox中，则添加
                if (this.PortNameComboBox.Items.Contains(name) == false)
                {
                    this.PortNameComboBox.Items.Add(name);
                }
            }

            this.PortNameComboBox.SelectedIndex = 0;
        }

        /// <summary>
        ///     在串口运行时进行串口检测和更改
        /// </summary>
        /// <param name="sender">事件源的对象</param>
        /// <param name="e">事件数据的对象</param>
        private void AutoDetectionTimer_Tick(object sender, EventArgs e)
        {
            try
            {
                // 检测有效串口，去掉重复串口
                var serialPortName = SerialPort.GetPortNames().Distinct().ToArray();
                // 如果没有运行
                //AddPortName();
                // 如果正在运行
                if (this.TurnOnButton.IsChecked == true)
                {
                    // 在有效串口号中遍历当前打开的串口号
                    foreach (var name in serialPortName)
                    {
                        // 如果找到串口，说明串口仍然有效，跳出循环
                        if (this.NewSerialPort.PortName == name)
                        {
                            return;
                        }
                    }

                    // 如果找不到, 说明串口失效了，关闭串口并移除串口名
                    this.TurnOnButton.IsChecked = false;
                    this.PortNameComboBox.Items.Remove(this.NewSerialPort.PortName);
                    this.PortNameComboBox.SelectedIndex = 0;
                    // 输出提示信息
                    this.StatusTextBlock.Text = "串口失效，已自动断开";
                }
                else
                {
                    // 检查有效串口和ComboBox中的串口号个数是否不同
                    if (this.PortNameComboBox.Items.Count != serialPortName.Length)
                    {
                        // 串口数不同，清空ComboBox
                        this.PortNameComboBox.Items.Clear();
                        // 重新添加有效串口
                        foreach (var name in serialPortName)
                        {
                            this.PortNameComboBox.Items.Add(name);
                        }

                        this.PortNameComboBox.SelectedIndex = -1;
                        // 输出提示信息
                        this.StatusTextBlock.Text = "串口列表已更新！";
                    }
                }
            }
            catch (Exception ex)
            {
                var str = ex.StackTrace;
                Console.WriteLine(str);
                this.TurnOnButton.IsChecked = false;
                this.StatusTextBlock.Text = "串口检测错误！";
            }
        }

        #endregion

        #region 打开/关闭串口

        /// <summary>
        ///     串口配置面板
        /// </summary>
        /// <param name="state">使能状态</param>
        private void SerialSettingControlState(bool state)
        {
            // state状态为true时, ComboBox不可用, 反之可用
            this.PortNameComboBox.IsEnabled = state;
            this.BaudRateComboBox.IsEnabled = state;
            this.ParityComboBox.IsEnabled = state;
            this.DataBitsComboBox.IsEnabled = state;
            this.StopBitsComboBox.IsEnabled = state;
        }

        /// <summary>
        ///     打开串口按钮
        /// </summary>
        /// <param name="sender">事件源的对象</param>
        /// <param name="e">事件数据的对象</param>
        private void TurnOnButton_Checked(object sender, RoutedEventArgs e)
        {
            try
            {
                // 获取面板中的配置, 并设置到串口属性中
                this.NewSerialPort.PortName = this.PortNameComboBox.Text;
                this.NewSerialPort.BaudRate = Convert.ToInt32(this.BaudRateComboBox.Text);
                this.NewSerialPort.Parity = (Parity)Enum.Parse(typeof(Parity), this.ParityComboBox.Text);
                this.NewSerialPort.DataBits = Convert.ToInt16(this.DataBitsComboBox.Text);
                this.NewSerialPort.StopBits = (StopBits)Enum.Parse(typeof(StopBits), this.StopBitsComboBox.Text);
                this.NewSerialPort.Encoding = this.SetEncoding;
                // 添加串口事件处理, 设置委托
                this.NewSerialPort.DataReceived += this.ReceiveData;
                // 关闭串口配置面板, 开启串口, 变更按钮文本, 打开绿灯, 显示提示文字
                this.SerialSettingControlState(false);
                this.NewSerialPort.Open();
                this.StatusTextBlock.Text = "串口已开启";
                this.SerialPortStatusEllipse.Fill = Brushes.Green;
                this.TurnOnButton.Content = "关闭串口";
                // 设置超时
                this.NewSerialPort.ReadTimeout = 500;
                this.NewSerialPort.WriteTimeout = 500;
                // 清空缓冲区
                this.NewSerialPort.DiscardInBuffer();
                this.NewSerialPort.DiscardOutBuffer();
                // 使能作图区
                this.DataSource = new ObservableDataSource<Point>();
                this.Plot_Loaded();
            }
            catch (Exception ex)
            {
                var str = ex.StackTrace;
                Console.WriteLine(str);
                // 异常时显示提示文字
                this.StatusTextBlock.Text = "开启串口出错！";
                this.NewSerialPort.Close();
                this.AutoSendTimer.Stop();
                this.TurnOnButton.IsChecked = false;
                this.SerialSettingControlState(true);
            }
        }

        /// <summary>
        ///     关闭串口按钮
        /// </summary>
        /// <param name="sender">事件源的对象</param>
        /// <param name="e">事件数据的对象</param>
        private void TurnOnButton_Unchecked(object sender, RoutedEventArgs e) // 关闭串口
        {
            try
            {
                // 关闭端口, 关闭自动发送定时器, 使能串口配置面板, 变更按钮文本, 关闭绿灯, 显示提示文字 
                this.NewSerialPort.Close();
                this.AutoSendTimer.Stop();
                this.SerialSettingControlState(true);
                this.StatusTextBlock.Text = "串口已关闭";
                this.SerialPortStatusEllipse.Fill = Brushes.Gray;
                this.TurnOnButton.Content = "打开串口";
                // 关闭作图
                this.Plotter.Children.RemoveAll(typeof(LineGraph));
                this.PlotPointX = 1;
                // 关闭连接
            }
            catch (Exception ex)
            {
                var str = ex.StackTrace;
                Console.WriteLine(str);
                // 异常时显示提示文字
                this.StatusTextBlock.Text = "关闭串口出错！";
                this.TurnOnButton.IsChecked = true;
            }
        }

        #endregion

        #region 串口数据接收处理/窗口显示清空功能
        /// <summary>
        ///     前报文尾帧
        /// </summary>
        public string BrokenFrameEnd { get; private set; } = "";

        /// <summary>
        ///     接收报文
        /// </summary>
        public string ReceiveText { get; private set; } = "";

        /// <summary>
        ///     接收串口数据, 转换为16进制字符串, 传递到显示功能
        /// </summary>
        /// <param name="sender">事件源的对象</param>
        /// <param name="e">事件数据的对象</param>
        public void ReceiveData(object sender, SerialDataReceivedEventArgs e)
        {
            //receiveCount++;
            // Console.WriteLine("接收" + receiveCount + "次");
            // 读取缓冲区内所有字节
            try
            {
                var receiveBuffer = new byte[this.NewSerialPort.BytesToRead];
                this.NewSerialPort.Read(receiveBuffer, 0, receiveBuffer.Length);
                // 字符串转换为十六进制字符串
                this.ReceiveText = BytesToHexStr(receiveBuffer);
            }
            catch (Exception ex)
            {
                var str = ex.StackTrace;
                Console.WriteLine(str);
                this.ReceiveText = "";
            }

            // 加入上一次断帧尾数
            this.ReceiveText = (this.BrokenFrameEnd + " " + this.ReceiveText).Trim(' ');
            // 对字符串进行断帧
            var segmentationStr = this.MessageSegmentationExtraction(this.ReceiveText);
            // 断帧情况判断
            switch (segmentationStr.Length)
            {
                // 如果只有一帧，要不是空帧，要不是废帧
                case 1:
                    this.BrokenFrameEnd = segmentationStr[0];
                    break;
                // 如果有两帧，只有一个头帧和一个可用帧
                case 2:
                    this.AvailableMessageHandler(segmentationStr[1]);
                    break;
                // 如果有多于三帧，则头帧，可用帧，尾帧都有
                default:
                    var useStr = new string[segmentationStr.Length - 2];
                    Array.Copy(segmentationStr, 1, useStr, 0, segmentationStr.Length - 2);
                    foreach (var item in useStr)
                    {
                        this.AvailableMessageHandler(item);
                    }

                    this.BrokenFrameEnd = segmentationStr[segmentationStr.Length - 1];
                    break;
            }

            //// 传参 (Invoke方法暂停工作线程, BeginInvoke方法不暂停)
            //AvailableMessageHandler(txt);
        }

        /// <summary>
        ///     根据帧头和内容不同进行不同方法处理
        /// </summary>
        /// <param name="txt"></param>
        /// <returns></returns>
        private void AvailableMessageHandler(string txt)
        {
            if (txt.Length < 2)
            {
                return;
            }

            try
            {
                switch (txt.Substring(0, 2))
                {
                    // Zigbee 四信解析
                    case "FE":
                        {
                            // 不解析确认帧，只显示
                            if (this.ConnectFlag == true &&
                                txt.Length >= 8 &&
                                txt.Substring(txt.Length - 8, 5) == "F0 55")
                            {
                                this.ConfirmationFrameResponse(txt);
                            }
                            // 如果是能解析的帧（常规数据帧或基本参数帧），就全面板显示
                            else if (txt.Substring(6, 5) == "44 5F" &&
                                (txt.Length + 1) / 3 == 27 ||
                                (txt.Length + 1) / 3 == 81)
                            {
                                this.StatusReceiveByteTextBlock.Dispatcher.Invoke(this.FullPanelDisplay(txt));
                            }
                            // 如果是不能解析的帧，就部分显示
                            else if (txt.Replace(" ", "") != "")
                            {
                                this.StatusReceiveByteTextBlock.Dispatcher.Invoke(this.PartialPanelDisplay(txt));
                            }
                        }
                        break;
                    // Zigbee Digi和LoRa解析
                    case "7E":
                        {
                            // 不解析确认帧，只显示
                            if (this.ConnectFlag == true &&
                                txt.Length >= 8 &&
                                txt.Substring(txt.Length - 8, 5) == "F0 55")
                            {
                                this.ConfirmationFrameResponse(txt);
                            }
                            // 如果是能解析的LoRa帧（常规数据帧或基本参数帧），就全面板显示
                            else if ((txt.Length + 1) / 3 == 24 ||
                                (txt.Length + 1) / 3 == 78)
                            {
                                this.IsLoRaFlag = true;
                                this.StatusReceiveByteTextBlock.Dispatcher.Invoke(this.FullPanelDisplay(txt));
                            }
                            // 如果是能解析的Digi帧（常规数据帧或基本参数帧），就全面板显示
                            else if (txt.Substring(9, 2) == "91" &&
                                (txt.Length + 1) / 3 == 42 ||
                                (txt.Length + 1) / 3 == 96)
                            {
                                this.IsLoRaFlag = false;
                                this.StatusReceiveByteTextBlock.Dispatcher.Invoke(this.FullPanelDisplay(txt));
                            }
                            // 如果是不能解析的帧，就部分显示
                            else if (txt.Replace(" ", "") != "")
                            {
                                this.StatusReceiveByteTextBlock.Dispatcher.Invoke(this.PartialPanelDisplay(txt));
                            }
                        }
                        break;

                    default:
                        this.StatusReceiveByteTextBlock.Dispatcher.Invoke(this.PartialPanelDisplay(txt));
                        break;
                }
            }
            catch (Exception ex)
            {
                var str = ex.StackTrace;
                Console.WriteLine(str);
            }
        }

        private void ConfirmationFrameResponse(string txt)
        {
            this.ConnectFlag = false;
            this.ConnectionStatusEllipse.Dispatcher.Invoke(this.ConnectionStatusEllipseColorCovert());
            this.EstablishConnectionButton.Dispatcher.Invoke(this.EstablishConnectionButtonEnabled());
            this.StatusReceiveByteTextBlock.Dispatcher.Invoke(this.PartialPanelDisplay(txt));
            this.StatusReceiveByteTextBlock.Dispatcher.Invoke(this.ConnectSuccessDisplay());
            MessageBox.Show("与无线仪表连接成功");
        }

        private Action ConnectSuccessDisplay() => this.ConnectSuccessDisplay_Action;

        private void ConnectSuccessDisplay_Action() => this.StatusTextBlock.Text = "仪表连接成功！";

        private Action EstablishConnectionButtonEnabled() => this.EstablishConnectionButtonEnabled_Action;

        private void EstablishConnectionButtonEnabled_Action() => this.EstablishConnectionButton.IsEnabled = true;

        private Action ConnectionStatusEllipseColorCovert() => this.ConnectionStatusEllipseColorCovert_Action;

        private void ConnectionStatusEllipseColorCovert_Action() => this.ConnectionStatusEllipse.Fill = Brushes.Green;

        private Action PartialPanelDisplay(string txt) => delegate { this.ShowReceiveData(txt); };

        private Action FullPanelDisplay(string txt) => delegate
                                                                 {
                                                                     this.ShowReceiveData(txt);
                                                                     this.InstrumentDataSegmentedText(txt);
                                                                     this.ShowParseParameter(txt);
                                                                     this.SendConfirmationFrame(txt);
                                                                 };

        /// <summary>
        ///     自动发送确认帧
        /// </summary>
        /// <param name="txt"></param>
        private void SendConfirmationFrame(string txt)
        {
            // 如果不需要建立仪表连接，发送常规确认帧
            if (this.ConnectFlag == false)
            {
                try
                {
                    switch (txt.Substring(0, 2))
                    {
                        case "FE":
                            {
                                if ((txt.Length + 1) / 3 == 27)
                                {
                                    var str = this.RegularDataConfirmationFrame();
                                    this.SerialPortSend(str);
                                }
                                else if ((txt.Length + 1) / 3 == 81)
                                {
                                    var str = this.BasicInformationConfirmationFrame();
                                    this.SerialPortSend(str);
                                }
                            }
                            break;
                        case "7E":
                            {
                                switch ((txt.Length + 1) / 3)
                                {
                                    case 42:
                                    case 24:
                                        {
                                            var str = this.RegularDataConfirmationFrame();
                                            this.SerialPortSend(str);
                                            break;
                                        }
                                    case 96:
                                    case 78:
                                        {
                                            var str = this.BasicInformationConfirmationFrame();
                                            this.SerialPortSend(str);
                                            break;
                                        }
                                }
                            }
                            break;
                    }
                }
                catch (Exception ex)
                {
                    var str = ex.StackTrace;
                    Console.WriteLine(str);
                }
            }
            // 如果需要建立仪表连接，用仪表连接帧替代确认帧
            else
            {
                try
                {
                    var str = this.EstablishBuild_Text();
                    this.SerialPortSend(str);
                }
                catch (Exception ex)
                {
                    var str = ex.StackTrace;
                    Console.WriteLine(str);
                }
            }
        }

        /// <summary>
        ///     接收窗口显示功能
        /// </summary>
        /// <param name="txt">需要窗口显示的字符串</param>
        private void ShowReceiveData(string txt)
        {
            // 更新接收字节数           
            this.ReceiveBytesCount += (uint)((txt.Length + 1) / 3);
            this.StatusReceiveByteTextBlock.Text = this.ReceiveBytesCount.ToString();
            // 在接收窗口中显示字符串
            if (txt.Replace(" ", "").Length >= 0)
            {
                // 接收窗口自动清空
                if (this.AutoClearCheckBox.IsChecked == true)
                {
                    this.DisplayTextBox.Clear();
                }

                this.DisplayTextBox.AppendText(DateTime.Now + " <-- " + txt + "\r\n");
                this.DisplayScrollViewer.ScrollToEnd();
            }
        }

        /// <summary>
        ///     仪表数据分段功能
        /// </summary>
        /// <param name="txt"></param>
        public void InstrumentDataSegmentedText(string txt)
        {
            // 仪表上行数据分段
            try
            {
                switch (txt.Substring(0, 2))
                {
                    case "FE":
                        {
                            // 帧头 (1位)
                            this.FrameHeader = txt.Substring(0 * 3, 1 * 3 - 1);
                            // 长度域 (1位, 最长为FF = 255)
                            this.FrameLength = txt.Substring((0 + 1) * 3, 1 * 3 - 1);
                            // 命令域 (2位)
                            this.FrameCommand = txt.Substring((0 + 1 + 1) * 3, 2 * 3 - 1);
                            // 数据域 (长度域指示长度)
                            // 数据地址域 (2位)
                            this.FrameAddress = txt.Substring((0 + 1 + 1 + 2) * 3, 2 * 3 - 1);
                            // 数据内容域 (去掉头6位，尾1位)
                            this.FrameContent = txt.Substring(6 * 3, txt.Length - 6 * 3 - 3);
                            // 校验码 (1位)
                            this.FrameCyclicRedundancyCheck = txt.Substring(txt.Length - 2, 2);
                        }
                        break;
                    case "7E":
                        {
                            // 如果是LoRa
                            if (this.IsLoRaFlag)
                            {
                                // 帧头 (1位)
                                this.FrameHeader = txt.Substring(0 * 3, 1 * 3 - 1);
                                // 长度域 (2位, 最长为FF = 65535)
                                this.FrameLength = txt.Substring((0 + 1) * 3, 2 * 3 - 1);
                                // 命令域 (0位，指示是否收到数据)
                                this.FrameCommand = "";
                                // 数据地址域 (0位)
                                this.FrameAddress = "";
                                // 非解析帧 (0位)
                                this.FrameUnparsed = txt.Substring((0 + 1 + 2 + 1 + 8) * 3, 9 * 3 - 1);
                                // 数据内容域 (去掉头3位，尾1位)
                                this.FrameContent = txt.Substring(3 * 3, txt.Length - 3 * 3 - 3);
                                // 校验码 (1位)
                                this.FrameCyclicRedundancyCheck = txt.Substring(txt.Length - 2, 2);
                            }
                            // 如果是Digi
                            else
                            {
                                // 帧头 (1位)
                                this.FrameHeader = txt.Substring(0 * 3, 1 * 3 - 1);
                                // 长度域 (2位, 最长为FF = 65535)
                                this.FrameLength = txt.Substring((0 + 1) * 3, 2 * 3 - 1);
                                // 命令域 (1位，指示是否收到数据)
                                this.FrameCommand = txt.Substring((0 + 1 + 2) * 3, 1 * 3 - 1);
                                // 数据地址域 (8位)
                                this.FrameAddress = txt.Substring((0 + 1 + 2 + 1) * 3, 8 * 3 - 1);
                                // 非解析帧 (9位)
                                this.FrameUnparsed = txt.Substring((0 + 1 + 2 + 1 + 8) * 3, 9 * 3 - 1);
                                // 数据内容域 (去掉头21位，尾1位)
                                this.FrameContent = txt.Substring(21 * 3, txt.Length - 21 * 3 - 3);
                                // 校验码 (1位)
                                this.FrameCyclicRedundancyCheck = txt.Substring(txt.Length - 2, 2);
                            }
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                var str = ex.StackTrace;
                Console.WriteLine(str);
                // 异常时显示提示文字
                this.StatusTextBlock.Text = "文本解析出错！";
            }
        }

        /// <summary>
        ///     仪表参数解析面板显示功能
        /// </summary>
        /// <param name="txt"></param>
        private void ShowParseParameter(string txt)
        {
            // 面板清空
            this.ParseParameterClear();
            // 仪表参数解析面板写入
            try
            {
                switch (txt.Substring(0, 2))
                {
                    case "FE":
                        {
                            ////字符串校验(已移动到断帧部分)
                            ////string j = this.CalCheckCode_FE(txt);
                            ////if (j == this.FrameCyclicRedundancyCheck)
                            //if (true)
                            //{
                            this.ResCyclicRedundancyCheck.Text = "通过";
                            // 校验成功写入其他解析参数
                            // 无线仪表数据域帧头
                            {
                                // 通讯协议
                                try
                                {
                                    // 1 0x0001 ZigBee SZ9-GRM V3.01油田专用通讯协议（国产四信）
                                    //string frameProtocol = frameContent.Substring(0, 5).Replace(" ", "");
                                    //int intFrameProtocol = Convert.ToInt32(frameProtocol, 16);
                                    //switch (intFrameProtocol)
                                    //{
                                    //    case 0x0001:
                                    this.ResProtocol.Text = "ZigBee SZ9-GRM V3.01油田专用通讯协议（国产四信）";
                                    this.ResProtocolDockPanel.Visibility = Visibility.Visible;
                                    //        break;
                                    //    default:
                                    //        resProtocol.Text = "未知";
                                    //        resProtocol.Foreground = new SolidColorBrush(Colors.Red);
                                    //        break;
                                    //}
                                }
                                catch (Exception ex)
                                {
                                    var str = ex.StackTrace;
                                    Console.WriteLine(str);
                                    // 异常时显示提示文字
                                    this.StatusTextBlock.Text = "通信协议解析出错！";
                                    this.TurnOnButton.IsChecked = false;
                                    return;
                                }

                                // 网络地址
                                try
                                {
                                    var frameContentAddress =
                                        (this.FrameAddress.Substring(3, 2) + this.FrameAddress.Substring(0, 2)).Replace(" ", "");
                                    var intFrameContentAddress = Convert.ToInt32(frameContentAddress, 16);
                                    this.ResAddress.Text = intFrameContentAddress.ToString();
                                    this.ResAddressDockPanel.Visibility = Visibility.Visible;
                                }
                                catch (Exception ex)
                                {
                                    var str = ex.StackTrace;
                                    Console.WriteLine(str);
                                    // 异常时显示提示文字
                                    this.StatusTextBlock.Text = "网络地址解析出错！";
                                    this.TurnOnButton.IsChecked = false;
                                    return;
                                }

                                // 厂商号
                                try
                                {
                                    var frameContentVendor = this.FrameContent.Substring(6, 5).Replace(" ", "");
                                    var intFrameContentVendor = Convert.ToInt32(frameContentVendor, 16);
                                    // 1 0x0001 厂商1
                                    // 2 0x0002 厂商2
                                    // 3 0x0003 厂商3
                                    // 4 ......
                                    // N 0x8001~0xFFFF 预留
                                    if (intFrameContentVendor > 0x0000 && intFrameContentVendor < 0x8001)
                                    {
                                        this.ResVendor.Text = "厂商" + intFrameContentVendor;
                                    }
                                    else if (intFrameContentVendor >= 0x8001 && intFrameContentVendor <= 0xFFFF)
                                    {
                                        this.ResVendor.Text = "预留厂商";
                                    }
                                    else
                                    {
                                        this.ResVendor.Text = "未定义";
                                        this.ResVendor.Foreground = new SolidColorBrush(Colors.Red);
                                    }

                                    this.ResVendorDockPanel.Visibility = Visibility.Visible;
                                }
                                catch (Exception ex)
                                {
                                    var str = ex.StackTrace;
                                    Console.WriteLine(str);
                                    // 异常时显示提示文字
                                    this.StatusTextBlock.Text = "厂商号解析出错！";
                                    this.TurnOnButton.IsChecked = false;
                                    return;
                                }

                                // 仪表类型
                                try
                                {
                                    var frameContentType = this.FrameContent.Substring(12, 5).Replace(" ", "");
                                    var intFrameContentType = Convert.ToInt32(frameContentType, 16);
                                    // 1  0x0001 无线一体化负荷
                                    // 2  0x0002 无线压力
                                    // 3  0x0003 无线温度
                                    // 4  0x0004 无线电量
                                    // 5  0x0005 无线角位移
                                    // 6  0x0006 无线载荷
                                    // 7  0x0007 无线扭矩
                                    // 8  0x0008 无线动液面
                                    // 9  0x0009 计量车
                                    //    0x000B 无线压力温度一体化变送器
                                    //    ......
                                    // 10 0x1f00 控制器(RTU)设备
                                    // 11 0x1f10 手操器
                                    // 12 ......
                                    // N  0x2000~0x4000 自定义
                                    //    0x2000 无线死点开关
                                    //    0x3000 无线拉线位移校准传感器
                                    //    0x3001 无线拉线位移功图校准传感器
                                    switch (intFrameContentType)
                                    {
                                        case 0x0001:
                                            this.ResType.Text = "无线一体化负荷";
                                            break;
                                        case 0x0002:
                                            this.ResType.Text = "无线压力";
                                            break;
                                        case 0x0003:
                                            this.ResType.Text = "无线温度";
                                            break;
                                        case 0x0004:
                                            this.ResType.Text = "无线电量";
                                            break;
                                        case 0x0005:
                                            this.ResType.Text = "无线角位移";
                                            break;
                                        case 0x0006:
                                            this.ResType.Text = "无线载荷";
                                            break;
                                        case 0x0007:
                                            this.ResType.Text = "无线扭矩";
                                            break;
                                        case 0x0008:
                                            this.ResType.Text = "无线动液面";
                                            break;
                                        case 0x0009:
                                            this.ResType.Text = "计量车";
                                            break;
                                        case 0x000B:
                                            this.ResType.Text = "无线压力温度一体化变送器";
                                            break;
                                        case 0x1F00:
                                            this.ResType.Text = "控制器(RTU)设备";
                                            break;
                                        case 0x1F10:
                                            this.ResType.Text = "手操器";
                                            break;
                                        // 自定义
                                        case 0x2000:
                                            this.ResType.Text = "温度型";
                                            break;
                                        case 0x3000:
                                            this.ResType.Text = "无线拉线位移校准传感器";
                                            break;
                                        case 0x3001:
                                            this.ResType.Text = "无线拉线位移功图校准传感器";
                                            break;
                                        default:
                                            this.ResType.Clear();
                                            break;
                                    }

                                    while (this.ResType.Text.Trim() == string.Empty)
                                    {
                                        if (intFrameContentType <= 0x4000 && intFrameContentType >= 0x3000)
                                        {
                                            this.ResType.Text = "自定义";
                                        }
                                        else
                                        {
                                            this.ResType.Text = "未定义";
                                            this.ResType.Foreground = new SolidColorBrush(Colors.Red);
                                        }
                                    }

                                    this.ResTypeDockPanel.Visibility = Visibility.Visible;
                                }
                                catch (Exception ex)
                                {
                                    var str = ex.StackTrace;
                                    Console.WriteLine(str);
                                    // 异常时显示提示文字
                                    this.StatusTextBlock.Text = "仪表类型解析出错！";
                                    this.TurnOnButton.IsChecked = false;
                                    return;
                                }

                                // 仪表组号
                                try
                                {
                                    this.ResGroup.Text =
                                        Convert.ToInt32(this.FrameContent.Substring(18, 2).Replace(" ", ""), 16) + "组" +
                                        Convert.ToInt32(this.FrameContent.Substring(21, 2).Replace(" ", ""), 16) + "号";
                                    this.ResGroup.Visibility = Visibility.Visible;
                                }
                                catch (Exception ex)
                                {
                                    var str = ex.StackTrace;
                                    Console.WriteLine(str);
                                    // 异常时显示提示文字
                                    this.StatusTextBlock.Text = "仪表组号解析出错！";
                                    this.TurnOnButton.IsChecked = false;
                                    return;
                                }

                                // 数据类型
                                try
                                {
                                    var frameContentFunctionData = this.FrameContent.Substring(24, 5).Replace(" ", "");
                                    var intFrameContentFunctionData = Convert.ToInt32(frameContentFunctionData, 16);
                                    // 1  0x0000 常规数据
                                    // 2  ……
                                    // 3  0x0010 仪表参数
                                    // 4  ……
                                    // 5  0x0020 读数据命令
                                    // 6 
                                    // 7  ……
                                    // 8 
                                    // 9 
                                    // 10 ……
                                    // 11 
                                    // 12 ……
                                    // 13 0x0100 控制器参数写应答（控制器应答命令）
                                    // 14 0x0101 控制器读仪表参数应答（控制器应答命令）
                                    // 15 ……
                                    // 16 0x0200 控制器应答一体化载荷位移示功仪功图参数应答（控制器应答命令触发功图采集）
                                    // 17 0x0201 控制器应答功图数据命令
                                    // 18 0x0202 控制器读功图数据应答（控制器应答命令读已有功图）
                                    // 19 ……
                                    // 20 0x0300 控制器(RTU)对仪表控制命令
                                    // 21 0x400~0x47f 配置协议命令
                                    // 22 0x480~0x5ff 标定协议命令
                                    // 23 0x1000~0x2000 厂家自定义数据类型
                                    // 24 ……
                                    // 25 0x8000－0xffff 预留
                                    switch (intFrameContentFunctionData)
                                    {
                                        case 0x0000:
                                            this.ResFunctionData.Text = "常规数据（仪表实时数据）";
                                            if ((txt.Length + 1) / 3 == 27)
                                            {
                                                // 无线仪表数据段
                                                // 通信效率
                                                try
                                                {
                                                    this.ResSucRate.Text =
                                                        Convert.ToInt32(this.FrameContent.Substring(30, 2), 16) + "%";
                                                    this.ResSucRateDockPanel.Visibility = Visibility.Visible;
                                                }
                                                catch (Exception ex)
                                                {
                                                    var str = ex.StackTrace;
                                                    Console.WriteLine(str);
                                                    // 异常时显示提示文字
                                                    this.StatusTextBlock.Text = "通信效率解析出错！";
                                                    this.TurnOnButton.IsChecked = false;
                                                    return;
                                                }

                                                // 电池电压
                                                try
                                                {
                                                    this.ResBatVol.Text =
                                                        Convert.ToInt32(this.FrameContent.Substring(33, 2), 16) + "%";
                                                    this.ResBatVolDockPanel.Visibility = Visibility.Visible;
                                                }
                                                catch (Exception ex)
                                                {
                                                    var str = ex.StackTrace;
                                                    Console.WriteLine(str);
                                                    // 异常时显示提示文字
                                                    this.StatusTextBlock.Text = "电池电压解析出错！";
                                                    this.TurnOnButton.IsChecked = false;
                                                    return;
                                                }

                                                // 休眠时间
                                                try
                                                {
                                                    var sleepTime =
                                                        Convert.ToInt32(this.FrameContent.Substring(36, 5).Replace(" ", ""),
                                                            16);
                                                    this.ResSleepTime.Text = sleepTime + "秒";
                                                    this.ResSleepTimeDockPanel.Visibility = Visibility.Visible;
                                                    if (this.RegularDataUpdateRate.Text == "")
                                                    {
                                                        this.RegularDataUpdateRate.Text = Convert.ToString(sleepTime);
                                                    }
                                                }
                                                catch (Exception ex)
                                                {
                                                    var str = ex.StackTrace;
                                                    Console.WriteLine(str);
                                                    // 异常时显示提示文字
                                                    this.StatusTextBlock.Text = "休眠时间解析出错！";
                                                    this.TurnOnButton.IsChecked = false;
                                                    return;
                                                }

                                                // 仪表状态
                                                try
                                                {
                                                    var frameStatue = this.FrameContent.Substring(42, 5).Replace(" ", "");
                                                    var binFrameStatue = Convert
                                                        .ToString(Convert.ToInt32(frameStatue, 16), 2).PadLeft(8, '0');
                                                    if (Convert.ToInt32(binFrameStatue.Replace(" ", ""), 2) != 0)
                                                    {
                                                        this.ResStatue.Text = "故障";
                                                        var failureMessage = "";
                                                        var count = 0;
                                                        // 1 Bit0 仪表故障
                                                        // 2 Bit1 参数错误
                                                        // 3 Bit2 电池欠压，日月协议中仍然保留
                                                        // 4 Bit3 AI1 上限报警
                                                        // 5 Bit4 AI1 下限报警
                                                        // 6 Bit5 AI2 上限报警
                                                        // 7 Bit6 AI2 下限报警
                                                        // 8 Bit7 预留
                                                        for (var a = 0; a < 8; a++)
                                                        {
                                                            // 从第0位到第7位
                                                            if (binFrameStatue.Substring(a, 1) == "1")
                                                            {
                                                                switch (a)
                                                                {
                                                                    case 0:
                                                                        failureMessage += ++count + " 仪表故障\n";
                                                                        break;
                                                                    case 1:
                                                                        failureMessage += ++count + " 参数故障\n";
                                                                        break;
                                                                    case 2:
                                                                        failureMessage += ++count + " 电池欠压\n";
                                                                        break;
                                                                    case 3:
                                                                        failureMessage += ++count + " 压力上限报警\n";
                                                                        break;
                                                                    case 4:
                                                                        failureMessage += ++count + " 压力下限报警\n";
                                                                        break;
                                                                    case 5:
                                                                        failureMessage += ++count + " 温度上限报警\n";
                                                                        break;
                                                                    case 6:
                                                                        failureMessage += ++count + " 温度下限报警\n";
                                                                        break;
                                                                    case 7:
                                                                        failureMessage += ++count + " 未定义故障\n";
                                                                        break;
                                                                    default:
                                                                        failureMessage += "参数错误\n";
                                                                        break;
                                                                }
                                                            }
                                                        }

                                                        this.StatusTextBlock.Text = failureMessage;
                                                        //string messageBoxText = "设备上报" + count + "个故障: \n" + failureMessage;
                                                        //string caption = "设备故障";
                                                        //MessageBoxButton button = MessageBoxButton.OK;
                                                        //MessageBoxImage icon = MessageBoxImage.Error;
                                                        //MessageBox.Show(messageBoxText, caption, button, icon);
                                                    }
                                                    else
                                                    {
                                                        this.ResStatue.Text = "正常";
                                                    }

                                                    this.ResStatueDockPanel.Visibility = Visibility.Visible;
                                                }
                                                catch (Exception ex)
                                                {
                                                    var str = ex.StackTrace;
                                                    Console.WriteLine(str);
                                                    // 异常时显示提示文字
                                                    this.StatusTextBlock.Text = "仪表状态解析出错！";
                                                    this.TurnOnButton.IsChecked = false;
                                                    return;
                                                }

                                                // 运行时间
                                                try
                                                {
                                                    this.ResTime.Text =
                                                        Convert.ToInt32(this.FrameContent.Substring(48, 5).Replace(" ", ""),
                                                            16) + "小时";
                                                    this.ResTimeDockPanel.Visibility = Visibility.Visible;
                                                }
                                                catch (Exception ex)
                                                {
                                                    var str = ex.StackTrace;
                                                    Console.WriteLine(str);
                                                    // 异常时显示提示文字
                                                    this.StatusTextBlock.Text = "运行时间解析出错！";
                                                    this.TurnOnButton.IsChecked = false;
                                                    return;
                                                }

                                                // 实时数据
                                                try
                                                {
                                                    var frameResData = Convert
                                                        .ToInt32(this.FrameContent.Substring(54, 5).Replace(" ", ""), 16).ToString();
                                                    this.ResData.Text = frameResData;
                                                    this.ResDataDockPanel.Visibility = Visibility.Visible;
                                                    try
                                                    {
                                                        this.RealTimeData = Convert.ToDouble(frameResData);
                                                    }
                                                    catch (Exception ex)
                                                    {
                                                        var str = ex.StackTrace;
                                                        Console.WriteLine(str);
                                                    }
                                                    // 作图
                                                    this.AnimatedPlot();
                                                    // 写数据库
                                                    if (this.SqlConnectButton.IsChecked == true
                                                        && this.AutoSaveToSqlCheckBox.IsChecked.Value)
                                                    {
                                                        string type = this.ResType.Text, address = this.ResAddress.Text,
                                                            protocol = this.ResProtocol.Text, data = this.ResData.Text,
                                                            statue = this.ResStatue.Text, workSheet = WorkSheet;
                                                        var sql = this.CalibrationSqlConnect;
                                                        this.DatabaseWrite(type, protocol, address, data, statue, workSheet, sql);
                                                    }
                                                }
                                                catch (Exception ex)
                                                {
                                                    var str = ex.StackTrace;
                                                    Console.WriteLine(str);
                                                    // 异常时显示提示文字
                                                    this.StatusTextBlock.Text = "实时数据解析出错！";
                                                    this.TurnOnButton.IsChecked = false;
                                                    return;
                                                }
                                            }

                                            break;
                                        case 0x0010:
                                            this.ResFunctionData.Text = "常规数据（仪表基本参数）";
                                            if ((txt.Length + 1) / 3 == 81)
                                            {
                                                // 无线仪表数据段
                                                // 仪表型号
                                                try
                                                {
                                                    this.ResModel.Text = this.FrameContent.Substring(30, 23);
                                                    this.ResModelDockPanel.Visibility = Visibility.Visible;
                                                }
                                                catch (Exception ex)
                                                {
                                                    var str = ex.StackTrace;
                                                    Console.WriteLine(str);
                                                    // 异常时显示提示文字
                                                    this.StatusTextBlock.Text = "仪表型号解析出错！";
                                                    this.TurnOnButton.IsChecked = false;
                                                    return;
                                                }

                                                // 系列号
                                                try
                                                {
                                                    this.ResFirmwareVersion.Text = this.FrameContent.Substring(54, 47);
                                                    this.ResFirmwareVersionDockPanel.Visibility = Visibility.Visible;
                                                }
                                                catch (Exception ex)
                                                {
                                                    var str = ex.StackTrace;
                                                    Console.WriteLine(str);
                                                    // 异常时显示提示文字
                                                    this.StatusTextBlock.Text = "系列号解析出错！";
                                                    this.TurnOnButton.IsChecked = false;
                                                    return;
                                                }

                                                // 固件版本
                                                try
                                                {
                                                    this.ResFirmwareVersion.Text = this.FrameContent.Substring(102, 5);
                                                    this.ResFirmwareVersionDockPanel.Visibility = Visibility.Visible;
                                                }
                                                catch (Exception ex)
                                                {
                                                    var str = ex.StackTrace;
                                                    Console.WriteLine(str);
                                                    // 异常时显示提示文字
                                                    this.StatusTextBlock.Text = "固件版本解析出错！";
                                                    this.TurnOnButton.IsChecked = false;
                                                    return;
                                                }

                                                // 软件版本
                                                try
                                                {
                                                    this.ResSoftwareVersion.Text = this.FrameContent.Substring(108, 5);
                                                    this.ResSoftwareVersionDockPanel.Visibility = Visibility.Visible;
                                                }
                                                catch (Exception ex)
                                                {
                                                    var str = ex.StackTrace;
                                                    Console.WriteLine(str);
                                                    // 异常时显示提示文字
                                                    this.StatusTextBlock.Text = "软件版本解析出错！";
                                                    this.TurnOnButton.IsChecked = false;
                                                    return;
                                                }

                                                // 量程下限
                                                try
                                                {
                                                    this.ResLowRange.Text = this.FrameContent.Substring(114, 5);
                                                    this.ResLowRangeDockPanel.Visibility = Visibility.Visible;
                                                }
                                                catch (Exception ex)
                                                {
                                                    var str = ex.StackTrace;
                                                    Console.WriteLine(str);
                                                    // 异常时显示提示文字
                                                    this.StatusTextBlock.Text = "量程下限解析出错！";
                                                    this.TurnOnButton.IsChecked = false;
                                                    return;
                                                }

                                                // 量程上限
                                                try
                                                {
                                                    this.ResHighRange.Text = this.FrameContent.Substring(120, 5);
                                                    this.ResHighRangeDockPanel.Visibility = Visibility.Visible;
                                                }
                                                catch (Exception ex)
                                                {
                                                    var str = ex.StackTrace;
                                                    Console.WriteLine(str);
                                                    // 异常时显示提示文字
                                                    this.StatusTextBlock.Text = "量程上限解析出错！";
                                                    this.TurnOnButton.IsChecked = false;
                                                    return;
                                                }

                                                // 测量精度
                                                try
                                                {
                                                    this.ResMeasurementAccuracy.Text = this.FrameContent.Substring(126, 5);
                                                    this.ResMeasurementAccuracyDockPanel.Visibility = Visibility.Visible;
                                                }
                                                catch (Exception ex)
                                                {
                                                    var str = ex.StackTrace;
                                                    Console.WriteLine(str);
                                                    // 异常时显示提示文字
                                                    this.StatusTextBlock.Text = "测量精度解析出错！";
                                                    this.TurnOnButton.IsChecked = false;
                                                    return;
                                                }

                                                // 防护等级
                                                try
                                                {
                                                    this.ResProtectionLevel.Text = this.FrameContent.Substring(132, 23);
                                                    this.ResProtectionLevelDockPanel.Visibility = Visibility.Visible;
                                                }
                                                catch (Exception ex)
                                                {
                                                    var str = ex.StackTrace;
                                                    Console.WriteLine(str);
                                                    // 异常时显示提示文字
                                                    this.StatusTextBlock.Text = "防护等级解析出错！";
                                                    this.TurnOnButton.IsChecked = false;
                                                    return;
                                                }

                                                // 防爆等级
                                                try
                                                {
                                                    this.ResExplosionProofGrade.Text = this.FrameContent.Substring(156, 35);
                                                    this.ResExplosionProofGradeDockPanel.Visibility = Visibility.Visible;
                                                }
                                                catch (Exception ex)
                                                {
                                                    var str = ex.StackTrace;
                                                    Console.WriteLine(str);
                                                    // 异常时显示提示文字
                                                    this.StatusTextBlock.Text = "防爆等级解析出错！";
                                                    this.TurnOnButton.IsChecked = false;
                                                    return;
                                                }

                                                // 说明
                                                try
                                                {
                                                    this.ResIllustrate.Text = this.FrameContent.Substring(191, 29);
                                                    this.ResIllustrateDockPanel.Visibility = Visibility.Visible;
                                                }
                                                catch (Exception ex)
                                                {
                                                    var str = ex.StackTrace;
                                                    Console.WriteLine(str);
                                                    // 异常时显示提示文字
                                                    this.StatusTextBlock.Text = "说明解析出错！";
                                                    this.TurnOnButton.IsChecked = false;
                                                    return;
                                                }
                                            }

                                            break;
                                        case 0x0020:
                                            this.ResFunctionData.Text = "读数据命令";
                                            break;
                                        case 0x0100:
                                            this.ResFunctionData.Text = "控制器参数写应答（控制器应答命令）";
                                            break;
                                        case 0x0101:
                                            this.ResFunctionData.Text = "控制器读仪表参数应答（控制器应答命令）";
                                            break;
                                        case 0x0200:
                                            this.ResFunctionData.Text = "控制器应答一体化载荷位移示功仪功图参数应答（控制器应答命令触发功图采集）";
                                            break;
                                        case 0x0201:
                                            this.ResFunctionData.Text = "控制器应答功图数据命令";
                                            break;
                                        case 0x0202:
                                            this.ResFunctionData.Text = "控制器读功图数据应答（控制器应答命令读已有功图）";
                                            break;
                                        case 0x0300:
                                            this.ResFunctionData.Text = "控制器(RTU)对仪表控制命令";
                                            break;
                                        default:
                                            this.ResType.Clear();
                                            break;
                                    }

                                    while (this.ResType.Text.Trim() == string.Empty)
                                    {
                                        if (intFrameContentFunctionData >= 0x400 &&
                                            intFrameContentFunctionData <= 0x47f)
                                        {
                                            this.ResFunctionData.Text = "配置协议命令";
                                        }
                                        else if (intFrameContentFunctionData >= 0x480 &&
                                                 intFrameContentFunctionData <= 0x5ff)
                                        {
                                            this.ResFunctionData.Text = "标定协议命令";
                                        }
                                        else if (intFrameContentFunctionData >= 0x1000 &&
                                                 intFrameContentFunctionData <= 0x2000)
                                        {
                                            this.ResFunctionData.Text = "厂家自定义数据类型";
                                        }
                                        else if (intFrameContentFunctionData >= 0x8000 &&
                                                 intFrameContentFunctionData <= 0xffff)
                                        {
                                            this.ResFunctionData.Text = "预留";
                                        }
                                        else
                                        {
                                            this.ResFunctionData.Text = "未定义";
                                            this.ResFunctionData.Foreground = new SolidColorBrush(Colors.Red);
                                            break;
                                        }
                                    }

                                    this.ResTypeDockPanel.Visibility = Visibility.Visible;
                                }
                                catch (Exception ex)
                                {
                                    var str = ex.StackTrace;
                                    Console.WriteLine(str);
                                    // 异常时显示提示文字
                                    this.StatusTextBlock.Text = "数据类型解析出错！";
                                    this.TurnOnButton.IsChecked = false;
                                    return;
                                }
                            }
                            //}
                            //else
                            //{
                            //    this.ResCyclicRedundancyCheck.Text = "未通过";
                            //    this.ResCyclicRedundancyCheck.Foreground = new SolidColorBrush(Colors.Red);
                            //}

                            this.ResCyclicRedundancyCheckDockPanel.Visibility = Visibility.Visible;
                        }
                        break;
                    case "7E":
                        {
                            ////字符串校验(已移动到断帧部分)
                            ////string calCheckCode = this.CalCheckCode_7E(txt);
                            ////if (calCheckCode == this.FrameCyclicRedundancyCheck)
                            //if (true)
                            //{
                            this.ResCyclicRedundancyCheck.Text = "通过";
                            // 校验成功写入其他解析参数
                            // 无线仪表数据域帧头
                            {
                                // 通信协议
                                try
                                {
                                    this.ResProtocol.Text = this.IsLoRaFlag ? "LoRa（Semtech）" : "ZigBee（Digi International）";
                                    this.ResProtocolDockPanel.Visibility = Visibility.Visible;
                                }
                                catch (Exception ex)
                                {
                                    var str = ex.StackTrace;
                                    Console.WriteLine(str);
                                    // 异常时显示提示文字
                                    this.StatusTextBlock.Text = "通信协议解析出错！";
                                    this.TurnOnButton.IsChecked = false;
                                    return;
                                }

                                // 网络地址
                                try
                                {
                                    if (this.IsLoRaFlag)
                                    {
                                        this.ResAddress.Text = "透传模式";
                                        this.ResAddressDockPanel.Visibility = Visibility.Visible;
                                    }
                                    else
                                    {
                                        var frameContentAddress =
                                            (this.FrameAddress.Substring(6, 2) + this.FrameAddress.Substring(3, 2)).Replace(" ",
                                                "");
                                        var intFrameContentAddress = Convert.ToInt32(frameContentAddress, 16);
                                        this.ResAddress.Text = intFrameContentAddress.ToString();
                                        this.ResAddressDockPanel.Visibility = Visibility.Visible;
                                    }
                                }
                                catch (Exception ex)
                                {
                                    var str = ex.StackTrace;
                                    Console.WriteLine(str);
                                    // 异常时显示提示文字
                                    this.StatusTextBlock.Text = "网络地址解析出错！";
                                    this.TurnOnButton.IsChecked = false;
                                    return;
                                }

                                // 厂商号
                                try
                                {
                                    var frameContentVendor = this.FrameContent.Substring(6, 5).Replace(" ", "");
                                    var intFrameContentVendor = Convert.ToInt32(frameContentVendor, 16);
                                    // 1 0x0001 厂商1
                                    // 2 0x0002 厂商2
                                    // 3 0x0003 厂商3
                                    // 4 ......
                                    // N 0x8001~0xFFFF 预留
                                    if (intFrameContentVendor > 0x0000 && intFrameContentVendor < 0x8001)
                                    {
                                        this.ResVendor.Text = "厂商" + intFrameContentVendor;
                                    }
                                    else if (intFrameContentVendor >= 0x8001 && intFrameContentVendor <= 0xFFFF)
                                    {
                                        this.ResVendor.Text = "预留厂商";
                                    }
                                    else
                                    {
                                        this.ResVendor.Text = "未定义";
                                        this.ResVendor.Foreground = new SolidColorBrush(Colors.Red);
                                    }

                                    this.ResVendorDockPanel.Visibility = Visibility.Visible;
                                }
                                catch (Exception ex)
                                {
                                    var str = ex.StackTrace;
                                    Console.WriteLine(str);
                                    // 异常时显示提示文字
                                    this.StatusTextBlock.Text = "厂商号解析出错！";
                                    this.TurnOnButton.IsChecked = false;
                                    return;
                                }

                                // 仪表类型
                                try
                                {
                                    var frameContentType = this.FrameContent.Substring(12, 5).Replace(" ", "");
                                    var intFrameContentType = Convert.ToInt32(frameContentType, 16);
                                    // 1  0x0001 无线一体化负荷
                                    // 2  0x0002 无线压力
                                    // 3  0x0003 无线温度
                                    // 4  0x0004 无线电量
                                    // 5  0x0005 无线角位移
                                    // 6  0x0006 无线载荷
                                    // 7  0x0007 无线扭矩
                                    // 8  0x0008 无线动液面
                                    // 9  0x0009 计量车
                                    //    0x000B 无线压力温度一体化变送器
                                    //    ......
                                    // 10 0x1f00 控制器(RTU)设备
                                    // 11 0x1f10 手操器
                                    // 12 ......
                                    // N  0x2000~0x4000 自定义
                                    //    0x2000 无线死点开关
                                    //    0x3000 无线拉线位移校准传感器
                                    //    0x3001 无线拉线位移功图校准传感器
                                    switch (intFrameContentType)
                                    {
                                        case 0x0001:
                                            this.ResType.Text = "无线一体化负荷";
                                            break;
                                        case 0x0002:
                                            this.ResType.Text = "无线压力";
                                            break;
                                        case 0x0003:
                                            this.ResType.Text = "无线温度";
                                            break;
                                        case 0x0004:
                                            this.ResType.Text = "无线电量";
                                            break;
                                        case 0x0005:
                                            this.ResType.Text = "无线角位移";
                                            break;
                                        case 0x0006:
                                            this.ResType.Text = "无线载荷";
                                            break;
                                        case 0x0007:
                                            this.ResType.Text = "无线扭矩";
                                            break;
                                        case 0x0008:
                                            this.ResType.Text = "无线动液面";
                                            break;
                                        case 0x0009:
                                            this.ResType.Text = "计量车";
                                            break;
                                        case 0x000B:
                                            this.ResType.Text = "无线压力温度一体化变送器";
                                            break;
                                        case 0x1F00:
                                            this.ResType.Text = "控制器(RTU)设备";
                                            break;
                                        case 0x1F10:
                                            this.ResType.Text = "手操器";
                                            break;
                                        // 自定义
                                        case 0x2000:
                                            this.ResType.Text = "温度型";
                                            break;
                                        case 0x3000:
                                            this.ResType.Text = "无线拉线位移校准传感器";
                                            break;
                                        case 0x3001:
                                            this.ResType.Text = "无线拉线位移功图校准传感器";
                                            break;
                                        default:
                                            this.ResType.Clear();
                                            break;
                                    }

                                    while (this.ResType.Text.Trim() == string.Empty)
                                    {
                                        if (intFrameContentType <= 0x4000 && intFrameContentType >= 0x3000)
                                        {
                                            this.ResType.Text = "自定义";
                                        }
                                        else
                                        {
                                            this.ResType.Text = "未定义";
                                            this.ResType.Foreground = new SolidColorBrush(Colors.Red);
                                        }
                                    }

                                    this.ResTypeDockPanel.Visibility = Visibility.Visible;
                                }
                                catch (Exception ex)
                                {
                                    var str = ex.StackTrace;
                                    Console.WriteLine(str);
                                    // 异常时显示提示文字
                                    this.StatusTextBlock.Text = "仪表类型解析出错！";
                                    this.TurnOnButton.IsChecked = false;
                                    return;
                                }

                                // 仪表组号
                                try
                                {
                                    this.ResGroup.Text =
                                        Convert.ToInt32(this.FrameContent.Substring(18, 2).Replace(" ", ""), 16) + "组" +
                                        Convert.ToInt32(this.FrameContent.Substring(21, 2).Replace(" ", ""), 16) + "号";
                                    this.ResGroupDockPanel.Visibility = Visibility.Visible;
                                }
                                catch (Exception ex)
                                {
                                    var str = ex.StackTrace;
                                    Console.WriteLine(str);
                                    // 异常时显示提示文字
                                    this.StatusTextBlock.Text = "仪表组号解析出错！";
                                    this.TurnOnButton.IsChecked = false;
                                    return;
                                }

                                // 根据不同数据类型解析其他数据
                                try
                                {
                                    var frameContentFunctionData = this.FrameContent.Substring(24, 5).Replace(" ", "");
                                    var intFrameContentFunctionData = Convert.ToInt32(frameContentFunctionData, 16);
                                    // 1  0x0000 常规数据
                                    // 2  ……
                                    // 3  0x0010 仪表参数
                                    // 4  ……
                                    // 5  0x0020 读数据命令
                                    // 6 
                                    // 7  ……
                                    // 8 
                                    // 9 
                                    // 10 ……
                                    // 11 
                                    // 12 ……
                                    // 13 0x0100 控制器参数写应答（控制器应答命令）
                                    // 14 0x0101 控制器读仪表参数应答（控制器应答命令）
                                    // 15 ……
                                    // 16 0x0200 控制器应答一体化载荷位移示功仪功图参数应答（控制器应答命令触发功图采集）
                                    // 17 0x0201 控制器应答功图数据命令
                                    // 18 0x0202 控制器读功图数据应答（控制器应答命令读已有功图）
                                    // 19 ……
                                    // 20 0x0300 控制器(RTU)对仪表控制命令
                                    // 21 0x400~0x47f 配置协议命令
                                    // 22 0x480~0x5ff 标定协议命令
                                    // 23 0x1000~0x2000 厂家自定义数据类型
                                    // 24 ……
                                    // 25 0x8000－0xffff 预留
                                    switch (intFrameContentFunctionData)
                                    {
                                        case 0x0000:
                                            this.ResFunctionData.Text = "常规数据（仪表实时数据）";
                                            if ((txt.Length + 1) / 3 == 42 ||
                                                (txt.Length + 1) / 3 == 24)
                                            {
                                                // 无线仪表数据段
                                                // 通信效率
                                                try
                                                {
                                                    this.ResSucRate.Text =
                                                        Convert.ToInt32(this.FrameContent.Substring(30, 2), 16) + "%";
                                                    this.ResSucRateDockPanel.Visibility = Visibility.Visible;
                                                }
                                                catch (Exception ex)
                                                {
                                                    var str = ex.StackTrace;
                                                    Console.WriteLine(str);
                                                    // 异常时显示提示文字
                                                    this.StatusTextBlock.Text = "通信效率解析出错！";
                                                    this.TurnOnButton.IsChecked = false;
                                                    return;
                                                }

                                                // 电池电压
                                                try
                                                {
                                                    this.ResBatVol.Text =
                                                        Convert.ToInt32(this.FrameContent.Substring(33, 2), 16) + "%";
                                                    this.ResBatVolDockPanel.Visibility = Visibility.Visible;
                                                }
                                                catch (Exception ex)
                                                {
                                                    var str = ex.StackTrace;
                                                    Console.WriteLine(str);
                                                    // 异常时显示提示文字
                                                    this.StatusTextBlock.Text = "电池电压解析出错！";
                                                    this.TurnOnButton.IsChecked = false;
                                                    return;
                                                }

                                                // 休眠时间
                                                try
                                                {
                                                    var sleepTime =
                                                        Convert.ToInt32(this.FrameContent.Substring(36, 5).Replace(" ", ""),
                                                            16);
                                                    this.ResSleepTime.Text = sleepTime + "秒";
                                                    this.ResSleepTimeDockPanel.Visibility = Visibility.Visible;
                                                    if (this.RegularDataUpdateRate.Text == "")
                                                    {
                                                        this.RegularDataUpdateRate.Text = Convert.ToString(sleepTime);
                                                    }
                                                }
                                                catch (Exception ex)
                                                {
                                                    var str = ex.StackTrace;
                                                    Console.WriteLine(str);
                                                    // 异常时显示提示文字
                                                    this.StatusTextBlock.Text = "休眠时间解析出错！";
                                                    this.TurnOnButton.IsChecked = false;
                                                    return;
                                                }

                                                // 仪表状态
                                                try
                                                {
                                                    var frameStatue = this.FrameContent.Substring(42, 5).Replace(" ", "");
                                                    var binFrameStatue = Convert
                                                        .ToString(Convert.ToInt32(frameStatue, 16), 2).PadLeft(8, '0');
                                                    if (Convert.ToInt32(binFrameStatue.Replace(" ", ""), 2) != 0)
                                                    {
                                                        this.ResStatue.Text = "故障";
                                                        var failureMessage = "";
                                                        var count = 0;
                                                        // 1 Bit0 仪表故障
                                                        // 2 Bit1 参数错误
                                                        // 3 Bit2 电池欠压，日月协议中仍然保留
                                                        // 4 Bit3 AI1 上限报警
                                                        // 5 Bit4 AI1 下限报警
                                                        // 6 Bit5 AI2 上限报警
                                                        // 7 Bit6 AI2 下限报警
                                                        // 8 Bit7 预留
                                                        for (var a = 0; a < 8; a++)
                                                        {
                                                            // 从第0位到第7位
                                                            if (binFrameStatue.Substring(a, 1) == "1")
                                                            {
                                                                switch (a)
                                                                {
                                                                    case 0:
                                                                        failureMessage += ++count + " 仪表故障\n";
                                                                        break;
                                                                    case 1:
                                                                        failureMessage += ++count + " 参数故障\n";
                                                                        break;
                                                                    case 2:
                                                                        failureMessage += ++count + " 电池欠压\n";
                                                                        break;
                                                                    case 3:
                                                                        failureMessage += ++count + " 压力上限报警\n";
                                                                        break;
                                                                    case 4:
                                                                        failureMessage += ++count + " 压力下限报警\n";
                                                                        break;
                                                                    case 5:
                                                                        failureMessage += ++count + " 温度上限报警\n";
                                                                        break;
                                                                    case 6:
                                                                        failureMessage += ++count + " 温度下限报警\n";
                                                                        break;
                                                                    case 7:
                                                                        failureMessage += ++count + " 未定义故障\n";
                                                                        break;
                                                                    default:
                                                                        failureMessage += "参数错误\n";
                                                                        break;
                                                                }
                                                            }
                                                        }

                                                        this.StatusTextBlock.Text = failureMessage;
                                                        //string messageBoxText = "设备上报" + count + "个故障: \n" + failureMessage;
                                                        //string caption = "设备故障";
                                                        //MessageBoxButton button = MessageBoxButton.OK;
                                                        //MessageBoxImage icon = MessageBoxImage.Error;
                                                        //MessageBox.Show(messageBoxText, caption, button, icon);
                                                    }
                                                    else
                                                    {
                                                        this.ResStatue.Text = "正常";
                                                    }

                                                    this.ResStatueDockPanel.Visibility = Visibility.Visible;
                                                }
                                                catch (Exception ex)
                                                {
                                                    var str = ex.StackTrace;
                                                    Console.WriteLine(str);
                                                    // 异常时显示提示文字
                                                    this.StatusTextBlock.Text = "仪表状态解析出错！";
                                                    this.TurnOnButton.IsChecked = false;
                                                    return;
                                                }

                                                // 运行时间
                                                try
                                                {
                                                    this.ResTime.Text =
                                                        Convert.ToInt32(this.FrameContent.Substring(48, 5).Replace(" ", ""),
                                                            16) + "小时";
                                                    this.ResTimeDockPanel.Visibility = Visibility.Visible;
                                                }
                                                catch (Exception ex)
                                                {
                                                    var str = ex.StackTrace;
                                                    Console.WriteLine(str);
                                                    // 异常时显示提示文字
                                                    this.StatusTextBlock.Text = "运行时间解析出错！";
                                                    this.TurnOnButton.IsChecked = false;
                                                    return;
                                                }

                                                // 实时数据
                                                try
                                                {
                                                    //string frameResData = frameContent.Substring(54, 5).Replace(" ", "").TrimStart('0');
                                                    //resData.Text = Convert.ToInt32(frameResData, 16) + "MPa";
                                                    // 十六进制字符串转换为浮点数字符串
                                                    var frameResData =
                                                        this.FrameContent.Substring(48, 11).Replace(" ", "");
                                                    var flFrameData = this.HexStrToFloat(frameResData);
                                                    this.RealTimeData = flFrameData;
                                                    // 单位类型
                                                    //var frameContentType =
                                                    //    frameContent.Substring(12, 5).Replace(" ", "");
                                                    //var type = "";
                                                    //switch (intFrameContentType)
                                                    //{
                                                    //    case 0x0002: type = "Pa"; break;
                                                    //    case 0x0003: type = "℃"; break;
                                                    //    default: type = ""; break;
                                                    //}
                                                    this.ResData.Text = flFrameData.ToString(CultureInfo.InvariantCulture);
                                                    this.ResDataDockPanel.Visibility = Visibility.Visible;
                                                    // 作图
                                                    this.AnimatedPlot();
                                                    // 写数据库
                                                    if (this.SqlConnectButton.IsChecked == true && this.AutoSaveToSqlCheckBox.IsChecked.Value)
                                                    {
                                                        string type = this.ResType.Text, address = this.ResAddress.Text, protocol = this.ResProtocol.Text, data = this.ResData.Text, statue = this.ResStatue.Text, workSheet = WorkSheet;
                                                        var sql = this.CalibrationSqlConnect;
                                                        this.DatabaseWrite(type, protocol, address, data, statue, workSheet, sql);
                                                    }
                                                }
                                                catch (Exception ex)
                                                {
                                                    var str = ex.StackTrace;
                                                    Console.WriteLine(str);
                                                    // 异常时显示提示文字
                                                    this.StatusTextBlock.Text = "实时数据解析出错！";
                                                    this.TurnOnButton.IsChecked = false;
                                                    return;
                                                }
                                            }

                                            break;
                                        case 0x0010:
                                            this.ResFunctionData.Text = "常规数据（仪表基本参数）";
                                            if ((txt.Length + 1) / 3 == 96 ||
                                                (txt.Length + 1) / 3 == 78)
                                            {
                                                // 无线仪表数据段
                                                // 仪表型号
                                                try
                                                {
                                                    this.ResModel.Text = this.FrameContent.Substring(30, 23);
                                                    this.ResModelDockPanel.Visibility = Visibility.Visible;
                                                }
                                                catch (Exception ex)
                                                {
                                                    var str = ex.StackTrace;
                                                    Console.WriteLine(str);
                                                    // 异常时显示提示文字
                                                    this.StatusTextBlock.Text = "仪表型号解析出错！";
                                                    this.TurnOnButton.IsChecked = false;
                                                    return;
                                                }

                                                // 系列号
                                                try
                                                {
                                                    this.ResFirmwareVersion.Text = this.FrameContent.Substring(54, 47);
                                                    this.ResFirmwareVersionDockPanel.Visibility = Visibility.Visible;
                                                }
                                                catch (Exception ex)
                                                {
                                                    var str = ex.StackTrace;
                                                    Console.WriteLine(str);
                                                    // 异常时显示提示文字
                                                    this.StatusTextBlock.Text = "系列号解析出错！";
                                                    this.TurnOnButton.IsChecked = false;
                                                    return;
                                                }

                                                // 固件版本
                                                try
                                                {
                                                    this.ResFirmwareVersion.Text = this.FrameContent.Substring(102, 5);
                                                    this.ResFirmwareVersionDockPanel.Visibility = Visibility.Visible;
                                                }
                                                catch (Exception ex)
                                                {
                                                    var str = ex.StackTrace;
                                                    Console.WriteLine(str);
                                                    // 异常时显示提示文字
                                                    this.StatusTextBlock.Text = "固件版本解析出错！";
                                                    this.TurnOnButton.IsChecked = false;
                                                    return;
                                                }

                                                // 软件版本
                                                try
                                                {
                                                    this.ResSoftwareVersion.Text = this.FrameContent.Substring(108, 5);
                                                    this.ResSoftwareVersionDockPanel.Visibility = Visibility.Visible;
                                                }
                                                catch (Exception ex)
                                                {
                                                    var str = ex.StackTrace;
                                                    Console.WriteLine(str);
                                                    // 异常时显示提示文字
                                                    this.StatusTextBlock.Text = "软件版本解析出错！";
                                                    this.TurnOnButton.IsChecked = false;
                                                    return;
                                                }

                                                // 量程下限
                                                try
                                                {
                                                    this.ResLowRange.Text = this.FrameContent.Substring(114, 5);
                                                    this.ResLowRangeDockPanel.Visibility = Visibility.Visible;
                                                }
                                                catch (Exception ex)
                                                {
                                                    var str = ex.StackTrace;
                                                    Console.WriteLine(str);
                                                    // 异常时显示提示文字
                                                    this.StatusTextBlock.Text = "量程下限解析出错！";
                                                    this.TurnOnButton.IsChecked = false;
                                                    return;
                                                }

                                                // 量程上限
                                                try
                                                {
                                                    this.ResHighRange.Text = this.FrameContent.Substring(120, 5);
                                                    this.ResHighRangeDockPanel.Visibility = Visibility.Visible;
                                                }
                                                catch (Exception ex)
                                                {
                                                    var str = ex.StackTrace;
                                                    Console.WriteLine(str);
                                                    // 异常时显示提示文字
                                                    this.StatusTextBlock.Text = "量程上限解析出错！";
                                                    this.TurnOnButton.IsChecked = false;
                                                    return;
                                                }

                                                // 测量精度
                                                try
                                                {
                                                    this.ResMeasurementAccuracy.Text = this.FrameContent.Substring(126, 5);
                                                    this.ResMeasurementAccuracyDockPanel.Visibility = Visibility.Visible;
                                                }
                                                catch (Exception ex)
                                                {
                                                    var str = ex.StackTrace;
                                                    Console.WriteLine(str);
                                                    // 异常时显示提示文字
                                                    this.StatusTextBlock.Text = "测量精度解析出错！";
                                                    this.TurnOnButton.IsChecked = false;
                                                    return;
                                                }

                                                // 防护等级
                                                try
                                                {
                                                    this.ResProtectionLevel.Text = this.FrameContent.Substring(132, 23);
                                                    this.ResProtectionLevelDockPanel.Visibility = Visibility.Visible;
                                                }
                                                catch (Exception ex)
                                                {
                                                    var str = ex.StackTrace;
                                                    Console.WriteLine(str);
                                                    // 异常时显示提示文字
                                                    this.StatusTextBlock.Text = "防护等级解析出错！";
                                                    this.TurnOnButton.IsChecked = false;
                                                    return;
                                                }

                                                // 防爆等级
                                                try
                                                {
                                                    this.ResExplosionProofGrade.Text = this.FrameContent.Substring(156, 35);
                                                    this.ResExplosionProofGradeDockPanel.Visibility = Visibility.Visible;
                                                }
                                                catch (Exception ex)
                                                {
                                                    var str = ex.StackTrace;
                                                    Console.WriteLine(str);
                                                    // 异常时显示提示文字
                                                    this.StatusTextBlock.Text = "防爆等级解析出错！";
                                                    this.TurnOnButton.IsChecked = false;
                                                    return;
                                                }

                                                // 说明
                                                try
                                                {
                                                    this.ResIllustrate.Text = this.FrameContent.Substring(191, 29);
                                                    this.ResIllustrateDockPanel.Visibility = Visibility.Visible;
                                                }
                                                catch (Exception ex)
                                                {
                                                    var str = ex.StackTrace;
                                                    Console.WriteLine(str);
                                                    // 异常时显示提示文字
                                                    this.StatusTextBlock.Text = "说明解析出错！";
                                                    this.TurnOnButton.IsChecked = false;
                                                    return;
                                                }
                                            }

                                            break;
                                        case 0x0020:
                                            this.ResFunctionData.Text = "读数据命令";
                                            break;
                                        case 0x0100:
                                            this.ResFunctionData.Text = "控制器参数写应答（控制器应答命令）";
                                            break;
                                        case 0x0101:
                                            this.ResFunctionData.Text = "控制器读仪表参数应答（控制器应答命令）";
                                            break;
                                        case 0x0200:
                                            this.ResFunctionData.Text = "控制器应答一体化载荷位移示功仪功图参数应答（控制器应答命令触发功图采集）";
                                            break;
                                        case 0x0201:
                                            this.ResFunctionData.Text = "控制器应答功图数据命令";
                                            break;
                                        case 0x0202:
                                            this.ResFunctionData.Text = "控制器读功图数据应答（控制器应答命令读已有功图）";
                                            break;
                                        case 0x0300:
                                            this.ResFunctionData.Text = "控制器(RTU)对仪表控制命令";
                                            break;
                                        default:
                                            this.ResType.Clear();
                                            break;
                                    }

                                    while (this.ResType.Text.Trim() == string.Empty)
                                    {
                                        if (intFrameContentFunctionData >= 0x400 &&
                                            intFrameContentFunctionData <= 0x47f)
                                        {
                                            this.ResFunctionData.Text = "配置协议命令";
                                        }
                                        else if (intFrameContentFunctionData >= 0x480 &&
                                                 intFrameContentFunctionData <= 0x5ff)
                                        {
                                            this.ResFunctionData.Text = "标定协议命令";
                                        }
                                        else if (intFrameContentFunctionData >= 0x1000 &&
                                                 intFrameContentFunctionData <= 0x2000)
                                        {
                                            this.ResFunctionData.Text = "厂家自定义数据类型";
                                        }
                                        else if (intFrameContentFunctionData >= 0x8000 &&
                                                 intFrameContentFunctionData <= 0xffff)
                                        {
                                            this.ResFunctionData.Text = "预留";
                                        }
                                        else
                                        {
                                            this.ResFunctionData.Text = "未定义";
                                            this.ResFunctionData.Foreground = new SolidColorBrush(Colors.Red);
                                            break;
                                        }
                                    }

                                    this.ResFunctionDataDockPanel.Visibility = Visibility.Visible;
                                }
                                catch (Exception ex)
                                {
                                    var str = ex.StackTrace;
                                    Console.WriteLine(str);
                                    // 异常时显示提示文字
                                    this.StatusTextBlock.Text = "数据类型解析出错！";
                                    this.TurnOnButton.IsChecked = false;
                                    return;
                                }
                            }
                            //}
                            //else
                            //{
                            //    this.ResCyclicRedundancyCheck.Text = "未通过";
                            //    this.ResCyclicRedundancyCheck.Foreground = new SolidColorBrush(Colors.Red);
                            //}

                            this.ResCyclicRedundancyCheckDockPanel.Visibility = Visibility.Visible;
                        }
                        break;
                    default:
                        this.ResProtocol.Text = "未知";
                        this.ResAddress.Foreground = new SolidColorBrush(Colors.Red);
                        break;
                }
            }
            catch (Exception ex)
            {
                var str = ex.StackTrace;
                Console.WriteLine(str);
                // 异常时显示提示文字
                this.StatusTextBlock.Text = "参数解析出错！";
                this.TurnOnButton.IsChecked = false;
            }
            // 更新数据曲线
            //string strConn = @"Data Source=.;Initial Catalog=Test; integrated security=True;";
            //SqlConnection conn = new SqlConnection(strConn);
        }

        private string CalCheckCode_7E(string text)
        {
            var j = 0;
            var txt = text.Trim();
            var hexvalue = txt.Remove(0, 9).Remove(txt.Length - 12).Split(' ');
            // 0xFF - 字符串求和
            try
            {
                j = hexvalue.Aggregate(j, (current, hex) => current + Convert.ToInt32(hex, 16));
                var calCheckCode =
                    (0xFF - Convert.ToInt32(j.ToString("X").Substring(j.ToString("X").Length - 2, 2), 16))
                    .ToString("X");
                return calCheckCode.ToUpper().PadLeft(2, '0');
            }
            catch (Exception ex)
            {
                var str = ex.StackTrace;
                Console.WriteLine(str);
                return "";
            }
        }

        private string CalCheckCode_FE(string text)
        {
            var j = "";
            var txt = text.Trim();
            var hexvalue = txt.Remove(0, 3).Remove(txt.Length - 6).Split(' ');
            // 求字符串异或值
            foreach (var hex in hexvalue)
            {
                j = this.HexStrXor(j, hex);
            }

            return j.ToUpper().PadLeft(2, '0');
        }

        /// <summary>
        ///     接收窗口清空按钮
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ClearReceiveButton_Click(object sender, RoutedEventArgs e) => this.DisplayTextBox.Clear();

        #endregion

        #region 串口数据发送/定时发送/窗口清空功能

        /// <summary>
        ///     在发送窗口中写入数据
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SendTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            // 将光标移至文字末尾
            this.SendTextBox.SelectionStart = this.SendTextBox.Text.Length;
            //MatchCollection hexadecimalCollection = Regex.Matches(e.Text, @"[(0(X|x))?\da-fA-F]");
            var hexadecimalCollection = Regex.Matches(e.Text, @"[\da-fA-F]");
            foreach (Match mat in hexadecimalCollection)
            {
                this.SendTextBox.AppendText(mat.Value);
            }

            // 每输入两个字符自动添加空格
            this.SendTextBox.Text = this.SendTextBox.Text.Replace(" ", "");
            this.SendTextBox.Text = string.Join(" ", Regex.Split(this.SendTextBox.Text, "(?<=\\G.{2})(?!$)"));
            this.SendTextBox.SelectionStart = this.SendTextBox.Text.Length;
            e.Handled = true;
        }

        /// <summary>
        ///     串口数据发送逻辑
        /// </summary>
        private void SerialPortSend()
        {
            //sendCount++;
            //Console.WriteLine("发送" + sendCount + "次");
            // 清空发送缓冲区
            try
            {
                this.NewSerialPort.DiscardOutBuffer();
            }
            catch (Exception ex)
            {
                var std = ex.StackTrace;
                Console.WriteLine(std);
            }

            if (!this.NewSerialPort.IsOpen)
            {
                this.StatusTextBlock.Text = "请先打开串口！";
                return;
            }

            // 去掉十六进制前缀
            var sendData = CleanHexString(this.SendTextBox.Text);

            // 十六进制数据发送
            try
            {
                // 分割字符串
                var strArray = sendData.Split(' ');
                // 写入数据缓冲区
                var sendBuffer = new byte[strArray.Length];
                var i = 0;
                foreach (var str in strArray)
                {
                    try
                    {
                        int j = Convert.ToInt16(str, 16);
                        sendBuffer[i] = Convert.ToByte(j);
                        i++;
                    }
                    catch (Exception ex)
                    {
                        var std = ex.StackTrace;
                        Console.WriteLine(std);
                        this.NewSerialPort.DiscardOutBuffer();
                        MessageBox.Show("字节越界，请逐个字节输入！", "Error");
                        this.AutoSendCheckBox.IsChecked = false; // 关闭自动发送
                    }
                }

                //foreach (byte b in sendBuffer)
                //{
                //    Console.Write(b.ToString("X2"));
                //}
                //Console.WriteLine("");
                try
                {
                    this.NewSerialPort.Write(sendBuffer, 0, sendBuffer.Length);
                }
                catch (Exception ex)
                {
                    var str = ex.StackTrace;
                    Console.WriteLine(str);
                    this.StatusTextBlock.Text = "串口异常";
                }

                // 更新发送数据计数
                this.SendBytesCount += (uint)sendBuffer.Length;
                this.StatusSendByteTextBlock.Text = this.SendBytesCount.ToString();
            }
            catch (Exception ex)
            {
                var str = ex.StackTrace;
                Console.WriteLine(str);
                this.StatusTextBlock.Text = "当前为16进制发送模式，请输入16进制数据";
                this.AutoSendCheckBox.IsChecked = false; // 关闭自动发送
            }
        }

        private void SerialPortSend(string hexStr)
        {
            //sendCount++;
            //Console.WriteLine("发送" + sendCount + "次");
            // 清空发送缓冲区
            this.NewSerialPort.DiscardOutBuffer();
            if (!this.NewSerialPort.IsOpen)
            {
                this.StatusTextBlock.Text = "请先打开串口！";
                return;
            }

            hexStr = CleanHexString(hexStr);
            var sendData = hexStr;

            // 十六进制数据发送
            try
            {
                // 分割字符串
                var strArray = sendData.Split(' ');
                // 写入数据缓冲区
                var sendBuffer = new byte[strArray.Length];
                var i = 0;
                foreach (var str in strArray)
                {
                    try
                    {
                        int j = Convert.ToInt16(str, 16);
                        sendBuffer[i] = Convert.ToByte(j);
                        i++;
                    }
                    catch (Exception ex)
                    {
                        var std = ex.StackTrace;
                        Console.WriteLine(std);
                        this.NewSerialPort.DiscardOutBuffer();
                        MessageBox.Show("字节越界，请逐个字节输入！", "Error");
                        this.AutoSendCheckBox.IsChecked = false; // 关闭自动发送
                    }
                }

                //foreach (byte b in sendBuffer)
                //{
                //    Console.Write(b.ToString("X2"));
                //}
                //Console.WriteLine("");
                try
                {
                    this.NewSerialPort.Write(sendBuffer, 0, sendBuffer.Length);
                }
                catch (Exception ex)
                {
                    var str = ex.StackTrace;
                    Console.WriteLine(str);
                    this.StatusTextBlock.Text = "串口异常";
                }

                // 更新发送数据计数
                this.SendBytesCount += (uint)sendBuffer.Length;
                this.StatusSendByteTextBlock.Text = this.SendBytesCount.ToString();
            }
            catch (Exception ex)
            {
                var str = ex.StackTrace;
                Console.WriteLine(str);
                this.StatusTextBlock.Text = "当前为16进制发送模式，请输入16进制数据";
                this.AutoSendCheckBox.IsChecked = false; // 关闭自动发送
            }
        }

        private static string CleanHexString(string hexStr)
        {
            // 去掉十六进制前缀
            hexStr = hexStr.Replace("0x", "");
            hexStr = hexStr.Replace("0X", "");
            // 去掉多余空格
            hexStr = hexStr.Replace("  ", " ");
            hexStr = hexStr.Replace("  ", " ");
            hexStr = hexStr.Replace("  ", " ");
            return hexStr;
        }

        /// <summary>
        ///     手动单击按钮发送
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SendButton_Click(object sender, RoutedEventArgs e) => this.SerialPortSend();

        /// <summary>
        ///     自动发送开启
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void AutoSendCheckBox_Checked(object sender, RoutedEventArgs e) => this.AutoSendTimer.Start();

        /// <summary>
        ///     在每个自动发送周期执行
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void AutoSendTimer_Tick(object sender, EventArgs e)
        {
            this.AutoSendTimer.Interval = new TimeSpan(0, 0, 0, 0, Convert.ToInt32(this.AutoSendCycleTextBox.Text));
            // 发送数据
            this.SerialPortSend();
            // 设置新的定时时间           
            // autoSendTimer.Interval = new TimeSpan(0, 0, 0, 0, Convert.ToInt32(autoSendCycleTextBox.Text));
        }

        /// <summary>
        ///     自动发送关闭
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void AutoSendCheckBox_Unchecked(object sender, RoutedEventArgs e) => this.AutoSendTimer.Stop();

        /// <summary>
        ///     清空发送区
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ClearSendButton_Click(object sender, RoutedEventArgs e) => this.SendTextBox.Clear();

        /// <summary>
        ///     清空计数器
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CountClearButton_Click(object sender, RoutedEventArgs e)
        {
            // 接收、发送计数清零
            this.ReceiveBytesCount = 0;
            this.SendBytesCount = 0;
            // 更新数据显示
            this.StatusReceiveByteTextBlock.Text = this.ReceiveBytesCount.ToString();
            this.StatusSendByteTextBlock.Text = this.SendBytesCount.ToString();
        }

        #endregion

        #region 文件读取与保存 (文件I/O)

        ///// <summary>
        /////     读取文件
        ///// </summary>
        ///// <param name="sender"></param>
        ///// <param name="e"></param>
        //private void FileOpen(object sender, ExecutedRoutedEventArgs e)
        //{
        //    // 打开文件对话框 (默认选择serialCom.txt, 默认格式为文本文档)
        //    var openFile = new OpenFileDialog
        //    {
        //        FileName = "serialCom",
        //        DefaultExt = ".txt",
        //        Filter = "文本文档|*.txt"
        //    };
        //    // 如果用户单击确定(选好了文本文档文件)
        //    if (openFile.ShowDialog() == true)
        //    {
        //        // 将文本文档中所有文字读取到发送区
        //        sendTextBox.Text = File.ReadAllText(openFile.FileName, setEncoding);
        //        // 将文本文档的文件名读取到串口发送面板的文本框中
        //        fileNameTextBox.Text = openFile.FileName;
        //    }
        //}

        ///// <summary>
        /////     读取接收区并保存文件
        ///// </summary>
        ///// <param name="sender"></param>
        ///// <param name="e"></param>
        //private void FileSave(object sender, ExecutedRoutedEventArgs e)
        //{
        //    // 判断接收区是否有字段
        //    if (displayTextBox.Text == string.Empty)
        //    {
        //        // 如果没有字段，弹出失败提示
        //        statusTextBlock.Text = "接收区为空，保存失败。";
        //    }
        //    else
        //    {
        //        var saveFile = new SaveFileDialog
        //        {
        //            DefaultExt = ".txt",
        //            Filter = "文本文档|*.txt"
        //        };
        //        // 如果用户单击确定(确定了文本文档保存的位置和名称)
        //        if (saveFile.ShowDialog() == true)
        //        {
        //            // 在文本文档中写入当前时间
        //            File.AppendAllText(saveFile.FileName, "\r\n******" + DateTime.Now + "\r\n******");
        //            // 将接收区所有字段写入到文本文档
        //            File.AppendAllText(saveFile.FileName, displayTextBox.Text);
        //            // 弹出成功提示
        //            statusTextBlock.Text = "保存成功！";
        //        }
        //    }
        //}

        #endregion

        #region 上位机响应仪表方法

        /// <summary>
        ///     常规数据确认帧
        /// </summary>
        /// <returns></returns>
        private string RegularDataConfirmationFrame()
        {
            try
            {
                var str = "";
                switch (this.FrameHeader)
                {
                    case "FE":
                        {
                            // 获取所需解析数据
                            this.ParameterAcquisition_FE(out var strHeader, out var strCommand, out var strAddress,
                                out var strProtocolVendor, out var strHandler, out var strGroup);
                            // 功能码 / 数据类型
                            var strFunctionData = "01 00";
                            // 写操作数据区
                            string strHandlerContent;
                            if (this.RegularDataUpdateRate.Text != "")
                            {
                                strHandlerContent = Convert.ToString(Convert.ToInt32(this.RegularDataUpdateRate.Text), 16)
                                    .ToUpper().PadLeft(4, '0').Insert(2, " ");
                            }
                            else if (this.ResSleepTime.Text != "")
                            {
                                strHandlerContent = this.FrameContent.Substring(36, 5);
                            }
                            else
                            {
                                strHandlerContent = "00 00";
                            }

                            // 合成数据域
                            var strContent = strAddress + " " + strProtocolVendor + " " + strHandler + " " + strGroup +
                                             " " + strFunctionData + " " + strHandlerContent;
                            // 计算长度域（不包含命令域）
                            var intLength = (strContent.Length + 1) / 3;
                            var strLength = Convert.ToString(intLength, 16).ToUpper().PadLeft(2, '0');
                            var strInner = strLength + " " + strCommand + " " + strContent;
                            // 计算异或校验码
                            var strCyclicRedundancyCheck = this.CalCheckCode_FE(CleanHexString("00 " + strInner + " 00"));
                            // 合成返回值
                            str = CleanHexString(strHeader + " " + strInner + " " + strCyclicRedundancyCheck);
                        }
                        break;
                    case "7E":
                        {
                            if (this.IsLoRaFlag)
                            {
                                // 获取所需解析数据
                                this.ParameterAcquisition_7E(out var strHeader, out _, out _,
                                    out var strProtocolVendor, out var strHandler, out var strGroup);
                                // 功能码 / 数据类型
                                var strFunctionData = "01 00";
                                // 写操作数据区
                                string strHandlerContent;
                                if (this.RegularDataUpdateRate.Text != "")
                                {
                                    strHandlerContent = Convert.ToString(Convert.ToInt32(this.RegularDataUpdateRate.Text), 16)
                                        .ToUpper().PadLeft(4, '0').Insert(2, " ");
                                }
                                else if (this.ResSleepTime.Text != "")
                                {
                                    strHandlerContent = this.FrameContent.Substring(36, 5);
                                }
                                else
                                {
                                    strHandlerContent = "00 00";
                                }

                                // 合成数据域
                                var strContent = strProtocolVendor + " " + strHandler + " " + strGroup + " " +
                                                 strFunctionData + " " + strHandlerContent;
                                // 计算长度域（包含命令域）
                                var intLength = (strContent.Length + 1) / 3;
                                var strLength = Convert.ToString(intLength, 16).ToUpper().PadLeft(4, '0').Insert(2, " ");
                                var strInner = strLength + " " + strContent;
                                // 计算异或校验码
                                var strCyclicRedundancyCheck = this.CalCheckCode_7E(CleanHexString("00 " + strInner + " 00"));
                                // 合成返回值
                                str = CleanHexString(strHeader + " " + strInner + " " + strCyclicRedundancyCheck);
                            }
                            else
                            {
                                // 获取所需解析数据
                                this.ParameterAcquisition_7E(out var strHeader, out var strCommand, out var strAddress,
                                    out var strProtocolVendor, out var strHandler, out var strGroup);
                                // 功能码 / 数据类型
                                var strFunctionData = "01 00";
                                // 写操作数据区
                                string strHandlerContent;
                                if (this.RegularDataUpdateRate.Text != "")
                                {
                                    strHandlerContent = Convert.ToString(Convert.ToInt32(this.RegularDataUpdateRate.Text), 16)
                                        .ToUpper().PadLeft(4, '0').Insert(2, " ");
                                }
                                else if (this.ResSleepTime.Text != "")
                                {
                                    strHandlerContent = this.FrameContent.Substring(36, 5);
                                }
                                else
                                {
                                    strHandlerContent = "00 00";
                                }

                                // 合成数据域
                                var strContent = strCommand + " " + strAddress + " " +
                                                 this.FrameUnparsed.Remove(this.FrameUnparsed.Length - 3, 3) + " 00 00 " +
                                                 strProtocolVendor + " " + strHandler + " " + strGroup + " " +
                                                 strFunctionData + " " + strHandlerContent;
                                // 计算长度域（包含命令域）
                                var intLength = (strContent.Length + 1) / 3;
                                var strLength = Convert.ToString(intLength, 16).ToUpper().PadLeft(4, '0').Insert(2, " ");
                                var strInner = strLength + " " + strContent;
                                // 计算异或校验码
                                var strCyclicRedundancyCheck = this.CalCheckCode_7E(CleanHexString("00 " + strInner + " 00"));
                                // 合成返回值
                                str = CleanHexString(strHeader + " " + strInner + " " + strCyclicRedundancyCheck);
                            }
                        }
                        break;
                }

                return str;
            }
            catch (Exception ex)
            {
                var str = ex.StackTrace;
                Console.WriteLine(str);
                return "";
            }
        }

        /// <summary>
        ///     基本信息确认帧
        /// </summary>
        /// <returns></returns>
        private string BasicInformationConfirmationFrame()
        {
            try
            {
                var str = "";
                switch (this.FrameHeader)
                {
                    case "FE":
                        {
                            // 获取所需解析数据
                            this.ParameterAcquisition_FE(out var strHeader, out var strCommand, out var strAddress,
                                out var strProtocolVendor, out var strHandler, out var strGroup);
                            // 写操作数据区
                            // 功能码 / 数据类型
                            var strFunctionData = "01 01";
                            var strHandlerContent = "";
                            // 合成数据域
                            var strContent = strAddress + " " + strProtocolVendor + " " + strHandler + " " + strGroup +
                                             " " + strFunctionData + " " + strHandlerContent;
                            // 计算长度域（不包含命令域）
                            var intLength = (strContent.Length + 1) / 3;
                            var strLength = Convert.ToString(intLength, 16).ToUpper().PadLeft(2, '0');
                            var strInner = strLength + " " + strCommand + " " + strContent;
                            // 计算异或校验码
                            var strCyclicRedundancyCheck = this.CalCheckCode_FE(CleanHexString("00 " + strInner + " 00"));
                            // 合成返回值
                            str = CleanHexString(strHeader + " " + strInner + " " + strCyclicRedundancyCheck);
                        }
                        break;
                    case "7E":
                        {
                            if (this.IsLoRaFlag)
                            {
                                // 获取所需解析数据
                                this.ParameterAcquisition_7E(out var strHeader, out _, out _,
                                    out var strProtocolVendor, out var strHandler, out var strGroup);
                                // 写操作数据区
                                // 功能码 / 数据类型
                                var strFunctionData = "01 01";
                                var strHandlerContent = "";
                                // 合成数据域
                                var strContent = strProtocolVendor + " " + strHandler + " " + strGroup + " " +
                                                 strFunctionData + " " + strHandlerContent;
                                // 计算长度域（包含命令域）
                                var intLength = (strContent.Length + 1) / 3;
                                var strLength = Convert.ToString(intLength, 16).ToUpper().PadLeft(4, '0').Insert(2, " ");
                                var strInner = strLength + " " + strContent;
                                // 计算异或校验码
                                var strCyclicRedundancyCheck = this.CalCheckCode_7E(CleanHexString("00 " + strInner + " 00"));
                                // 合成返回值
                                str = CleanHexString(strHeader + " " + strInner + " " + strCyclicRedundancyCheck);
                            }
                            else
                            {
                                // 获取所需解析数据
                                this.ParameterAcquisition_7E(out var strHeader, out var strCommand, out var strAddress,
                                    out var strProtocolVendor, out var strHandler, out var strGroup);
                                // 写操作数据区
                                // 功能码 / 数据类型
                                var strFunctionData = "01 01";
                                var strHandlerContent = "";
                                // 合成数据域
                                var strContent = strCommand + " " + strAddress + " " +
                                                 this.FrameUnparsed.Remove(this.FrameUnparsed.Length - 3, 3) + " 00 00 " +
                                                 strProtocolVendor + " " + strHandler + " " + strGroup + " " +
                                                 strFunctionData + " " + strHandlerContent;
                                // 计算长度域（包含命令域）
                                var intLength = (strContent.Length + 1) / 3;
                                var strLength = Convert.ToString(intLength, 16).ToUpper().PadLeft(4, '0').Insert(2, " ");
                                var strInner = strLength + " " + strContent;
                                // 计算异或校验码
                                var strCyclicRedundancyCheck = this.CalCheckCode_7E(CleanHexString("00 " + strInner + " 00"));
                                // 合成返回值
                                str = CleanHexString(strHeader + " " + strInner + " " + strCyclicRedundancyCheck);
                            }
                        }
                        break;
                }

                return str;
            }

            catch (Exception ex)
            {
                var str = ex.StackTrace;
                Console.WriteLine(str);
                return "";
            }
        }

        /// <summary>
        ///     仪表连接处理程序
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void EstablishConnectionButton_Checked(object sender, RoutedEventArgs e)
        {
            // 判断仪表参数是否解析完成
            if (this.ResCyclicRedundancyCheck.Text == "通过")
            {
                try
                {
                    // 发送下行报文建立连接 
                    //// 生成16进制字符串
                    this.SendTextBox.Text = this.EstablishBuild_Text();
                    //// 标定连接发送（已替换为通过connectFlag自动发送）
                    //SerialPortSend(sendTextBox.Text);
                    //serialPort.Write(EstablishBuild_Text());
                    // 指示灯变绿
                    if (true)
                    {
                        this.ConnectionStatusEllipse.Fill = Brushes.Yellow;
                        this.ConnectFlag = true;
                    }

                    this.EstablishConnectionButton.Content = "断开仪表";
                    this.EstablishConnectionButton.IsEnabled = false;
                    // 更新率锁定
                    this.RegularDataUpdateRate.IsEnabled = false;
                    this.StatusTextBlock.Text = "正在连接仪表……";
                }
                catch (Exception ex)
                {
                    var str = ex.StackTrace;
                    Console.WriteLine(str);
                    // 异常时显示提示文字
                    this.StatusTextBlock.Text = "仪表连接出错！";
                    // 指示灯变灰
                    this.EstablishConnectionButton.IsEnabled = true;
                    this.EstablishConnectionButton.Content = "连接仪表";
                    this.ConnectFlag = false;
                    this.ConnectionStatusEllipse.Fill = Brushes.Gray;
                }
            }
            else
            {
                this.StatusTextBlock.Text = "请先解析仪表参数！";
                this.EstablishConnectionButton.IsChecked = false;
            }
        }

        /// <summary>
        ///     建立连接帧
        /// </summary>
        /// <returns></returns>
        private string EstablishBuild_Text()
        {
            var str = "";
            switch (this.FrameHeader)
            {
                case "FE":
                    {
                        // 获取所需解析数据
                        this.ParameterAcquisition_FE(out var strHeader, out var strCommand, out var strAddress,
                            out var strProtocolVendor, out var strHandler, out var strGroup);
                        // 写操作数据区
                        // 功能码 / 数据类型
                        var strFunctionData = "00 80";
                        var strHandlerContent = "F0";
                        // 合成数据域
                        var strContent = strAddress + " " + strProtocolVendor + " " + strHandler + " " + strGroup + " " +
                                         strFunctionData + " " + strHandlerContent;
                        // 计算长度域（不包含命令域）
                        var intLength = (strContent.Length + 1) / 3;
                        var strLength = Convert.ToString(intLength, 16).ToUpper().PadLeft(2, '0');
                        var strInner = strLength + " " + strCommand + " " + strContent;
                        // 计算异或校验码
                        var strCyclicRedundancyCheck = this.CalCheckCode_FE(CleanHexString("00 " + strInner + " 00"));
                        // 合成返回值
                        str = CleanHexString(strHeader + " " + strInner + " " + strCyclicRedundancyCheck);
                    }
                    break;
                case "7E":
                    {
                        if (this.IsLoRaFlag)
                        {
                            // 获取所需解析数据
                            this.ParameterAcquisition_7E(out var strHeader, out _, out _,
                                out var strProtocolVendor, out var strHandler, out var strGroup);
                            // 写操作数据区
                            // 功能码 / 数据类型
                            var strFunctionData = "00 80";
                            var strHandlerContent = "F0";
                            // 合成数据域
                            var strContent = strProtocolVendor + " " + strHandler + " " + strGroup + " " +
                                             strFunctionData + " " + strHandlerContent;
                            // 计算长度域（包含命令域）
                            var intLength = (strContent.Length + 1) / 3;
                            var strLength = Convert.ToString(intLength, 16).ToUpper().PadLeft(4, '0').Insert(2, " ");
                            var strInner = strLength + " " + strContent;
                            // 计算异或校验码
                            var strCyclicRedundancyCheck = this.CalCheckCode_7E(CleanHexString("00 " + strInner + " 00"));
                            // 合成返回值
                            str = CleanHexString(strHeader + " " + strInner + " " + strCyclicRedundancyCheck);
                        }
                        else
                        {
                            // 获取所需解析数据
                            this.ParameterAcquisition_7E(out var strHeader, out var strCommand, out var strAddress,
                                out var strProtocolVendor, out var strHandler, out var strGroup);
                            // 写操作数据区
                            // 功能码 / 数据类型
                            var strFunctionData = "00 80";
                            var strHandlerContent = "F0";
                            // 合成数据域
                            var strContent = strCommand + " " + strAddress + " " +
                                             this.FrameUnparsed.Remove(this.FrameUnparsed.Length - 3, 3) + " 00 00 " +
                                             strProtocolVendor + " " + strHandler + " " + strGroup + " " +
                                             strFunctionData + " " + strHandlerContent;
                            // 计算长度域（包含命令域）
                            var intLength = (strContent.Length + 1) / 3;
                            var strLength = Convert.ToString(intLength, 16).ToUpper().PadLeft(4, '0').Insert(2, " ");
                            var strInner = strLength + " " + strContent;
                            // 计算异或校验码
                            var strCyclicRedundancyCheck = this.CalCheckCode_7E(CleanHexString("00 " + strInner + " 00"));
                            // 合成返回值
                            str = CleanHexString(strHeader + " " + strInner + " " + strCyclicRedundancyCheck);
                        }
                    }
                    break;
            }

            return str;
        }

        /// <summary>
        ///     断开仪表处理程序
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void EstablishConnectionButton_Unchecked(object sender, RoutedEventArgs e)
        {
            // 判断仪表参数是否解析完成
            if (this.ResCyclicRedundancyCheck.Text == "通过")
            {
                try
                {
                    // 发送下行报文建立连接 
                    //// 生成16进制字符串
                    this.SendTextBox.Text = this.EstablishDisconnect_Text();
                    //// 标定连接发送（已替换为通过connectFlag自动发送）
                    this.SerialPortSend(this.SendTextBox.Text);
                    // serialPort.Write(EstablishBuild_Text());
                    // 指示灯变灰
                    this.EstablishConnectionButton.Content = "连接仪表";
                    this.ConnectFlag = false;
                    this.ConnectionStatusEllipse.Fill = Brushes.Gray;
                    // 更新率不锁定
                    this.RegularDataUpdateRate.IsEnabled = true;
                    this.EstablishConnectionButton.IsEnabled = true;
                    this.StatusTextBlock.Text = "仪表连接已断开";
                }
                catch (Exception ex)
                {
                    var str = ex.StackTrace;
                    Console.WriteLine(str);
                    // 异常时显示提示文字
                    this.StatusTextBlock.Text = "断开仪表出错！";
                    // 指示灯变灰
                    this.EstablishConnectionButton.Content = "断开仪表";
                    this.EstablishConnectionButton.IsEnabled = true;
                    this.ConnectionStatusEllipse.Fill = Brushes.Green;
                }
            }
            else
            {
                this.StatusTextBlock.Text = "请先解析仪表参数！";
            }
        }

        /// <summary>
        ///     断开连接帧
        /// </summary>
        /// <returns></returns>
        private string EstablishDisconnect_Text()
        {
            var str = "";
            switch (this.FrameHeader)
            {
                case "FE":
                    {
                        // 获取所需解析数据
                        this.ParameterAcquisition_FE(out var strHeader, out var strCommand, out var strAddress,
                            out var strProtocolVendor, out var strHandler, out var strGroup);
                        // 写操作数据区
                        // 功能码 / 数据类型
                        var strFunctionData = "00 80";
                        var strHandlerContent = "FF";
                        // 合成数据域
                        var strContent = strAddress + " " + strProtocolVendor + " " + strHandler + " " + strGroup + " " +
                                         strFunctionData + " " + strHandlerContent;
                        // 计算长度域（不包含命令域）
                        var intLength = (strContent.Length + 1) / 3;
                        var strLength = Convert.ToString(intLength, 16).ToUpper().PadLeft(2, '0');
                        var strInner = strLength + " " + strCommand + " " + strContent;
                        // 计算异或校验码
                        var strCyclicRedundancyCheck = this.CalCheckCode_FE(CleanHexString("00 " + strInner + " 00"));
                        // 合成返回值
                        str = CleanHexString(strHeader + " " + strInner + " " + strCyclicRedundancyCheck);
                    }
                    break;
                case "7E":
                    {
                        if (this.IsLoRaFlag)
                        {
                            // 获取所需解析数据
                            this.ParameterAcquisition_7E(out var strHeader, out _, out _,
                                out var strProtocolVendor, out var strHandler, out var strGroup);
                            // 写操作数据区
                            // 功能码 / 数据类型
                            var strFunctionData = "00 80";
                            var strHandlerContent = "FF";
                            // 合成数据域
                            var strContent = strProtocolVendor + " " + strHandler + " " + strGroup + " " +
                                             strFunctionData + " " + strHandlerContent;
                            // 计算长度域（包含命令域）
                            var intLength = (strContent.Length + 1) / 3;
                            var strLength = Convert.ToString(intLength, 16).ToUpper().PadLeft(4, '0').Insert(2, " ");
                            var strInner = strLength + " " + strContent;
                            // 计算异或校验码
                            var strCyclicRedundancyCheck = this.CalCheckCode_7E(CleanHexString("00 " + strInner + " 00"));
                            // 合成返回值
                            str = CleanHexString(strHeader + " " + strInner + " " + strCyclicRedundancyCheck);
                        }
                        else
                        {
                            // 获取所需解析数据
                            this.ParameterAcquisition_7E(out var strHeader, out var strCommand, out var strAddress,
                                out var strProtocolVendor, out var strHandler, out var strGroup);
                            // 写操作数据区
                            // 功能码 / 数据类型
                            var strFunctionData = "00 80";
                            var strHandlerContent = "FF";
                            // 合成数据域
                            var strContent = strCommand + " " + strAddress + " " +
                                             this.FrameUnparsed.Remove(this.FrameUnparsed.Length - 3, 3) + " 00 00 " +
                                             strProtocolVendor + " " + strHandler + " " + strGroup + " " +
                                             strFunctionData + " " + strHandlerContent;
                            // 计算长度域（包含命令域）
                            var intLength = (strContent.Length + 1) / 3;
                            var strLength = Convert.ToString(intLength, 16).ToUpper().PadLeft(4, '0').Insert(2, " ");
                            var strInner = strLength + " " + strContent;
                            // 计算异或校验码
                            var strCyclicRedundancyCheck = this.CalCheckCode_7E(CleanHexString("00 " + strInner + " 00"));
                            // 合成返回值
                            str = CleanHexString(strHeader + " " + strInner + " " + strCyclicRedundancyCheck);
                        }
                    }
                    break;
            }

            return str;
        }

        /// <summary>
        ///     描述标定处理程序
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void DescriptionCalibrationButton_Click(object sender, RoutedEventArgs e)
        {
            // 判断仪表参数是否解析完成和是否处于连接状态
            if (this.ResCyclicRedundancyCheck.Text == "通过" && this.ConnectionStatusEllipse.Fill == Brushes.Green)
            {
                try
                {
                    // 发送下行报文建立连接 
                    // 生成16进制字符串
                    this.SendTextBox.Text = this.DescribeCalibration_Text();
                    // 标定连接发送
                    this.SerialPortSend(this.SendTextBox.Text);
                    //if (true)
                    //{
                    //    establishConnectionButton.IsChecked = false;
                    //}
                    this.StatusTextBlock.Text = "描述标定已发送！";
                }
                catch (Exception ex)
                {
                    var str = ex.StackTrace;
                    Console.WriteLine(str);
                    // 异常时显示提示文字
                    this.StatusTextBlock.Text = "描述标定出错！";
                }
            }
            else
            {
                this.StatusTextBlock.Text = "请先连接仪表！";
            }
        }

        /// <summary>
        ///     生成描述标定数据
        /// </summary>
        /// <returns></returns>
        private string DescribeCalibration_Text()
        {
            var str = "";
            switch (this.FrameHeader)
            {
                case "FE":
                    {
                        // 获取所需解析数据
                        this.ParameterAcquisition_FE(out var strHeader, out var strCommand, out var strAddress,
                            out var strProtocolVendor, out var strHandler, out var strGroup);
                        // 获取设备描述标定信息
                        // 功能码 / 数据类型
                        var strFunctionData = "00 80";
                        // 写操作数据区
                        var strHandlerContent = "F1 " + this.CalibrationInstrumentModelTextBox.Text.Trim() + " " +
                                                this.CalibrationSerialNumberTextBox.Text.Trim() + " " +
                                                this.CalibrationIpRatingTextBox.Text.Trim() + " " +
                                                this.CalibrationExplosionProofLevelTextBox.Text.Trim() + " " +
                                                this.CalibrationInstructionsTextBox.Text.Trim();
                        // 合成数据域
                        var strContent = strAddress + " " + strProtocolVendor + " " + strHandler + " " + strGroup + " " +
                                         strFunctionData + " " + strHandlerContent;
                        // 计算长度域
                        var intLength = (strContent.Length + 1) / 3;
                        var strLength = Convert.ToString(intLength, 16).ToUpper().PadLeft(2, '0');
                        var strInner = strLength + " " + strCommand + " " + strContent;
                        // 计算异或校验码
                        var strCyclicRedundancyCheck = this.HexCyclicRedundancyCheck(CleanHexString(strInner));
                        // 合成返回值
                        str = CleanHexString(strHeader + " " + strInner + " " + strCyclicRedundancyCheck);
                    }
                    break;
                case "7E":
                    {
                        if (this.IsLoRaFlag)
                        {
                            // 获取所需解析数据
                            this.ParameterAcquisition_7E(out var strHeader, out _, out _,
                                out var strProtocolVendor, out var strHandler, out var strGroup);
                            // 获取设备描述标定信息
                            // 功能码 / 数据类型
                            var strFunctionData = "00 80";
                            // 写操作数据区
                            var strHandlerContent = "F1 " + this.CalibrationInstrumentModelTextBox.Text.Trim() + " " +
                                                    this.CalibrationSerialNumberTextBox.Text.Trim() + " " +
                                                    this.CalibrationIpRatingTextBox.Text.Trim() + " " +
                                                    this.CalibrationExplosionProofLevelTextBox.Text.Trim() + " " +
                                                    this.CalibrationInstructionsTextBox.Text.Trim();
                            // 合成数据域
                            var strContent = strProtocolVendor + " " + strHandler + " " + strGroup + " " +
                                             strFunctionData + " " + strHandlerContent;
                            // 计算长度域（包含命令域）
                            var intLength = (strContent.Length + 1) / 3;
                            var strLength = Convert.ToString(intLength, 16).ToUpper().PadLeft(4, '0').Insert(2, " ");
                            var strInner = strLength + " " + strContent;
                            // 计算异或校验码
                            var strCyclicRedundancyCheck = this.CalCheckCode_7E(CleanHexString("00 " + strInner + " 00"));
                            // 合成返回值
                            str = CleanHexString(strHeader + " " + strInner + " " + strCyclicRedundancyCheck);
                        }
                        else
                        {
                            // 获取所需解析数据
                            this.ParameterAcquisition_7E(out var strHeader, out var strCommand, out var strAddress,
                                out var strProtocolVendor, out var strHandler, out var strGroup);
                            // 获取设备描述标定信息
                            // 功能码 / 数据类型
                            var strFunctionData = "00 80";
                            // 写操作数据区
                            var strHandlerContent = "F1 " + this.CalibrationInstrumentModelTextBox.Text.Trim() + " " +
                                                    this.CalibrationSerialNumberTextBox.Text.Trim() + " " +
                                                    this.CalibrationIpRatingTextBox.Text.Trim() + " " +
                                                    this.CalibrationExplosionProofLevelTextBox.Text.Trim() + " " +
                                                    this.CalibrationInstructionsTextBox.Text.Trim();
                            // 合成数据域
                            var strContent = strCommand + " " + strAddress + " " +
                                             this.FrameUnparsed.Remove(this.FrameUnparsed.Length - 3, 3) + " 00 00 " +
                                             strProtocolVendor + " " + strHandler + " " + strGroup + " " +
                                             strFunctionData + " " + strHandlerContent;
                            // 计算长度域
                            var intLength = (strContent.Length + 1) / 3;
                            var strLength = Convert.ToString(intLength, 16).ToUpper().PadLeft(4, '0').Insert(2, " ");
                            var strInner = strLength + " " + strContent;
                            // 计算异或校验码
                            var strCyclicRedundancyCheck = this.CalCheckCode_7E(CleanHexString("00 " + strInner + " 00"));
                            // 合成返回值
                            str = CleanHexString(strHeader + " " + strInner + " " + strCyclicRedundancyCheck);
                        }
                    }
                    break;
            }

            return str;
        }

        /// <summary>
        ///     参数标定处理程序
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ParameterCalibrationButton_Click(object sender, RoutedEventArgs e)
        {
            // 判断仪表参数是否解析完成和是否处于连接状态
            if (this.ResCyclicRedundancyCheck.Text == "通过" && this.ConnectionStatusEllipse.Fill == Brushes.Green)
            {
                try
                {
                    // 发送下行报文建立连接 
                    // 生成16进制字符串
                    this.SendTextBox.Text = this.ParameterCalibration_Text();
                    this.SerialPortSend(this.SendTextBox.Text);
                    this.StatusTextBlock.Text = "参数标定已发送！";

                    // 标定连接发送
                    // SerialPortSend();
                }
                catch (Exception ex)
                {
                    var str = ex.StackTrace;
                    Console.WriteLine(str);
                    // 异常时显示提示文字
                    this.StatusTextBlock.Text = "描述标定出错！";
                }
            }
            else
            {
                this.StatusTextBlock.Text = "请先连接仪表！";
            }
        }

        /// <summary>
        ///     生成参数标定数据
        /// </summary>
        /// <returns></returns>
        private string ParameterCalibration_Text()
        {
            var str = "";
            switch (this.FrameHeader)
            {
                case "FE":
                    {
                        // 获取所需解析数据
                        this.ParameterAcquisition_FE(out var strHeader, out var strCommand, out var strAddress,
                            out var strProtocolVendor, out var strHandler, out var strGroup);
                        // 获取设备描述标定信息
                        // 功能码 / 数据类型
                        var strFunctionData = "00 80";
                        // 标定数据
                        var calibrationParameters = FloatStrToHexStr(this.CalibrationParametersContentTextBox.Text);
                        // 写操作数据区
                        var strHandlerContent = "F2 " + this.CalibrationParametersComboBox.Text.Substring(2, 2).Trim() + " " +
                                                this.CalibrationUnitTextBox.Text.Trim() + " " + calibrationParameters;
                        // 合成数据域
                        var strContent = strAddress + " " + strProtocolVendor + " " + strHandler + " " + strGroup + " " +
                                         strFunctionData + " " + strHandlerContent;
                        // 计算长度域
                        var intLength = (strContent.Length + 1) / 3;
                        var strLength = Convert.ToString(intLength, 16).ToUpper().PadLeft(2, '0');
                        var strInner = strLength + " " + strCommand + " " + strContent;
                        // 计算异或校验码
                        var strCyclicRedundancyCheck = this.HexCyclicRedundancyCheck(CleanHexString(strInner));
                        // 合成返回值
                        str = CleanHexString(strHeader + " " + strInner + " " + strCyclicRedundancyCheck);
                    }
                    break;
                case "7E":
                    {
                        if (this.IsLoRaFlag)
                        {
                            // 获取所需解析数据
                            this.ParameterAcquisition_7E(out var strHeader, out _, out _,
                                out var strProtocolVendor, out var strHandler, out var strGroup);
                            // 获取设备描述标定信息
                            // 功能码 / 数据类型
                            var strFunctionData = "00 80";
                            // 标定数据
                            var calibrationParameters = FloatStrToHexStr(this.CalibrationParametersContentTextBox.Text);
                            // 写操作数据区
                            var strHandlerContent = "F2 " + this.CalibrationParametersComboBox.Text.Substring(2, 2).Trim() +
                                                    " " + this.CalibrationUnitTextBox.Text.Trim() + " " +
                                                    calibrationParameters;
                            // 合成数据域
                            var strContent = strProtocolVendor + " " + strHandler + " " + strGroup + " " +
                                             strFunctionData + " " + strHandlerContent;
                            // 计算长度域（包含命令域）
                            var intLength = (strContent.Length + 1) / 3;
                            var strLength = Convert.ToString(intLength, 16).ToUpper().PadLeft(4, '0').Insert(2, " ");
                            var strInner = strLength + " " + strContent;
                            // 计算异或校验码
                            var strCyclicRedundancyCheck = this.CalCheckCode_7E(CleanHexString("00 " + strInner + " 00"));
                            // 合成返回值
                            str = CleanHexString(strHeader + " " + strInner + " " + strCyclicRedundancyCheck);
                        }
                        else
                        {
                            // 获取所需解析数据
                            this.ParameterAcquisition_7E(out var strHeader, out var strCommand, out var strAddress,
                                out var strProtocolVendor, out var strHandler, out var strGroup);
                            // 获取设备描述标定信息
                            // 功能码 / 数据类型
                            var strFunctionData = "00 80";
                            // 标定数据
                            var calibrationParameters = FloatStrToHexStr(this.CalibrationParametersContentTextBox.Text);
                            // 写操作数据区
                            var strHandlerContent = "F2 " + this.CalibrationParametersComboBox.Text.Substring(2, 2).Trim() +
                                                    " " + this.CalibrationUnitTextBox.Text.Trim() + " " +
                                                    calibrationParameters;
                            // 合成数据域
                            var strContent = strCommand + " " + strAddress + " " +
                                             this.FrameUnparsed.Remove(this.FrameUnparsed.Length - 3, 3) + " 00 00 " +
                                             strProtocolVendor + " " + strHandler + " " + strGroup + " " +
                                             strFunctionData + " " + strHandlerContent;
                            // 计算长度域
                            var intLength = (strContent.Length + 1) / 3;
                            var strLength = Convert.ToString(intLength, 16).ToUpper().PadLeft(4, '0').Insert(2, " ");
                            var strInner = strLength + " " + strContent;
                            // 计算异或校验码
                            var strCyclicRedundancyCheck = this.CalCheckCode_7E(CleanHexString("00 " + strInner + " 00"));
                            // 合成返回值
                            str = CleanHexString(strHeader + " " + strInner + " " + strCyclicRedundancyCheck);
                        }
                    }
                    break;
            }

            return str;
        }


        /// <summary>
        ///     FE标定公用数据
        /// </summary>
        /// <param name="strHeader"></param>
        /// <param name="strCommand"></param>
        /// <param name="strAddress"></param>
        /// <param name="strProtocolVendor"></param>
        /// <param name="strHandler"></param>
        /// <param name="strGroup"></param>
        private void ParameterAcquisition_FE(out string strHeader, out string strCommand, out string strAddress,
            out string strProtocolVendor, out string strHandler, out string strGroup)
        {
            // 获取所需解析数据
            // 帧头
            strHeader = "FE";
            // 发送命令域
            strCommand = "24 5F";
            // 发送地址
            strAddress = this.FrameAddress;
            // 协议和厂商号为数据内容前四位
            strProtocolVendor = this.FrameContent.Substring(0, 11);
            // 仪表类型：手操器
            strHandler = "1F 10";
            // 组号表号
            strGroup = this.FrameContent.Substring(18, 5);
        }

        /// <summary>
        ///     7E标定公用数据
        /// </summary>
        /// <param name="strHeader"></param>
        /// <param name="strCommand"></param>
        /// <param name="strAddress"></param>
        /// <param name="strProtocolVendor"></param>
        /// <param name="strHandler"></param>
        /// <param name="strGroup"></param>
        private void ParameterAcquisition_7E(out string strHeader, out string strCommand, out string strAddress,
            out string strProtocolVendor, out string strHandler, out string strGroup)
        {
            // 帧头
            strHeader = "7E";
            if (this.IsLoRaFlag)
            {
                // 发送命令域
                strCommand = "";
                // 发送地址
                strAddress = "";
            }
            else
            {
                // 发送命令域
                strCommand = "11 00";
                // 发送地址
                strAddress = this.FrameAddress;
            }

            // 协议和厂商号
            strProtocolVendor = this.FrameContent.Substring(0, 11);
            // 仪表类型：手操器
            strHandler = "1F 10";
            // 组号表号
            strGroup = this.FrameContent.Substring(18, 5);
        }

        #endregion

        #region 绘图方法
        /// <summary>
        ///     画折线图
        /// </summary>
        public ObservableDataSource<Point> DataSource { get; private set; } = new ObservableDataSource<Point>();

        public int PlotPointX { get; private set; } = 1;
        public double PlotPointY { get; private set; }
        public bool ConnectFlag { get; private set; }
        /// <summary>
        ///     绘图方法
        /// </summary>
        private void AnimatedPlot()
        {
            double x = this.PlotPointX;
            try
            {
                this.PlotPointY = this.RealTimeData;
            }
            catch (Exception ex)
            {
                var str = ex.StackTrace;
                Console.WriteLine(str);
            }

            var point = new Point(x, this.PlotPointY);
            this.DataSource.AppendAsync(this.Dispatcher, point);
            this.PlotPointX++;
        }

        /// <summary>
        ///     加载绘图区
        /// </summary>
        private void Plot_Loaded()
        {
            this.Plotter.AxisGrid.Visibility = Visibility.Hidden;
            this.Plotter.AddLineGraph(this.DataSource, Colors.Blue, 2, "实时数据");
            this.Plotter.Viewport.Visible = new Rect(0, -1, 5, 24);
            this.Plotter.Viewport.FitToView();
        }
        #endregion

        #region 数据库方法
        #region 数据库操作字符串定义

        #region 连接字符串

        /// <summary>
        ///     服务器连接字符串
        /// </summary>
        public static string ConnectString { get; set; }


        /// <summary>
        ///     服务器名称
        /// </summary>
        private static string SqlServer { get; set; }

        /// <summary>
        ///     身份验证类型
        /// </summary>
        private static string SqlIntegratedSecurity { get; set; }

        /// <summary>
        ///     数据库名称
        /// </summary>
        private static string SqlDatabase { get; set; }

        /// <summary>
        ///     工作表
        /// </summary>
        private static string WorkSheet { get; set; }

        #endregion 连接字符串

        #endregion 数据库操作字符串定义


        #region 声明

        /// <summary>
        ///     标定用数据库连接
        /// </summary>
        public SqlConnection CalibrationSqlConnect { get; set; }


        /// <summary>
        ///     执行数据库命令
        /// </summary>
        /// <param name="command">需要执行的命令</param>
        /// <param name="sql">执行命令的数据库连接对象</param>
        private int ExecuteSqlCommand(string command, SqlConnection sql)
        {
            var returnValue = -1;
            var cmd = new SqlCommand(command, sql);
            try
            {
                returnValue = cmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                var str = ex.StackTrace;
                Console.WriteLine(str);
                MessageBox.Show(ex.Message);
            }
            return returnValue;
        }

        #endregion

        /// <summary>
        ///     建立数据库连接
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SqlConnectButton_Checked(object sender, RoutedEventArgs e)
        {
            var sqlSetting = new SqlSetting();
            sqlSetting.ShowDialog();
            SqlServer = sqlSetting.setSqlServer;
            SqlIntegratedSecurity = sqlSetting.setSqlIntegratedSecurity;
            SqlDatabase = sqlSetting.setSqlDatabase;
            WorkSheet = sqlSetting.setWorkSheet;

            ConnectString = $"Server={SqlServer}; Integrated security={SqlIntegratedSecurity}; database={SqlDatabase}";
            this.CalibrationSqlConnect = new SqlConnection(ConnectString);


            // 连接到数据库服务器
            try
            {
                this.CalibrationSqlConnect.Open();
                MessageBox.Show($"与服务器{SqlServer}建立连接：操作数据库为{SqlDatabase}。", "建立数据库连接", MessageBoxButton.OK,
                    MessageBoxImage.Information);
                this.SqlConnectButton.Content = "断开数据库";
                this.SqlConnectEllipse.Fill = Brushes.Green;
            }
            catch (Exception ex)
            {
                MessageBox.Show("建立连接失败：" + ex.Message, "建立数据库连接", MessageBoxButton.OK,
                    MessageBoxImage.Exclamation);
                this.SqlConnectButton.IsChecked = false;
                this.SqlConnectButton.Content = "连接数据库";
                this.SqlConnectEllipse.Fill = Brushes.Gray;
            }
            var command = $"create table {WorkSheet} ( id int identity(1, 1) primary key, type varchar(MAX), protocol varchar(MAX), address varchar(MAX), data varchar(MAX), statue varchar(MAX))";
            // 创建数据表（如果已经存在就不创建了）
            this.ExecuteSqlCommand(command, this.CalibrationSqlConnect);
        }

        /// <summary>
        ///     断开数据库连接
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SqlConnectButton_Unchecked(object sender, RoutedEventArgs e)
        {
            try
            {
                this.CalibrationSqlConnect.Close();
                MessageBox.Show($"与服务器{SqlServer}断开连接：操作数据库为{SqlDatabase}。", "断开数据库连接", MessageBoxButton.OK,
                    MessageBoxImage.Information);
                this.SqlConnectButton.Content = "连接数据库";
                this.SqlConnectEllipse.Fill = Brushes.Gray;
            }
            catch (Exception ex)
            {
                MessageBox.Show("断开连接失败：" + ex.Message, "断开数据库连接", MessageBoxButton.OK,
                    MessageBoxImage.Exclamation);
                this.SqlConnectButton.IsChecked = true;
                this.SqlConnectButton.Content = "断开数据库";
                this.SqlConnectEllipse.Fill = Brushes.Green;
            }
        }
        /// <summary>
        ///     写入表方法
        /// </summary>
        /// <param name="type"></param>
        /// <param name="protocol"></param>
        /// <param name="address"></param>
        /// <param name="data"></param>
        /// <param name="statue"></param>
        /// <param name="workSheet"></param>
        /// <param name="sql"></param>
        private void DatabaseWrite(string type, string protocol, string address, string data, string statue, string workSheet, SqlConnection sql)
        {
            try
            {
                var command = $"insert into {workSheet}(type, protocol, address, data, statue) values('{type}', '{protocol}', '{address}', '{data}', '{statue}')";
                var returnValue = this.ExecuteSqlCommand(command, sql);
                if (returnValue != -1)
                {
                    //MessageBox.Show("写入成功！");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("写入失败！" + ex.Message);
            }
        }



        #endregion

        #region 其他方法

        /// <summary>
        ///     对十六进制报文字符串进行分割
        /// </summary>
        /// <param name="str"></param>
        /// <returns>第0位为头字节，第1至length - 2位为可用字节，第length - 1位为尾字节</returns>
        private string[] MessageSegmentationExtraction(string str)
        {
            var sepStr = str.Split(' ');
            string handleStr;
            string[] sepHandleStr;
            var tmpStr = "";
            var mantissaStartFrame = 0;
            // 定义输出格式
            var outPutStrTag = 0;
            var outPutStr = new string[1000];
            // 寻找可用报文
            for (var i = 0; i < sepStr.Length; i++)
            {
                // 先判断帧头
                switch (sepStr[i])
                {
                    case "FE":
                        handleStr = str.Substring(i * 3, str.Length - i * 3);
                        sepHandleStr = handleStr.Split(' ');
                        // 通过长度域和异或码判断是否为完整报文①至少有5位（有长度域）②按长度域能取到足够长度
                        if (sepHandleStr.Length >= 5 &&
                            sepHandleStr.Length >= Convert.ToInt32(sepHandleStr[1], 16) + 5)
                        {
                            // 按长度域提取完整报文的部分
                            var useFrame = new string[Convert.ToInt32(sepHandleStr[1], 16) + 5];
                            Array.Copy(sepHandleStr, useFrame, Convert.ToInt32(sepHandleStr[1], 16) + 5);
                            var useFrameStr = string.Join(" ", useFrame);
                            // 判断完整报文是否满足异或码要求
                            if (this.CalCheckCode_FE(useFrameStr) == useFrameStr.Substring(useFrameStr.Length - 2, 2))
                            {
                                // 提取头帧（如果有）
                                if (outPutStrTag == 0 && i != 0)
                                {
                                    var headFrame = new string[i];
                                    Array.Copy(sepStr, headFrame, i);
                                    outPutStr.SetValue(string.Join(" ", headFrame), 0);
                                }

                                // 提取可用帧
                                outPutStr.SetValue(useFrameStr, ++outPutStrTag);
                                i = i + Convert.ToInt32(sepHandleStr[1], 16) + 4;
                                mantissaStartFrame = i + 1;
                            }

                        }
                        break;
                    case "7E":
                        handleStr = str.Substring(i * 3, str.Length - i * 3);
                        sepHandleStr = handleStr.Split(' ');
                        if (sepHandleStr.Length >= 5)
                        {
                            tmpStr = sepHandleStr[1] + sepHandleStr[2];
                        }
                        // 通过长度域和异或码判断是否为完整报文①至少有5位（有长度域）②按长度域能取到足够长度
                        if (sepHandleStr.Length >= 5 && sepHandleStr.Length >=
                            Convert.ToInt32(sepHandleStr[1] + sepHandleStr[2], 16) + 4)
                        {
                            var a = new string[Convert.ToInt32(tmpStr, 16) + 4];
                            Array.Copy(sepHandleStr, a, Convert.ToInt32(tmpStr, 16) + 4);
                            var useFrameStr = string.Join(" ", a);
                            // 判断完整报文是否满足异或码要求
                            if (this.CalCheckCode_7E(useFrameStr) == useFrameStr.Substring(useFrameStr.Length - 2, 2))
                            {
                                // 提取头帧（如果有）
                                if (outPutStrTag == 0 && i != 0)
                                {
                                    var headFrame = new string[i];
                                    Array.Copy(sepStr, headFrame, i);
                                    outPutStr.SetValue(string.Join(" ", headFrame), 0);
                                }

                                // 提取可用帧
                                outPutStr.SetValue(useFrameStr, ++outPutStrTag);
                                i = i + Convert.ToInt32(tmpStr, 16) + 3;
                                mantissaStartFrame = i + 1;
                            }
                        }

                        break;
                }
            }

            // 提取尾帧
            if (mantissaStartFrame < sepStr.Length)
            {
                outPutStr.SetValue(str.Substring(mantissaStartFrame * 3, str.Length - mantissaStartFrame * 3),
                    outPutStrTag + 1);
            }

            //Console.WriteLine();
            if (outPutStrTag == 0)
            {
                var useOutPutStr = new string[1];
                useOutPutStr.SetValue(str, 0);
                return useOutPutStr;
            }

            if (outPutStr[0] == null)
            {
                outPutStr = outPutStr.Where(s => !string.IsNullOrEmpty(s)).ToArray();
                var useOutPutStr = new string[outPutStr.Length + 1];
                Array.Copy(outPutStr, 0, useOutPutStr, 1, outPutStr.Length);
                return useOutPutStr;
            }

            outPutStr = outPutStr.Where(s => !string.IsNullOrEmpty(s)).ToArray();
            return outPutStr;
        }

        /// <summary>
        ///     二进制字符串转换为十六进制字符串并格式化
        /// </summary>
        /// <param name="bytes"></param>
        private static string BytesToHexStr(byte[] bytes)
        {
            var hexStr = bytes.Aggregate("", (current, t) => current + $"{t:X2} ");

            hexStr = hexStr.Trim();
            return hexStr;
        }

        /// <summary>
        ///     两个十六进制字符串求异或
        /// </summary>
        /// <param name="hexStr1"></param>
        /// <param name="hexStr2"></param>
        /// <returns></returns>
        public string HexStrXor(string hexStr1, string hexStr2)
        {
            if (hexStr1 == null)
            {
                throw new ArgumentNullException(nameof(hexStr1));
            }
            // 两个十六进制字符串的长度和长度差的绝对值以及异或结果
            var iHexStr1Len = hexStr1.Length;
            var iHexStr2Len = hexStr2.Length;
            var result = string.Empty;
            // 获取这两个十六进制字符串长度的差值
            var iGap = iHexStr1Len - iHexStr2Len;
            // 获取这两个十六进制字符串长度最小的那一个
            var iHexStrLenLow = iHexStr1Len < iHexStr2Len ? iHexStr1Len : iHexStr2Len;
            // 将这两个字符串转换成字节数组
            var bHexStr1 = this.HexStrToBytes(hexStr1);
            var bHexStr2 = this.HexStrToBytes(hexStr2);
            var i = 0;
            //先把每个字节异或后得到一个0~15范围内的整数，再转换成十六进制字符
            for (; i < iHexStrLenLow; ++i)
            {
                result += (bHexStr1[i] ^ bHexStr2[i]).ToString("X");
            }

            result += iGap >= 0 ? hexStr1.Substring(i, iGap) : hexStr2.Substring(i, -iGap);
            return result;
        }

        /// <summary>
        ///     一串字符串求异或值
        /// </summary>
        /// <param name="ori"></param>
        /// <returns></returns>
        private string HexCyclicRedundancyCheck(string ori)
        {
            if (ori == null)
            {
                throw new ArgumentNullException(nameof(ori));
            }

            var hexvalue = ori.Trim().Split(' ', '	');
            var j = "";
            foreach (var hex in hexvalue)
            {
                j = this.HexStrXor(j, hex);
            }

            return j;
        }

        /// <summary>
        ///     将十六进制字符串转换为十六进制数组
        /// </summary>
        /// <param name="hexStr"></param>
        /// <returns></returns>
        public byte[] HexStrToBytes(string hexStr)
        {
            if (hexStr == null)
            {
                throw new ArgumentNullException(nameof(hexStr));
            }

            var bytes = new byte[hexStr.Length];
            if (bytes == null)
            {
                throw new ArgumentNullException(nameof(bytes));
            }

            try
            {
                for (var i = 0; i < bytes.Length; ++i)
                {
                    //将每个16进制字符转换成对应的1个字节
                    bytes[i] = Convert.ToByte(hexStr.Substring(i, 1), 16);
                }
            }
            catch (Exception ex)
            {
                var str = ex.StackTrace;
                Console.WriteLine(str);
            }

            return bytes;
        }

        /// <summary>
        ///     十六进制字符串转换为浮点数
        /// </summary>
        /// <param name="hexStr"></param>
        /// <returns></returns>
        private float HexStrToFloat(string hexStr)
        {
            if (hexStr == null)
            {
                throw new ArgumentNullException(nameof(hexStr));
            }

            hexStr = hexStr.Replace(" ", "");
            if (hexStr.Length != 8)
            {
                throw new ArgumentNullException(nameof(hexStr));
            }

            var data1 = Convert.ToInt32(hexStr.Substring(0, 2), 16);
            var data2 = Convert.ToInt32(hexStr.Substring(2, 2), 16);
            var data3 = Convert.ToInt32(hexStr.Substring(4, 2), 16);
            var data4 = Convert.ToInt32(hexStr.Substring(6, 2), 16);

            var data = (data1 << 24) | (data2 << 16) | (data3 << 8) | data4;

            int nSign;
            if ((data & 0x80000000) > 0)
            {
                nSign = -1;
            }
            else
            {
                nSign = 1;
            }

            var nExp = data & 0x7F800000;
            nExp >>= 23;
            float nMantissa = data & 0x7FFFFF;

            if (nMantissa != 0)
            {
                nMantissa = 1 + nMantissa / 8388608;
            }

            var value = nSign * nMantissa * (2 << (nExp - 128));
            return value;
        }

        /// <summary>
        ///     浮点数字符串转十六进制字符串
        /// </summary>
        /// <param name="floatStr"></param>
        /// <returns></returns>
        private static string FloatStrToHexStr(string floatStr)
        {
            try
            {
                var tmpFloat = float.Parse(floatStr);
                var hexFloat = BitConverter.ToString(BitConverter.GetBytes(tmpFloat));
                return hexFloat.Replace("-", " ");
            }
            catch (Exception ex)
            {
                var str = ex.StackTrace;
                Console.WriteLine(str);
                return "";
            }
        }

        /// <summary>
        ///     仅接受数字
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        private bool IsValid(string input)
        {
            var regex = new Regex("[0-9]");
            return regex.IsMatch(input);
        }

        /// <summary>
        ///     仪表参数解析面板清空
        /// </summary>
        private void ParseParameterClear()
        {
            // 清空解析面板
            // 实时数据清空
            this.ResProtocol.Clear();
            this.ResAddress.Clear();
            this.ResVendor.Clear();
            this.ResType.Clear();
            this.ResGroup.Clear();
            this.ResFunctionData.Clear();
            this.ResSucRate.Clear();
            this.ResBatVol.Clear();
            this.ResSleepTime.Clear();
            this.ResStatue.Clear();
            this.ResData.Clear();
            this.ResTime.Clear();
            // 仪表参数清空
            this.ResModel.Clear();
            this.ResSerialNumber.Clear();
            this.ResFirmwareVersion.Clear();
            this.ResSoftwareVersion.Clear();
            this.ResLowRange.Clear();
            this.ResHighRange.Clear();
            this.ResMeasurementAccuracy.Clear();
            this.ResProtectionLevel.Clear();
            this.ResExplosionProofGrade.Clear();
            this.ResIllustrate.Clear();
            // 校验码清空
            this.ResCyclicRedundancyCheck.Clear();
            // 将前景色改为黑色
            // 实时数据改色
            this.ResProtocol.Foreground = new SolidColorBrush(Colors.Black);
            this.ResAddress.Foreground = new SolidColorBrush(Colors.Black);
            this.ResVendor.Foreground = new SolidColorBrush(Colors.Black);
            this.ResType.Foreground = new SolidColorBrush(Colors.Black);
            this.ResGroup.Foreground = new SolidColorBrush(Colors.Black);
            this.ResFunctionData.Foreground = new SolidColorBrush(Colors.Black);
            this.ResSucRate.Foreground = new SolidColorBrush(Colors.Black);
            this.ResBatVol.Foreground = new SolidColorBrush(Colors.Black);
            this.ResSleepTime.Foreground = new SolidColorBrush(Colors.Black);
            this.ResStatue.Foreground = new SolidColorBrush(Colors.Black);
            this.ResData.Foreground = new SolidColorBrush(Colors.Black);
            this.ResTime.Foreground = new SolidColorBrush(Colors.Black);
            this.ResCyclicRedundancyCheck.Foreground = new SolidColorBrush(Colors.Black);
            // 仪表参数改色
            this.ResModel.Foreground = new SolidColorBrush(Colors.Black);
            this.ResSerialNumber.Foreground = new SolidColorBrush(Colors.Black);
            this.ResFirmwareVersion.Foreground = new SolidColorBrush(Colors.Black);
            this.ResSoftwareVersion.Foreground = new SolidColorBrush(Colors.Black);
            this.ResLowRange.Foreground = new SolidColorBrush(Colors.Black);
            this.ResHighRange.Foreground = new SolidColorBrush(Colors.Black);
            this.ResMeasurementAccuracy.Foreground = new SolidColorBrush(Colors.Black);
            this.ResProtectionLevel.Foreground = new SolidColorBrush(Colors.Black);
            this.ResExplosionProofGrade.Foreground = new SolidColorBrush(Colors.Black);
            this.ResIllustrate.Foreground = new SolidColorBrush(Colors.Black);
            // 校验码改色
            this.ResCyclicRedundancyCheck.Foreground = new SolidColorBrush(Colors.Black);
            // 隐藏字段
            // 实时数据隐藏字段
            this.ResProtocolDockPanel.Visibility = Visibility.Collapsed;
            this.ResAddressDockPanel.Visibility = Visibility.Collapsed;
            this.ResVendorDockPanel.Visibility = Visibility.Collapsed;
            this.ResTypeDockPanel.Visibility = Visibility.Collapsed;
            this.ResGroupDockPanel.Visibility = Visibility.Collapsed;
            this.ResFunctionDataDockPanel.Visibility = Visibility.Collapsed;
            this.ResSucRateDockPanel.Visibility = Visibility.Collapsed;
            this.ResBatVolDockPanel.Visibility = Visibility.Collapsed;
            this.ResSleepTimeDockPanel.Visibility = Visibility.Collapsed;
            this.ResStatueDockPanel.Visibility = Visibility.Collapsed;
            this.ResDataDockPanel.Visibility = Visibility.Collapsed;
            this.ResTimeDockPanel.Visibility = Visibility.Collapsed;
            // 仪表参数隐藏字段
            this.ResModelDockPanel.Visibility = Visibility.Collapsed;
            this.ResSerialNumberDockPanel.Visibility = Visibility.Collapsed;
            this.ResFirmwareVersionDockPanel.Visibility = Visibility.Collapsed;
            this.ResSoftwareVersionDockPanel.Visibility = Visibility.Collapsed;
            this.ResLowRangeDockPanel.Visibility = Visibility.Collapsed;
            this.ResHighRangeDockPanel.Visibility = Visibility.Collapsed;
            this.ResMeasurementAccuracyDockPanel.Visibility = Visibility.Collapsed;
            this.ResProtectionLevelDockPanel.Visibility = Visibility.Collapsed;
            this.ResExplosionProofGradeDockPanel.Visibility = Visibility.Collapsed;
            this.ResIllustrateDockPanel.Visibility = Visibility.Collapsed;
            // 校验码隐藏字段
            this.ResCyclicRedundancyCheckDockPanel.Visibility = Visibility.Collapsed;
        }

        /// <summary>
        ///     更新率设置
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void RegularDataUpdateRate_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            {
                if (this.RegularDataUpdateRate.Text != "")
                {
                    if (this.IsValid(this.RegularDataUpdateRate.Text) == false ||
                        Convert.ToInt32(this.RegularDataUpdateRate.Text, 16) > 65535 ||
                        Convert.ToInt32(this.RegularDataUpdateRate.Text, 16) < 0)
                    {
                        MessageBox.Show("请输入0 - 65535整数");
                        this.RegularDataUpdateRate.Text = "8";
                    }
                }
            }
        }

        #region 数据预览
        /// <summary>
        ///     标定栏的数据预览
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CalibrationInstrumentModelTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            // 将光标移至文字末尾
            this.CalibrationInstrumentModelTextBox.SelectionStart = this.CalibrationInstrumentModelTextBox.Text.Length;
            var hexadecimalCollection = Regex.Matches(e.Text, @"[\da-fA-F]");
            foreach (Match mat in hexadecimalCollection)
            {
                this.CalibrationInstrumentModelTextBox.AppendText(mat.Value);
            }

            // 每输入两个字符自动添加空格
            this.CalibrationInstrumentModelTextBox.Text = this.CalibrationInstrumentModelTextBox.Text.Replace(" ", "");
            this.CalibrationInstrumentModelTextBox.Text = string.Join(" ",
                Regex.Split(this.CalibrationInstrumentModelTextBox.Text, "(?<=\\G.{2})(?!$)"));
            this.CalibrationInstrumentModelTextBox.SelectionStart = this.CalibrationInstrumentModelTextBox.Text.Length;
            e.Handled = true;
        }

        private void CalibrationSerialNumberTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            // 将光标移至文字末尾
            this.CalibrationSerialNumberTextBox.SelectionStart = this.CalibrationSerialNumberTextBox.Text.Length;
            var hexadecimalCollection = Regex.Matches(e.Text, @"[\da-fA-F]");
            foreach (Match mat in hexadecimalCollection)
            {
                this.CalibrationSerialNumberTextBox.AppendText(mat.Value);
            }

            // 每输入两个字符自动添加空格
            this.CalibrationSerialNumberTextBox.Text = this.CalibrationSerialNumberTextBox.Text.Replace(" ", "");
            this.CalibrationSerialNumberTextBox.Text = string.Join(" ",
                Regex.Split(this.CalibrationSerialNumberTextBox.Text, "(?<=\\G.{2})(?!$)"));
            this.CalibrationSerialNumberTextBox.SelectionStart = this.CalibrationSerialNumberTextBox.Text.Length;
            e.Handled = true;
        }

        private void CalibrationIPRatingTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            // 将光标移至文字末尾
            this.CalibrationIpRatingTextBox.SelectionStart = this.CalibrationIpRatingTextBox.Text.Length;
            var hexadecimalCollection = Regex.Matches(e.Text, @"[\da-fA-F]");
            foreach (Match mat in hexadecimalCollection)
            {
                this.CalibrationIpRatingTextBox.AppendText(mat.Value);
            }

            // 每输入两个字符自动添加空格
            this.CalibrationIpRatingTextBox.Text = this.CalibrationIpRatingTextBox.Text.Replace(" ", "");
            this.CalibrationIpRatingTextBox.Text =
                string.Join(" ", Regex.Split(this.CalibrationIpRatingTextBox.Text, "(?<=\\G.{2})(?!$)"));
            this.CalibrationIpRatingTextBox.SelectionStart = this.CalibrationIpRatingTextBox.Text.Length;
            e.Handled = true;
        }

        private void CalibrationExplosionProofLevelTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            // 将光标移至文字末尾
            this.CalibrationExplosionProofLevelTextBox.SelectionStart = this.CalibrationExplosionProofLevelTextBox.Text.Length;
            var hexadecimalCollection = Regex.Matches(e.Text, @"[\da-fA-F]");
            foreach (Match mat in hexadecimalCollection)
            {
                this.CalibrationExplosionProofLevelTextBox.AppendText(mat.Value);
            }

            // 每输入两个字符自动添加空格
            this.CalibrationExplosionProofLevelTextBox.Text = this.CalibrationExplosionProofLevelTextBox.Text.Replace(" ", "");
            this.CalibrationExplosionProofLevelTextBox.Text = string.Join(" ",
                Regex.Split(this.CalibrationExplosionProofLevelTextBox.Text, "(?<=\\G.{2})(?!$)"));
            this.CalibrationExplosionProofLevelTextBox.SelectionStart = this.CalibrationExplosionProofLevelTextBox.Text.Length;
            e.Handled = true;
        }

        private void CalibrationInstructionsTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            // 将光标移至文字末尾
            this.CalibrationInstructionsTextBox.SelectionStart = this.CalibrationInstructionsTextBox.Text.Length;
            var hexadecimalCollection = Regex.Matches(e.Text, @"[\da-fA-F]");
            foreach (Match mat in hexadecimalCollection)
            {
                this.CalibrationInstructionsTextBox.AppendText(mat.Value);
            }

            // 每输入两个字符自动添加空格
            this.CalibrationInstructionsTextBox.Text = this.CalibrationInstructionsTextBox.Text.Replace(" ", "");
            this.CalibrationInstructionsTextBox.Text = string.Join(" ",
                Regex.Split(this.CalibrationInstructionsTextBox.Text, "(?<=\\G.{2})(?!$)"));
            this.CalibrationInstructionsTextBox.SelectionStart = this.CalibrationInstructionsTextBox.Text.Length;
            e.Handled = true;
        }

        #endregion

        #endregion


    }

}