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
            InitializeComponent();
            // 检测和添加串口
            AddPortName();
            // 开启串口检测定时器，并设置自动检测1秒1次
            AutoDetectionTimer.Tick += AutoDetectionTimer_Tick;
            AutoDetectionTimer.Interval = new TimeSpan(0, 0, 0, 1, 0);
            AutoDetectionTimer.Start();
            // 开启当前时间定时器，并设置自动检测100毫秒1次
            GetCurrentTimer.Tick += GetCurrentTime;
            GetCurrentTimer.Interval = new TimeSpan(0, 0, 0, 0, 100);
            GetCurrentTimer.Start();
            // 设置自动发送定时器，并设置自动检测100毫秒1次
            AutoSendTimer.Tick += AutoSendTimer_Tick;
            // 设置定时时间，开启定时器
            AutoSendTimer.Interval = new TimeSpan(0, 0, 0, 0, Convert.ToInt32(AutoSendCycleTextBox.Text));
            // 设置状态栏提示
            StatusTextBlock.Text = "准备就绪";
        }

        /// <summary>
        ///     显示当前时间
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public void GetCurrentTime(object sender, EventArgs e)
        {
            DateStr = DateTime.Now.ToString("yyyy-MM-dd");
            TimeStr = DateTime.Now.ToString("HH:mm:ss");
            OperationTime.Text = DateStr + " " + TimeStr;
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
                // 如果检测到的串口不存在于portNameComboBox中，则添加
                if (PortNameComboBox.Items.Contains(name) == false)
                    PortNameComboBox.Items.Add(name);

            PortNameComboBox.SelectedIndex = 0;
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
                if (TurnOnButton.IsChecked == true)
                {
                    // 在有效串口号中遍历当前打开的串口号
                    foreach (var name in serialPortName)
                        // 如果找到串口，说明串口仍然有效，跳出循环
                        if (NewSerialPort.PortName == name)
                            return;

                    // 如果找不到, 说明串口失效了，关闭串口并移除串口名
                    TurnOnButton.IsChecked = false;
                    PortNameComboBox.Items.Remove(NewSerialPort.PortName);
                    PortNameComboBox.SelectedIndex = 0;
                    // 输出提示信息
                    StatusTextBlock.Text = "串口失效，已自动断开";
                }
                else
                {
                    // 检查有效串口和ComboBox中的串口号个数是否不同
                    if (PortNameComboBox.Items.Count != serialPortName.Length)
                    {
                        // 串口数不同，清空ComboBox
                        PortNameComboBox.Items.Clear();
                        // 重新添加有效串口
                        foreach (var name in serialPortName) PortNameComboBox.Items.Add(name);

                        PortNameComboBox.SelectedIndex = -1;
                        // 输出提示信息
                        StatusTextBlock.Text = "串口列表已更新！";
                    }
                }
            }
            catch (Exception ex)
            {
                var str = ex.StackTrace;
                Console.WriteLine(str);
                TurnOnButton.IsChecked = false;
                StatusTextBlock.Text = "串口检测错误！";
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
            PortNameComboBox.IsEnabled = state;
            BaudRateComboBox.IsEnabled = state;
            ParityComboBox.IsEnabled = state;
            DataBitsComboBox.IsEnabled = state;
            StopBitsComboBox.IsEnabled = state;
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
                NewSerialPort.PortName = PortNameComboBox.Text;
                NewSerialPort.BaudRate = Convert.ToInt32(BaudRateComboBox.Text);
                NewSerialPort.Parity = (Parity)Enum.Parse(typeof(Parity), ParityComboBox.Text);
                NewSerialPort.DataBits = Convert.ToInt16(DataBitsComboBox.Text);
                NewSerialPort.StopBits = (StopBits)Enum.Parse(typeof(StopBits), StopBitsComboBox.Text);
                NewSerialPort.Encoding = SetEncoding;
                // 添加串口事件处理, 设置委托
                NewSerialPort.DataReceived += ReceiveData;
                // 关闭串口配置面板, 开启串口, 变更按钮文本, 打开绿灯, 显示提示文字
                SerialSettingControlState(false);
                NewSerialPort.Open();
                StatusTextBlock.Text = "串口已开启";
                SerialPortStatusEllipse.Fill = Brushes.Green;
                TurnOnButton.Content = "关闭串口";
                // 设置超时
                NewSerialPort.ReadTimeout = 500;
                NewSerialPort.WriteTimeout = 500;
                // 清空缓冲区
                NewSerialPort.DiscardInBuffer();
                NewSerialPort.DiscardOutBuffer();
                // 使能作图区
                DataSource = new ObservableDataSource<Point>();
                Plot_Loaded();
            }
            catch (Exception ex)
            {
                var str = ex.StackTrace;
                Console.WriteLine(str);
                // 异常时显示提示文字
                StatusTextBlock.Text = "开启串口出错！";
                NewSerialPort.Close();
                AutoSendTimer.Stop();
                TurnOnButton.IsChecked = false;
                SerialSettingControlState(true);
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
                NewSerialPort.Close();
                AutoSendTimer.Stop();
                SerialSettingControlState(true);
                StatusTextBlock.Text = "串口已关闭";
                SerialPortStatusEllipse.Fill = Brushes.Gray;
                TurnOnButton.Content = "打开串口";
                // 关闭作图
                Plotter.Children.RemoveAll(typeof(LineGraph));
                PlotPointX = 1;
                // 关闭连接
            }
            catch (Exception ex)
            {
                var str = ex.StackTrace;
                Console.WriteLine(str);
                // 异常时显示提示文字
                StatusTextBlock.Text = "关闭串口出错！";
                TurnOnButton.IsChecked = true;
            }
        }

        #endregion

        #region 串口数据接收处理/窗口显示清空功能

        public string BrokenFrameEnd { get; private set; } = "";
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
                var receiveBuffer = new byte[NewSerialPort.BytesToRead];
                NewSerialPort.Read(receiveBuffer, 0, receiveBuffer.Length);
                // 字符串转换为十六进制字符串
                ReceiveText = BytesToHexStr(receiveBuffer);
            }
            catch (Exception ex)
            {
                var str = ex.StackTrace;
                Console.WriteLine(str);
                ReceiveText = "";
            }

            // 加入上一次断帧尾数
            ReceiveText = (BrokenFrameEnd + " " + ReceiveText).Trim(' ');
            // 对字符串进行断帧
            var segmentationStr = MessageSegmentationExtraction(ReceiveText);
            // 断帧情况判断
            switch (segmentationStr.Length)
            {
                // 如果只有一帧，要不是空帧，要不是废帧
                case 1:
                    BrokenFrameEnd = segmentationStr[0];
                    break;
                // 如果有两帧，只有一个头帧和一个可用帧
                case 2:
                    AvailableMessageHandler(segmentationStr[1]);
                    break;
                // 如果有多于三帧，则头帧，可用帧，尾帧都有
                default:
                    var useStr = new string[segmentationStr.Length - 2];
                    Array.Copy(segmentationStr, 1, useStr, 0, segmentationStr.Length - 2);
                    foreach (var item in useStr) AvailableMessageHandler(item);

                    BrokenFrameEnd = segmentationStr[segmentationStr.Length - 1];
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
            if (txt.Length < 2) return;
            try
            {
                switch (txt.Substring(0, 2))
                {
                    // Zigbee 四信解析
                    case "FE":
                        {
                            // 不解析确认帧，只显示
                            if (txt.Length >= 8 && txt.Substring(txt.Length - 8, 5) == "F0 55")
                                ConfirmationFrameResponse(txt);
                            // 如果是能解析的帧（常规数据帧或基本参数帧），就全面板显示
                            else if (txt.Substring(6, 5) == "44 5F" && (txt.Length + 1) / 3 == 27 ||
                                     (txt.Length + 1) / 3 == 81)
                                StatusReceiveByteTextBlock.Dispatcher.Invoke(FullPanelDisplay(txt));
                            // 如果是不能解析的帧，就部分显示
                            else if (txt.Replace(" ", "") != "")
                                StatusReceiveByteTextBlock.Dispatcher.Invoke(PartialPanelDisplay(txt));
                        }
                        break;
                    // Zigbee Digi和LoRa解析
                    case "7E":
                        {
                            // 不解析确认帧，只显示
                            if (txt.Length >= 8 && txt.Substring(txt.Length - 8, 5) == "F0 55")
                            {
                                ConfirmationFrameResponse(txt);
                            }
                            // 如果是能解析的LoRa帧（常规数据帧或基本参数帧），就全面板显示
                            else if ((txt.Length + 1) / 3 == 24 || (txt.Length + 1) / 3 == 78)
                            {
                                IsLoRaFlag = true;
                                StatusReceiveByteTextBlock.Dispatcher.Invoke(FullPanelDisplay(txt));
                            }
                            // 如果是能解析的Digi帧（常规数据帧或基本参数帧），就全面板显示
                            else if (txt.Substring(9, 2) == "91" && (txt.Length + 1) / 3 == 42 ||
                                     (txt.Length + 1) / 3 == 96)
                            {
                                IsLoRaFlag = false;
                                StatusReceiveByteTextBlock.Dispatcher.Invoke(FullPanelDisplay(txt));
                            }
                            // 如果是不能解析的帧，就部分显示
                            else if (txt.Replace(" ", "") != "")
                            {
                                StatusReceiveByteTextBlock.Dispatcher.Invoke(PartialPanelDisplay(txt));
                            }
                        }
                        break;

                    default:
                        StatusReceiveByteTextBlock.Dispatcher.Invoke(PartialPanelDisplay(txt));
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
            ConnectFlag = false;
            ConnectionStatusEllipse.Dispatcher.Invoke(ConnectionStatusEllipseColorCovert());
            EstablishConnectionButton.Dispatcher.Invoke(EstablishConnectionButtonEnabled());
            StatusReceiveByteTextBlock.Dispatcher.Invoke(PartialPanelDisplay(txt));
            StatusReceiveByteTextBlock.Dispatcher.Invoke(ConnectSuccessDisplay());
        }

        private Action ConnectSuccessDisplay()
        {
            return ConnectSuccessDisplay_Action;
        }

        private void ConnectSuccessDisplay_Action()
        {
            StatusTextBlock.Text = "建立连接成功！";
        }

        private Action EstablishConnectionButtonEnabled()
        {
            return EstablishConnectionButtonEnabled_Action;
        }

        private void EstablishConnectionButtonEnabled_Action()
        {
            EstablishConnectionButton.IsEnabled = true;
        }

        private Action ConnectionStatusEllipseColorCovert()
        {
            return ConnectionStatusEllipseColorCovert_Action;
        }

        private void ConnectionStatusEllipseColorCovert_Action()
        {
            ConnectionStatusEllipse.Fill = Brushes.Green;
        }

        private Action PartialPanelDisplay(string txt)
        {
            return delegate { ShowReceiveData(txt); };
        }

        private Action FullPanelDisplay(string txt)
        {
            return delegate
            {
                ShowReceiveData(txt);
                InstrumentDataSegmentedText(txt);
                ShowParseParameter(txt);
                SendConfirmationFrame(txt);
            };
        }

        /// <summary>
        ///     自动发送确认帧
        /// </summary>
        /// <param name="txt"></param>
        private void SendConfirmationFrame(string txt)
        {
            // 如果不需要建立连接，发送常规确认帧
            if (ConnectFlag == false)
                try
                {
                    switch (txt.Substring(0, 2))
                    {
                        case "FE":
                            {
                                if ((txt.Length + 1) / 3 == 27)
                                {
                                    var str = RegularDataConfirmationFrame();
                                    SerialPortSend(str);
                                }
                                else if ((txt.Length + 1) / 3 == 81)
                                {
                                    var str = BasicInformationConfirmationFrame();
                                    SerialPortSend(str);
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
                                            var str = RegularDataConfirmationFrame();
                                            SerialPortSend(str);
                                            break;
                                        }
                                    case 96:
                                    case 78:
                                        {
                                            var str = BasicInformationConfirmationFrame();
                                            SerialPortSend(str);
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
            // 如果需要建立连接，用建立连接帧替代确认帧
            else
                try
                {
                    var str = EstablishBuild_Text();
                    SerialPortSend(str);
                }
                catch (Exception ex)
                {
                    var str = ex.StackTrace;
                    Console.WriteLine(str);
                }
        }

        /// <summary>
        ///     接收窗口显示功能
        /// </summary>
        /// <param name="txt">需要窗口显示的字符串</param>
        private void ShowReceiveData(string txt)
        {
            // 更新接收字节数           
            ReceiveBytesCount += (uint)((txt.Length + 1) / 3);
            StatusReceiveByteTextBlock.Text = ReceiveBytesCount.ToString();
            // 在接收窗口中显示字符串
            if (txt.Replace(" ", "").Length >= 0)
            {
                // 接收窗口自动清空
                if (AutoClearCheckBox.IsChecked == true) DisplayTextBox.Clear();

                DisplayTextBox.AppendText(DateTime.Now + " <-- " + txt + "\r\n");
                DisplayScrollViewer.ScrollToEnd();
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
                            FrameHeader = txt.Substring(0 * 3, 1 * 3 - 1);
                            // 长度域 (1位, 最长为FF = 255)
                            FrameLength = txt.Substring((0 + 1) * 3, 1 * 3 - 1);
                            // 命令域 (2位)
                            FrameCommand = txt.Substring((0 + 1 + 1) * 3, 2 * 3 - 1);
                            // 数据域 (长度域指示长度)
                            // 数据地址域 (2位)
                            FrameAddress = txt.Substring((0 + 1 + 1 + 2) * 3, 2 * 3 - 1);
                            // 数据内容域 (去掉头6位，尾1位)
                            FrameContent = txt.Substring(6 * 3, txt.Length - 6 * 3 - 3);
                            // 校验码 (1位)
                            FrameCyclicRedundancyCheck = txt.Substring(txt.Length - 2, 2);
                        }
                        break;
                    case "7E":
                        {
                            // 如果是LoRa
                            if (IsLoRaFlag)
                            {
                                // 帧头 (1位)
                                FrameHeader = txt.Substring(0 * 3, 1 * 3 - 1);
                                // 长度域 (2位, 最长为FF = 65535)
                                FrameLength = txt.Substring((0 + 1) * 3, 2 * 3 - 1);
                                // 命令域 (0位，指示是否收到数据)
                                FrameCommand = "";
                                // 数据地址域 (0位)
                                FrameAddress = "";
                                // 非解析帧 (0位)
                                FrameUnparsed = txt.Substring((0 + 1 + 2 + 1 + 8) * 3, 9 * 3 - 1);
                                // 数据内容域 (去掉头3位，尾1位)
                                FrameContent = txt.Substring(3 * 3, txt.Length - 3 * 3 - 3);
                                // 校验码 (1位)
                                FrameCyclicRedundancyCheck = txt.Substring(txt.Length - 2, 2);
                            }
                            // 如果是Digi
                            else
                            {
                                // 帧头 (1位)
                                FrameHeader = txt.Substring(0 * 3, 1 * 3 - 1);
                                // 长度域 (2位, 最长为FF = 65535)
                                FrameLength = txt.Substring((0 + 1) * 3, 2 * 3 - 1);
                                // 命令域 (1位，指示是否收到数据)
                                FrameCommand = txt.Substring((0 + 1 + 2) * 3, 1 * 3 - 1);
                                // 数据地址域 (8位)
                                FrameAddress = txt.Substring((0 + 1 + 2 + 1) * 3, 8 * 3 - 1);
                                // 非解析帧 (9位)
                                FrameUnparsed = txt.Substring((0 + 1 + 2 + 1 + 8) * 3, 9 * 3 - 1);
                                // 数据内容域 (去掉头21位，尾1位)
                                FrameContent = txt.Substring(21 * 3, txt.Length - 21 * 3 - 3);
                                // 校验码 (1位)
                                FrameCyclicRedundancyCheck = txt.Substring(txt.Length - 2, 2);
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
                StatusTextBlock.Text = "文本解析出错！";
            }
        }

        /// <summary>
        ///     仪表参数解析面板显示功能
        /// </summary>
        /// <param name="txt"></param>
        private void ShowParseParameter(string txt)
        {
            // 面板清空
            ParseParameterClear();
            // 仪表参数解析面板写入
            try
            {
                switch (txt.Substring(0, 2))
                {
                    case "FE":
                        {
                            //字符串校验
                            var j = CalCheckCode_FE(txt);
                            if (j == FrameCyclicRedundancyCheck)
                            {
                                ResCyclicRedundancyCheck.Text = "通过";
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
                                        ResProtocol.Text = "ZigBee SZ9-GRM V3.01油田专用通讯协议（国产四信）";
                                        ResProtocolDockPanel.Visibility = Visibility.Visible;
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
                                        StatusTextBlock.Text = "通信协议解析出错！";
                                        TurnOnButton.IsChecked = false;
                                        return;
                                    }

                                    // 网络地址
                                    try
                                    {
                                        var frameContentAddress =
                                            (FrameAddress.Substring(3, 2) + FrameAddress.Substring(0, 2)).Replace(" ", "");
                                        var intFrameContentAddress = Convert.ToInt32(frameContentAddress, 16);
                                        ResAddress.Text = intFrameContentAddress.ToString();
                                        ResAddress.Visibility = Visibility.Visible;
                                    }
                                    catch (Exception ex)
                                    {
                                        var str = ex.StackTrace;
                                        Console.WriteLine(str);
                                        // 异常时显示提示文字
                                        StatusTextBlock.Text = "网络地址解析出错！";
                                        TurnOnButton.IsChecked = false;
                                        return;
                                    }

                                    // 厂商号
                                    try
                                    {
                                        var frameContentVendor = FrameContent.Substring(6, 5).Replace(" ", "");
                                        var intFrameContentVendor = Convert.ToInt32(frameContentVendor, 16);
                                        // 1 0x0001 厂商1
                                        // 2 0x0002 厂商2
                                        // 3 0x0003 厂商3
                                        // 4 ......
                                        // N 0x8001~0xFFFF 预留
                                        if (intFrameContentVendor > 0x0000 && intFrameContentVendor < 0x8001)
                                        {
                                            ResVendor.Text = "厂商" + intFrameContentVendor;
                                        }
                                        else if (intFrameContentVendor >= 0x8001 && intFrameContentVendor <= 0xFFFF)
                                        {
                                            ResVendor.Text = "预留厂商";
                                        }
                                        else
                                        {
                                            ResVendor.Text = "未定义";
                                            ResVendor.Foreground = new SolidColorBrush(Colors.Red);
                                        }

                                        ResVendorDockPanel.Visibility = Visibility.Visible;
                                    }
                                    catch (Exception ex)
                                    {
                                        var str = ex.StackTrace;
                                        Console.WriteLine(str);
                                        // 异常时显示提示文字
                                        StatusTextBlock.Text = "厂商号解析出错！";
                                        TurnOnButton.IsChecked = false;
                                        return;
                                    }

                                    // 仪表类型
                                    try
                                    {
                                        var frameContentType = FrameContent.Substring(12, 5).Replace(" ", "");
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
                                                ResType.Text = "无线一体化负荷";
                                                break;
                                            case 0x0002:
                                                ResType.Text = "无线压力";
                                                break;
                                            case 0x0003:
                                                ResType.Text = "无线温度";
                                                break;
                                            case 0x0004:
                                                ResType.Text = "无线电量";
                                                break;
                                            case 0x0005:
                                                ResType.Text = "无线角位移";
                                                break;
                                            case 0x0006:
                                                ResType.Text = "无线载荷";
                                                break;
                                            case 0x0007:
                                                ResType.Text = "无线扭矩";
                                                break;
                                            case 0x0008:
                                                ResType.Text = "无线动液面";
                                                break;
                                            case 0x0009:
                                                ResType.Text = "计量车";
                                                break;
                                            case 0x000B:
                                                ResType.Text = "无线压力温度一体化变送器";
                                                break;
                                            case 0x1F00:
                                                ResType.Text = "控制器(RTU)设备";
                                                break;
                                            case 0x1F10:
                                                ResType.Text = "手操器";
                                                break;
                                            // 自定义
                                            case 0x2000:
                                                ResType.Text = "温度型";
                                                break;
                                            case 0x3000:
                                                ResType.Text = "无线拉线位移校准传感器";
                                                break;
                                            case 0x3001:
                                                ResType.Text = "无线拉线位移功图校准传感器";
                                                break;
                                            default:
                                                ResType.Clear();
                                                break;
                                        }

                                        while (ResType.Text.Trim() == string.Empty)
                                            if (intFrameContentType <= 0x4000 && intFrameContentType >= 0x3000)
                                            {
                                                ResType.Text = "自定义";
                                            }
                                            else
                                            {
                                                ResType.Text = "未定义";
                                                ResType.Foreground = new SolidColorBrush(Colors.Red);
                                            }

                                        ResTypeDockPanel.Visibility = Visibility.Visible;
                                    }
                                    catch (Exception ex)
                                    {
                                        var str = ex.StackTrace;
                                        Console.WriteLine(str);
                                        // 异常时显示提示文字
                                        StatusTextBlock.Text = "仪表类型解析出错！";
                                        TurnOnButton.IsChecked = false;
                                        return;
                                    }

                                    // 仪表组号
                                    try
                                    {
                                        ResGroup.Text =
                                            Convert.ToInt32(FrameContent.Substring(18, 2).Replace(" ", ""), 16) + "组" +
                                            Convert.ToInt32(FrameContent.Substring(21, 2).Replace(" ", ""), 16) + "号";
                                        ResGroup.Visibility = Visibility.Visible;
                                    }
                                    catch (Exception ex)
                                    {
                                        var str = ex.StackTrace;
                                        Console.WriteLine(str);
                                        // 异常时显示提示文字
                                        StatusTextBlock.Text = "仪表组号解析出错！";
                                        TurnOnButton.IsChecked = false;
                                        return;
                                    }

                                    // 数据类型
                                    try
                                    {
                                        var frameContentFunctionData = FrameContent.Substring(24, 5).Replace(" ", "");
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
                                                ResFunctionData.Text = "常规数据（仪表实时数据）";
                                                if ((txt.Length + 1) / 3 == 27)
                                                {
                                                    // 无线仪表数据段
                                                    // 通信效率
                                                    try
                                                    {
                                                        ResSucRate.Text =
                                                            Convert.ToInt32(FrameContent.Substring(30, 2), 16) + "%";
                                                        ResSucRateDockPanel.Visibility = Visibility.Visible;
                                                    }
                                                    catch (Exception ex)
                                                    {
                                                        var str = ex.StackTrace;
                                                        Console.WriteLine(str);
                                                        // 异常时显示提示文字
                                                        StatusTextBlock.Text = "通信效率解析出错！";
                                                        TurnOnButton.IsChecked = false;
                                                        return;
                                                    }

                                                    // 电池电压
                                                    try
                                                    {
                                                        ResBatVol.Text =
                                                            Convert.ToInt32(FrameContent.Substring(33, 2), 16) + "%";
                                                        ResBatVolDockPanel.Visibility = Visibility.Visible;
                                                    }
                                                    catch (Exception ex)
                                                    {
                                                        var str = ex.StackTrace;
                                                        Console.WriteLine(str);
                                                        // 异常时显示提示文字
                                                        StatusTextBlock.Text = "电池电压解析出错！";
                                                        TurnOnButton.IsChecked = false;
                                                        return;
                                                    }

                                                    // 休眠时间
                                                    try
                                                    {
                                                        var sleepTime =
                                                            Convert.ToInt32(FrameContent.Substring(36, 5).Replace(" ", ""),
                                                                16);
                                                        ResSleepTime.Text = sleepTime + "秒";
                                                        ResSleepTimeDockPanel.Visibility = Visibility.Visible;
                                                        if (RegularDataUpdateRate.Text == "")
                                                            RegularDataUpdateRate.Text = Convert.ToString(sleepTime);
                                                    }
                                                    catch (Exception ex)
                                                    {
                                                        var str = ex.StackTrace;
                                                        Console.WriteLine(str);
                                                        // 异常时显示提示文字
                                                        StatusTextBlock.Text = "休眠时间解析出错！";
                                                        TurnOnButton.IsChecked = false;
                                                        return;
                                                    }

                                                    // 仪表状态
                                                    try
                                                    {
                                                        var frameStatue = FrameContent.Substring(42, 5).Replace(" ", "");
                                                        var binFrameStatue = Convert
                                                            .ToString(Convert.ToInt32(frameStatue, 16), 2).PadLeft(8, '0');
                                                        if (Convert.ToInt32(binFrameStatue.Replace(" ", ""), 2) != 0)
                                                        {
                                                            ResStatue.Text = "故障";
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
                                                                // 从第0位到第7位
                                                                if (binFrameStatue.Substring(a, 1) == "1")
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

                                                            StatusTextBlock.Text = failureMessage;
                                                            //string messageBoxText = "设备上报" + count + "个故障: \n" + failureMessage;
                                                            //string caption = "设备故障";
                                                            //MessageBoxButton button = MessageBoxButton.OK;
                                                            //MessageBoxImage icon = MessageBoxImage.Error;
                                                            //MessageBox.Show(messageBoxText, caption, button, icon);
                                                        }
                                                        else
                                                        {
                                                            ResStatue.Text = "正常";
                                                        }

                                                        ResStatueDockPanel.Visibility = Visibility.Visible;
                                                    }
                                                    catch (Exception ex)
                                                    {
                                                        var str = ex.StackTrace;
                                                        Console.WriteLine(str);
                                                        // 异常时显示提示文字
                                                        StatusTextBlock.Text = "仪表状态解析出错！";
                                                        TurnOnButton.IsChecked = false;
                                                        return;
                                                    }

                                                    // 运行时间
                                                    try
                                                    {
                                                        ResTime.Text =
                                                            Convert.ToInt32(FrameContent.Substring(48, 5).Replace(" ", ""),
                                                                16) + "小时";
                                                        ResTimeDockPanel.Visibility = Visibility.Visible;
                                                    }
                                                    catch (Exception ex)
                                                    {
                                                        var str = ex.StackTrace;
                                                        Console.WriteLine(str);
                                                        // 异常时显示提示文字
                                                        StatusTextBlock.Text = "运行时间解析出错！";
                                                        TurnOnButton.IsChecked = false;
                                                        return;
                                                    }

                                                    // 实时数据
                                                    try
                                                    {
                                                        var frameResData = Convert
                                                            .ToInt32(FrameContent.Substring(54, 5).Replace(" ", ""), 16).ToString();
                                                        ResData.Text = frameResData;
                                                        ResDataDockPanel.Visibility = Visibility.Visible;
                                                        try
                                                        {
                                                            RealTimeData = Convert.ToDouble(frameResData);
                                                        }
                                                        catch (Exception ex)
                                                        {
                                                            var str = ex.StackTrace;
                                                            Console.WriteLine(str);
                                                        }
                                                        // 作图
                                                        AnimatedPlot();
                                                        // 写数据库
                                                        if (SqlConnectButton.IsChecked == true
                                                            && AutoSaveToSqlCheckBox.IsChecked.Value)
                                                        {
                                                            string type = ResType.Text, address = ResAddress.Text,
                                                                protocol = ResProtocol.Text, data = ResData.Text,
                                                                statue = ResStatue.Text, workSheet = WorkSheet;
                                                            SqlConnection sql = CalibrationSqlConnect;
                                                            DatabaseWrite(type, protocol, address, data, statue, workSheet, sql);
                                                        }
                                                    }
                                                    catch (Exception ex)
                                                    {
                                                        var str = ex.StackTrace;
                                                        Console.WriteLine(str);
                                                        // 异常时显示提示文字
                                                        StatusTextBlock.Text = "实时数据解析出错！";
                                                        TurnOnButton.IsChecked = false;
                                                        return;
                                                    }
                                                }

                                                break;
                                            case 0x0010:
                                                ResFunctionData.Text = "常规数据（仪表基本参数）";
                                                if ((txt.Length + 1) / 3 == 81)
                                                {
                                                    // 无线仪表数据段
                                                    // 仪表型号
                                                    try
                                                    {
                                                        ResModel.Text = FrameContent.Substring(30, 23);
                                                        ResModelDockPanel.Visibility = Visibility.Visible;
                                                    }
                                                    catch (Exception ex)
                                                    {
                                                        var str = ex.StackTrace;
                                                        Console.WriteLine(str);
                                                        // 异常时显示提示文字
                                                        StatusTextBlock.Text = "仪表型号解析出错！";
                                                        TurnOnButton.IsChecked = false;
                                                        return;
                                                    }

                                                    // 系列号
                                                    try
                                                    {
                                                        ResFirmwareVersion.Text = FrameContent.Substring(54, 47);
                                                        ResFirmwareVersionDockPanel.Visibility = Visibility.Visible;
                                                    }
                                                    catch (Exception ex)
                                                    {
                                                        var str = ex.StackTrace;
                                                        Console.WriteLine(str);
                                                        // 异常时显示提示文字
                                                        StatusTextBlock.Text = "系列号解析出错！";
                                                        TurnOnButton.IsChecked = false;
                                                        return;
                                                    }

                                                    // 固件版本
                                                    try
                                                    {
                                                        ResFirmwareVersion.Text = FrameContent.Substring(102, 5);
                                                        ResFirmwareVersionDockPanel.Visibility = Visibility.Visible;
                                                    }
                                                    catch (Exception ex)
                                                    {
                                                        var str = ex.StackTrace;
                                                        Console.WriteLine(str);
                                                        // 异常时显示提示文字
                                                        StatusTextBlock.Text = "固件版本解析出错！";
                                                        TurnOnButton.IsChecked = false;
                                                        return;
                                                    }

                                                    // 软件版本
                                                    try
                                                    {
                                                        ResSoftwareVersion.Text = FrameContent.Substring(108, 5);
                                                        ResSoftwareVersionDockPanel.Visibility = Visibility.Visible;
                                                    }
                                                    catch (Exception ex)
                                                    {
                                                        var str = ex.StackTrace;
                                                        Console.WriteLine(str);
                                                        // 异常时显示提示文字
                                                        StatusTextBlock.Text = "软件版本解析出错！";
                                                        TurnOnButton.IsChecked = false;
                                                        return;
                                                    }

                                                    // 量程下限
                                                    try
                                                    {
                                                        ResLowRange.Text = FrameContent.Substring(114, 5);
                                                        ResLowRangeDockPanel.Visibility = Visibility.Visible;
                                                    }
                                                    catch (Exception ex)
                                                    {
                                                        var str = ex.StackTrace;
                                                        Console.WriteLine(str);
                                                        // 异常时显示提示文字
                                                        StatusTextBlock.Text = "量程下限解析出错！";
                                                        TurnOnButton.IsChecked = false;
                                                        return;
                                                    }

                                                    // 量程上限
                                                    try
                                                    {
                                                        ResHighRange.Text = FrameContent.Substring(120, 5);
                                                        ResHighRangeDockPanel.Visibility = Visibility.Visible;
                                                    }
                                                    catch (Exception ex)
                                                    {
                                                        var str = ex.StackTrace;
                                                        Console.WriteLine(str);
                                                        // 异常时显示提示文字
                                                        StatusTextBlock.Text = "量程上限解析出错！";
                                                        TurnOnButton.IsChecked = false;
                                                        return;
                                                    }

                                                    // 测量精度
                                                    try
                                                    {
                                                        ResMeasurementAccuracy.Text = FrameContent.Substring(126, 5);
                                                        ResMeasurementAccuracyDockPanel.Visibility = Visibility.Visible;
                                                    }
                                                    catch (Exception ex)
                                                    {
                                                        var str = ex.StackTrace;
                                                        Console.WriteLine(str);
                                                        // 异常时显示提示文字
                                                        StatusTextBlock.Text = "测量精度解析出错！";
                                                        TurnOnButton.IsChecked = false;
                                                        return;
                                                    }

                                                    // 防护等级
                                                    try
                                                    {
                                                        ResProtectionLevel.Text = FrameContent.Substring(132, 23);
                                                        ResProtectionLevelDockPanel.Visibility = Visibility.Visible;
                                                    }
                                                    catch (Exception ex)
                                                    {
                                                        var str = ex.StackTrace;
                                                        Console.WriteLine(str);
                                                        // 异常时显示提示文字
                                                        StatusTextBlock.Text = "防护等级解析出错！";
                                                        TurnOnButton.IsChecked = false;
                                                        return;
                                                    }

                                                    // 防爆等级
                                                    try
                                                    {
                                                        ResExplosionProofGrade.Text = FrameContent.Substring(156, 35);
                                                        ResExplosionProofGradeDockPanel.Visibility = Visibility.Visible;
                                                    }
                                                    catch (Exception ex)
                                                    {
                                                        var str = ex.StackTrace;
                                                        Console.WriteLine(str);
                                                        // 异常时显示提示文字
                                                        StatusTextBlock.Text = "防爆等级解析出错！";
                                                        TurnOnButton.IsChecked = false;
                                                        return;
                                                    }

                                                    // 说明
                                                    try
                                                    {
                                                        ResIllustrate.Text = FrameContent.Substring(191, 29);
                                                        ResIllustrateDockPanel.Visibility = Visibility.Visible;
                                                    }
                                                    catch (Exception ex)
                                                    {
                                                        var str = ex.StackTrace;
                                                        Console.WriteLine(str);
                                                        // 异常时显示提示文字
                                                        StatusTextBlock.Text = "说明解析出错！";
                                                        TurnOnButton.IsChecked = false;
                                                        return;
                                                    }
                                                }

                                                break;
                                            case 0x0020:
                                                ResFunctionData.Text = "读数据命令";
                                                break;
                                            case 0x0100:
                                                ResFunctionData.Text = "控制器参数写应答（控制器应答命令）";
                                                break;
                                            case 0x0101:
                                                ResFunctionData.Text = "控制器读仪表参数应答（控制器应答命令）";
                                                break;
                                            case 0x0200:
                                                ResFunctionData.Text = "控制器应答一体化载荷位移示功仪功图参数应答（控制器应答命令触发功图采集）";
                                                break;
                                            case 0x0201:
                                                ResFunctionData.Text = "控制器应答功图数据命令";
                                                break;
                                            case 0x0202:
                                                ResFunctionData.Text = "控制器读功图数据应答（控制器应答命令读已有功图）";
                                                break;
                                            case 0x0300:
                                                ResFunctionData.Text = "控制器(RTU)对仪表控制命令";
                                                break;
                                            default:
                                                ResType.Clear();
                                                break;
                                        }

                                        while (ResType.Text.Trim() == string.Empty)
                                            if (intFrameContentFunctionData >= 0x400 &&
                                                intFrameContentFunctionData <= 0x47f)
                                            {
                                                ResFunctionData.Text = "配置协议命令";
                                            }
                                            else if (intFrameContentFunctionData >= 0x480 &&
                                                     intFrameContentFunctionData <= 0x5ff)
                                            {
                                                ResFunctionData.Text = "标定协议命令";
                                            }
                                            else if (intFrameContentFunctionData >= 0x1000 &&
                                                     intFrameContentFunctionData <= 0x2000)
                                            {
                                                ResFunctionData.Text = "厂家自定义数据类型";
                                            }
                                            else if (intFrameContentFunctionData >= 0x8000 &&
                                                     intFrameContentFunctionData <= 0xffff)
                                            {
                                                ResFunctionData.Text = "预留";
                                            }
                                            else
                                            {
                                                ResFunctionData.Text = "未定义";
                                                ResFunctionData.Foreground = new SolidColorBrush(Colors.Red);
                                                break;
                                            }

                                        ResTypeDockPanel.Visibility = Visibility.Visible;
                                    }
                                    catch (Exception ex)
                                    {
                                        var str = ex.StackTrace;
                                        Console.WriteLine(str);
                                        // 异常时显示提示文字
                                        StatusTextBlock.Text = "数据类型解析出错！";
                                        TurnOnButton.IsChecked = false;
                                        return;
                                    }
                                }
                            }
                            else
                            {
                                ResCyclicRedundancyCheck.Text = "未通过";
                                ResCyclicRedundancyCheck.Foreground = new SolidColorBrush(Colors.Red);
                            }

                            ResCyclicRedundancyCheckDockPanel.Visibility = Visibility.Visible;
                        }
                        break;
                    case "7E":
                        {
                            //字符串校验
                            var calCheckCode = CalCheckCode_7E(txt);
                            if (calCheckCode == FrameCyclicRedundancyCheck)
                            //if (true)
                            {
                                ResCyclicRedundancyCheck.Text = "通过";
                                // 校验成功写入其他解析参数
                                // 无线仪表数据域帧头
                                {
                                    // 通信协议
                                    try
                                    {
                                        ResProtocol.Text = IsLoRaFlag ? "LoRa（Semtech）" : "ZigBee（Digi International）";
                                        ResProtocolDockPanel.Visibility = Visibility.Visible;
                                    }
                                    catch (Exception ex)
                                    {
                                        var str = ex.StackTrace;
                                        Console.WriteLine(str);
                                        // 异常时显示提示文字
                                        StatusTextBlock.Text = "通信协议解析出错！";
                                        TurnOnButton.IsChecked = false;
                                        return;
                                    }

                                    // 网络地址
                                    try
                                    {
                                        if (IsLoRaFlag)
                                        {
                                            ResAddress.Text = "透传模式";
                                            ResAddressDockPanel.Visibility = Visibility.Visible;
                                        }
                                        else
                                        {
                                            var frameContentAddress =
                                                (FrameAddress.Substring(6, 2) + FrameAddress.Substring(3, 2)).Replace(" ",
                                                    "");
                                            var intFrameContentAddress = Convert.ToInt32(frameContentAddress, 16);
                                            ResAddress.Text = intFrameContentAddress.ToString();
                                            ResAddressDockPanel.Visibility = Visibility.Visible;
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        var str = ex.StackTrace;
                                        Console.WriteLine(str);
                                        // 异常时显示提示文字
                                        StatusTextBlock.Text = "网络地址解析出错！";
                                        TurnOnButton.IsChecked = false;
                                        return;
                                    }

                                    // 厂商号
                                    try
                                    {
                                        var frameContentVendor = FrameContent.Substring(6, 5).Replace(" ", "");
                                        var intFrameContentVendor = Convert.ToInt32(frameContentVendor, 16);
                                        // 1 0x0001 厂商1
                                        // 2 0x0002 厂商2
                                        // 3 0x0003 厂商3
                                        // 4 ......
                                        // N 0x8001~0xFFFF 预留
                                        if (intFrameContentVendor > 0x0000 && intFrameContentVendor < 0x8001)
                                        {
                                            ResVendor.Text = "厂商" + intFrameContentVendor;
                                        }
                                        else if (intFrameContentVendor >= 0x8001 && intFrameContentVendor <= 0xFFFF)
                                        {
                                            ResVendor.Text = "预留厂商";
                                        }
                                        else
                                        {
                                            ResVendor.Text = "未定义";
                                            ResVendor.Foreground = new SolidColorBrush(Colors.Red);
                                        }

                                        ResVendorDockPanel.Visibility = Visibility.Visible;
                                    }
                                    catch (Exception ex)
                                    {
                                        var str = ex.StackTrace;
                                        Console.WriteLine(str);
                                        // 异常时显示提示文字
                                        StatusTextBlock.Text = "厂商号解析出错！";
                                        TurnOnButton.IsChecked = false;
                                        return;
                                    }

                                    // 仪表类型
                                    try
                                    {
                                        var frameContentType = FrameContent.Substring(12, 5).Replace(" ", "");
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
                                                ResType.Text = "无线一体化负荷";
                                                break;
                                            case 0x0002:
                                                ResType.Text = "无线压力";
                                                break;
                                            case 0x0003:
                                                ResType.Text = "无线温度";
                                                break;
                                            case 0x0004:
                                                ResType.Text = "无线电量";
                                                break;
                                            case 0x0005:
                                                ResType.Text = "无线角位移";
                                                break;
                                            case 0x0006:
                                                ResType.Text = "无线载荷";
                                                break;
                                            case 0x0007:
                                                ResType.Text = "无线扭矩";
                                                break;
                                            case 0x0008:
                                                ResType.Text = "无线动液面";
                                                break;
                                            case 0x0009:
                                                ResType.Text = "计量车";
                                                break;
                                            case 0x000B:
                                                ResType.Text = "无线压力温度一体化变送器";
                                                break;
                                            case 0x1F00:
                                                ResType.Text = "控制器(RTU)设备";
                                                break;
                                            case 0x1F10:
                                                ResType.Text = "手操器";
                                                break;
                                            // 自定义
                                            case 0x2000:
                                                ResType.Text = "温度型";
                                                break;
                                            case 0x3000:
                                                ResType.Text = "无线拉线位移校准传感器";
                                                break;
                                            case 0x3001:
                                                ResType.Text = "无线拉线位移功图校准传感器";
                                                break;
                                            default:
                                                ResType.Clear();
                                                break;
                                        }

                                        while (ResType.Text.Trim() == string.Empty)
                                            if (intFrameContentType <= 0x4000 && intFrameContentType >= 0x3000)
                                            {
                                                ResType.Text = "自定义";
                                            }
                                            else
                                            {
                                                ResType.Text = "未定义";
                                                ResType.Foreground = new SolidColorBrush(Colors.Red);
                                            }

                                        ResTypeDockPanel.Visibility = Visibility.Visible;
                                    }
                                    catch (Exception ex)
                                    {
                                        var str = ex.StackTrace;
                                        Console.WriteLine(str);
                                        // 异常时显示提示文字
                                        StatusTextBlock.Text = "仪表类型解析出错！";
                                        TurnOnButton.IsChecked = false;
                                        return;
                                    }

                                    // 仪表组号
                                    try
                                    {
                                        ResGroup.Text =
                                            Convert.ToInt32(FrameContent.Substring(18, 2).Replace(" ", ""), 16) + "组" +
                                            Convert.ToInt32(FrameContent.Substring(21, 2).Replace(" ", ""), 16) + "号";
                                        ResGroupDockPanel.Visibility = Visibility.Visible;
                                    }
                                    catch (Exception ex)
                                    {
                                        var str = ex.StackTrace;
                                        Console.WriteLine(str);
                                        // 异常时显示提示文字
                                        StatusTextBlock.Text = "仪表组号解析出错！";
                                        TurnOnButton.IsChecked = false;
                                        return;
                                    }

                                    // 根据不同数据类型解析其他数据
                                    try
                                    {
                                        var frameContentFunctionData = FrameContent.Substring(24, 5).Replace(" ", "");
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
                                                ResFunctionData.Text = "常规数据（仪表实时数据）";
                                                if ((txt.Length + 1) / 3 == 42 ||
                                                    (txt.Length + 1) / 3 == 24)
                                                {
                                                    // 无线仪表数据段
                                                    // 通信效率
                                                    try
                                                    {
                                                        ResSucRate.Text =
                                                            Convert.ToInt32(FrameContent.Substring(30, 2), 16) + "%";
                                                        ResSucRateDockPanel.Visibility = Visibility.Visible;
                                                    }
                                                    catch (Exception ex)
                                                    {
                                                        var str = ex.StackTrace;
                                                        Console.WriteLine(str);
                                                        // 异常时显示提示文字
                                                        StatusTextBlock.Text = "通信效率解析出错！";
                                                        TurnOnButton.IsChecked = false;
                                                        return;
                                                    }

                                                    // 电池电压
                                                    try
                                                    {
                                                        ResBatVol.Text =
                                                            Convert.ToInt32(FrameContent.Substring(33, 2), 16) + "%";
                                                        ResBatVolDockPanel.Visibility = Visibility.Visible;
                                                    }
                                                    catch (Exception ex)
                                                    {
                                                        var str = ex.StackTrace;
                                                        Console.WriteLine(str);
                                                        // 异常时显示提示文字
                                                        StatusTextBlock.Text = "电池电压解析出错！";
                                                        TurnOnButton.IsChecked = false;
                                                        return;
                                                    }

                                                    // 休眠时间
                                                    try
                                                    {
                                                        var sleepTime =
                                                            Convert.ToInt32(FrameContent.Substring(36, 5).Replace(" ", ""),
                                                                16);
                                                        ResSleepTime.Text = sleepTime + "秒";
                                                        ResSleepTimeDockPanel.Visibility = Visibility.Visible;
                                                        if (RegularDataUpdateRate.Text == "")
                                                            RegularDataUpdateRate.Text = Convert.ToString(sleepTime);
                                                    }
                                                    catch (Exception ex)
                                                    {
                                                        var str = ex.StackTrace;
                                                        Console.WriteLine(str);
                                                        // 异常时显示提示文字
                                                        StatusTextBlock.Text = "休眠时间解析出错！";
                                                        TurnOnButton.IsChecked = false;
                                                        return;
                                                    }

                                                    // 仪表状态
                                                    try
                                                    {
                                                        var frameStatue = FrameContent.Substring(42, 5).Replace(" ", "");
                                                        var binFrameStatue = Convert
                                                            .ToString(Convert.ToInt32(frameStatue, 16), 2).PadLeft(8, '0');
                                                        if (Convert.ToInt32(binFrameStatue.Replace(" ", ""), 2) != 0)
                                                        {
                                                            ResStatue.Text = "故障";
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
                                                                // 从第0位到第7位
                                                                if (binFrameStatue.Substring(a, 1) == "1")
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

                                                            StatusTextBlock.Text = failureMessage;
                                                            //string messageBoxText = "设备上报" + count + "个故障: \n" + failureMessage;
                                                            //string caption = "设备故障";
                                                            //MessageBoxButton button = MessageBoxButton.OK;
                                                            //MessageBoxImage icon = MessageBoxImage.Error;
                                                            //MessageBox.Show(messageBoxText, caption, button, icon);
                                                        }
                                                        else
                                                        {
                                                            ResStatue.Text = "正常";
                                                        }

                                                        ResStatueDockPanel.Visibility = Visibility.Visible;
                                                    }
                                                    catch (Exception ex)
                                                    {
                                                        var str = ex.StackTrace;
                                                        Console.WriteLine(str);
                                                        // 异常时显示提示文字
                                                        StatusTextBlock.Text = "仪表状态解析出错！";
                                                        TurnOnButton.IsChecked = false;
                                                        return;
                                                    }

                                                    // 运行时间
                                                    try
                                                    {
                                                        ResTime.Text =
                                                            Convert.ToInt32(FrameContent.Substring(48, 5).Replace(" ", ""),
                                                                16) + "小时";
                                                        ResTimeDockPanel.Visibility = Visibility.Visible;
                                                    }
                                                    catch (Exception ex)
                                                    {
                                                        var str = ex.StackTrace;
                                                        Console.WriteLine(str);
                                                        // 异常时显示提示文字
                                                        StatusTextBlock.Text = "运行时间解析出错！";
                                                        TurnOnButton.IsChecked = false;
                                                        return;
                                                    }

                                                    // 实时数据
                                                    try
                                                    {
                                                        //string frameResData = frameContent.Substring(54, 5).Replace(" ", "").TrimStart('0');
                                                        //resData.Text = Convert.ToInt32(frameResData, 16) + "MPa";
                                                        // 十六进制字符串转换为浮点数字符串
                                                        var frameResData =
                                                            FrameContent.Substring(48, 11).Replace(" ", "");
                                                        var flFrameData = HexStrToFloat(frameResData);
                                                        RealTimeData = flFrameData;
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
                                                        ResData.Text = flFrameData.ToString(CultureInfo.InvariantCulture);
                                                        ResDataDockPanel.Visibility = Visibility.Visible;
                                                        // 作图
                                                        AnimatedPlot();
                                                        // 写数据库
                                                        if (SqlConnectButton.IsChecked == true && AutoSaveToSqlCheckBox.IsChecked.Value)
                                                        {
                                                            string type = ResType.Text, address = ResAddress.Text, protocol = ResProtocol.Text, data = ResData.Text, statue = ResStatue.Text, workSheet = WorkSheet;
                                                            SqlConnection sql = CalibrationSqlConnect;
                                                            DatabaseWrite(type, protocol, address, data, statue, workSheet, sql);
                                                        }
                                                    }
                                                    catch (Exception ex)
                                                    {
                                                        var str = ex.StackTrace;
                                                        Console.WriteLine(str);
                                                        // 异常时显示提示文字
                                                        StatusTextBlock.Text = "实时数据解析出错！";
                                                        TurnOnButton.IsChecked = false;
                                                        return;
                                                    }
                                                }

                                                break;
                                            case 0x0010:
                                                ResFunctionData.Text = "常规数据（仪表基本参数）";
                                                if ((txt.Length + 1) / 3 == 96 ||
                                                    (txt.Length + 1) / 3 == 78)
                                                {
                                                    // 无线仪表数据段
                                                    // 仪表型号
                                                    try
                                                    {
                                                        ResModel.Text = FrameContent.Substring(30, 23);
                                                        ResModelDockPanel.Visibility = Visibility.Visible;
                                                    }
                                                    catch (Exception ex)
                                                    {
                                                        var str = ex.StackTrace;
                                                        Console.WriteLine(str);
                                                        // 异常时显示提示文字
                                                        StatusTextBlock.Text = "仪表型号解析出错！";
                                                        TurnOnButton.IsChecked = false;
                                                        return;
                                                    }

                                                    // 系列号
                                                    try
                                                    {
                                                        ResFirmwareVersion.Text = FrameContent.Substring(54, 47);
                                                        ResFirmwareVersionDockPanel.Visibility = Visibility.Visible;
                                                    }
                                                    catch (Exception ex)
                                                    {
                                                        var str = ex.StackTrace;
                                                        Console.WriteLine(str);
                                                        // 异常时显示提示文字
                                                        StatusTextBlock.Text = "系列号解析出错！";
                                                        TurnOnButton.IsChecked = false;
                                                        return;
                                                    }

                                                    // 固件版本
                                                    try
                                                    {
                                                        ResFirmwareVersion.Text = FrameContent.Substring(102, 5);
                                                        ResFirmwareVersionDockPanel.Visibility = Visibility.Visible;
                                                    }
                                                    catch (Exception ex)
                                                    {
                                                        var str = ex.StackTrace;
                                                        Console.WriteLine(str);
                                                        // 异常时显示提示文字
                                                        StatusTextBlock.Text = "固件版本解析出错！";
                                                        TurnOnButton.IsChecked = false;
                                                        return;
                                                    }

                                                    // 软件版本
                                                    try
                                                    {
                                                        ResSoftwareVersion.Text = FrameContent.Substring(108, 5);
                                                        ResSoftwareVersionDockPanel.Visibility = Visibility.Visible;
                                                    }
                                                    catch (Exception ex)
                                                    {
                                                        var str = ex.StackTrace;
                                                        Console.WriteLine(str);
                                                        // 异常时显示提示文字
                                                        StatusTextBlock.Text = "软件版本解析出错！";
                                                        TurnOnButton.IsChecked = false;
                                                        return;
                                                    }

                                                    // 量程下限
                                                    try
                                                    {
                                                        ResLowRange.Text = FrameContent.Substring(114, 5);
                                                        ResLowRangeDockPanel.Visibility = Visibility.Visible;
                                                    }
                                                    catch (Exception ex)
                                                    {
                                                        var str = ex.StackTrace;
                                                        Console.WriteLine(str);
                                                        // 异常时显示提示文字
                                                        StatusTextBlock.Text = "量程下限解析出错！";
                                                        TurnOnButton.IsChecked = false;
                                                        return;
                                                    }

                                                    // 量程上限
                                                    try
                                                    {
                                                        ResHighRange.Text = FrameContent.Substring(120, 5);
                                                        ResHighRangeDockPanel.Visibility = Visibility.Visible;
                                                    }
                                                    catch (Exception ex)
                                                    {
                                                        var str = ex.StackTrace;
                                                        Console.WriteLine(str);
                                                        // 异常时显示提示文字
                                                        StatusTextBlock.Text = "量程上限解析出错！";
                                                        TurnOnButton.IsChecked = false;
                                                        return;
                                                    }

                                                    // 测量精度
                                                    try
                                                    {
                                                        ResMeasurementAccuracy.Text = FrameContent.Substring(126, 5);
                                                        ResMeasurementAccuracyDockPanel.Visibility = Visibility.Visible;
                                                    }
                                                    catch (Exception ex)
                                                    {
                                                        var str = ex.StackTrace;
                                                        Console.WriteLine(str);
                                                        // 异常时显示提示文字
                                                        StatusTextBlock.Text = "测量精度解析出错！";
                                                        TurnOnButton.IsChecked = false;
                                                        return;
                                                    }

                                                    // 防护等级
                                                    try
                                                    {
                                                        ResProtectionLevel.Text = FrameContent.Substring(132, 23);
                                                        ResProtectionLevelDockPanel.Visibility = Visibility.Visible;
                                                    }
                                                    catch (Exception ex)
                                                    {
                                                        var str = ex.StackTrace;
                                                        Console.WriteLine(str);
                                                        // 异常时显示提示文字
                                                        StatusTextBlock.Text = "防护等级解析出错！";
                                                        TurnOnButton.IsChecked = false;
                                                        return;
                                                    }

                                                    // 防爆等级
                                                    try
                                                    {
                                                        ResExplosionProofGrade.Text = FrameContent.Substring(156, 35);
                                                        ResExplosionProofGradeDockPanel.Visibility = Visibility.Visible;
                                                    }
                                                    catch (Exception ex)
                                                    {
                                                        var str = ex.StackTrace;
                                                        Console.WriteLine(str);
                                                        // 异常时显示提示文字
                                                        StatusTextBlock.Text = "防爆等级解析出错！";
                                                        TurnOnButton.IsChecked = false;
                                                        return;
                                                    }

                                                    // 说明
                                                    try
                                                    {
                                                        ResIllustrate.Text = FrameContent.Substring(191, 29);
                                                        ResIllustrateDockPanel.Visibility = Visibility.Visible;
                                                    }
                                                    catch (Exception ex)
                                                    {
                                                        var str = ex.StackTrace;
                                                        Console.WriteLine(str);
                                                        // 异常时显示提示文字
                                                        StatusTextBlock.Text = "说明解析出错！";
                                                        TurnOnButton.IsChecked = false;
                                                        return;
                                                    }
                                                }

                                                break;
                                            case 0x0020:
                                                ResFunctionData.Text = "读数据命令";
                                                break;
                                            case 0x0100:
                                                ResFunctionData.Text = "控制器参数写应答（控制器应答命令）";
                                                break;
                                            case 0x0101:
                                                ResFunctionData.Text = "控制器读仪表参数应答（控制器应答命令）";
                                                break;
                                            case 0x0200:
                                                ResFunctionData.Text = "控制器应答一体化载荷位移示功仪功图参数应答（控制器应答命令触发功图采集）";
                                                break;
                                            case 0x0201:
                                                ResFunctionData.Text = "控制器应答功图数据命令";
                                                break;
                                            case 0x0202:
                                                ResFunctionData.Text = "控制器读功图数据应答（控制器应答命令读已有功图）";
                                                break;
                                            case 0x0300:
                                                ResFunctionData.Text = "控制器(RTU)对仪表控制命令";
                                                break;
                                            default:
                                                ResType.Clear();
                                                break;
                                        }

                                        while (ResType.Text.Trim() == string.Empty)
                                            if (intFrameContentFunctionData >= 0x400 &&
                                                intFrameContentFunctionData <= 0x47f)
                                            {
                                                ResFunctionData.Text = "配置协议命令";
                                            }
                                            else if (intFrameContentFunctionData >= 0x480 &&
                                                     intFrameContentFunctionData <= 0x5ff)
                                            {
                                                ResFunctionData.Text = "标定协议命令";
                                            }
                                            else if (intFrameContentFunctionData >= 0x1000 &&
                                                     intFrameContentFunctionData <= 0x2000)
                                            {
                                                ResFunctionData.Text = "厂家自定义数据类型";
                                            }
                                            else if (intFrameContentFunctionData >= 0x8000 &&
                                                     intFrameContentFunctionData <= 0xffff)
                                            {
                                                ResFunctionData.Text = "预留";
                                            }
                                            else
                                            {
                                                ResFunctionData.Text = "未定义";
                                                ResFunctionData.Foreground = new SolidColorBrush(Colors.Red);
                                                break;
                                            }

                                        ResFunctionDataDockPanel.Visibility = Visibility.Visible;
                                    }
                                    catch (Exception ex)
                                    {
                                        var str = ex.StackTrace;
                                        Console.WriteLine(str);
                                        // 异常时显示提示文字
                                        StatusTextBlock.Text = "数据类型解析出错！";
                                        TurnOnButton.IsChecked = false;
                                        return;
                                    }
                                }
                            }
                            else
                            {
                                ResCyclicRedundancyCheck.Text = "未通过";
                                ResCyclicRedundancyCheck.Foreground = new SolidColorBrush(Colors.Red);
                            }

                            ResCyclicRedundancyCheckDockPanel.Visibility = Visibility.Visible;
                        }
                        break;
                    default:
                        ResProtocol.Text = "未知";
                        ResAddress.Foreground = new SolidColorBrush(Colors.Red);
                        break;
                }
            }
            catch (Exception ex)
            {
                var str = ex.StackTrace;
                Console.WriteLine(str);
                // 异常时显示提示文字
                StatusTextBlock.Text = "参数解析出错！";
                TurnOnButton.IsChecked = false;
            }
            // 更新数据曲线
            //string strConn = @"Data Source=.;Initial Catalog=Test; integrated security=True;";
            //SqlConnection conn = new SqlConnection(strConn);
        }

        private static string CalCheckCode_7E(string text)
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
            foreach (var hex in hexvalue) j = HexStrXor(j, hex);
            return j.ToUpper().PadLeft(2, '0');
        }

        /// <summary>
        ///     接收窗口清空按钮
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ClearReceiveButton_Click(object sender, RoutedEventArgs e)
        {
            DisplayTextBox.Clear();
        }

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
            SendTextBox.SelectionStart = SendTextBox.Text.Length;
            //MatchCollection hexadecimalCollection = Regex.Matches(e.Text, @"[(0(X|x))?\da-fA-F]");
            var hexadecimalCollection = Regex.Matches(e.Text, @"[\da-fA-F]");
            foreach (Match mat in hexadecimalCollection) SendTextBox.AppendText(mat.Value);

            // 每输入两个字符自动添加空格
            SendTextBox.Text = SendTextBox.Text.Replace(" ", "");
            SendTextBox.Text = string.Join(" ", Regex.Split(SendTextBox.Text, "(?<=\\G.{2})(?!$)"));
            SendTextBox.SelectionStart = SendTextBox.Text.Length;
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
                NewSerialPort.DiscardOutBuffer();
            }
            catch (Exception ex)
            {
                var std = ex.StackTrace;
                Console.WriteLine(std);
            }

            if (!NewSerialPort.IsOpen)
            {
                StatusTextBlock.Text = "请先打开串口！";
                return;
            }

            // 去掉十六进制前缀
            var sendData = CleanHexString(SendTextBox.Text);

            // 十六进制数据发送
            try
            {
                // 分割字符串
                var strArray = sendData.Split(' ');
                // 写入数据缓冲区
                var sendBuffer = new byte[strArray.Length];
                var i = 0;
                foreach (var str in strArray)
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
                        NewSerialPort.DiscardOutBuffer();
                        MessageBox.Show("字节越界，请逐个字节输入！", "Error");
                        AutoSendCheckBox.IsChecked = false; // 关闭自动发送
                    }

                //foreach (byte b in sendBuffer)
                //{
                //    Console.Write(b.ToString("X2"));
                //}
                //Console.WriteLine("");
                try
                {
                    NewSerialPort.Write(sendBuffer, 0, sendBuffer.Length);
                }
                catch (Exception ex)
                {
                    var str = ex.StackTrace;
                    Console.WriteLine(str);
                    StatusTextBlock.Text = "串口异常";
                }

                // 更新发送数据计数
                SendBytesCount += (uint)sendBuffer.Length;
                StatusSendByteTextBlock.Text = SendBytesCount.ToString();
            }
            catch (Exception ex)
            {
                var str = ex.StackTrace;
                Console.WriteLine(str);
                StatusTextBlock.Text = "当前为16进制发送模式，请输入16进制数据";
                AutoSendCheckBox.IsChecked = false; // 关闭自动发送
            }
        }

        private void SerialPortSend(string hexStr)
        {
            //sendCount++;
            //Console.WriteLine("发送" + sendCount + "次");
            // 清空发送缓冲区
            NewSerialPort.DiscardOutBuffer();
            if (!NewSerialPort.IsOpen)
            {
                StatusTextBlock.Text = "请先打开串口！";
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
                        NewSerialPort.DiscardOutBuffer();
                        MessageBox.Show("字节越界，请逐个字节输入！", "Error");
                        AutoSendCheckBox.IsChecked = false; // 关闭自动发送
                    }

                //foreach (byte b in sendBuffer)
                //{
                //    Console.Write(b.ToString("X2"));
                //}
                //Console.WriteLine("");
                try
                {
                    NewSerialPort.Write(sendBuffer, 0, sendBuffer.Length);
                }
                catch (Exception ex)
                {
                    var str = ex.StackTrace;
                    Console.WriteLine(str);
                    StatusTextBlock.Text = "串口异常";
                }

                // 更新发送数据计数
                SendBytesCount += (uint)sendBuffer.Length;
                StatusSendByteTextBlock.Text = SendBytesCount.ToString();
            }
            catch (Exception ex)
            {
                var str = ex.StackTrace;
                Console.WriteLine(str);
                StatusTextBlock.Text = "当前为16进制发送模式，请输入16进制数据";
                AutoSendCheckBox.IsChecked = false; // 关闭自动发送
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
        private void SendButton_Click(object sender, RoutedEventArgs e)
        {
            SerialPortSend();
        }

        /// <summary>
        ///     自动发送开启
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void AutoSendCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            AutoSendTimer.Start();
        }

        /// <summary>
        ///     在每个自动发送周期执行
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void AutoSendTimer_Tick(object sender, EventArgs e)
        {
            AutoSendTimer.Interval = new TimeSpan(0, 0, 0, 0, Convert.ToInt32(AutoSendCycleTextBox.Text));
            // 发送数据
            SerialPortSend();
            // 设置新的定时时间           
            // autoSendTimer.Interval = new TimeSpan(0, 0, 0, 0, Convert.ToInt32(autoSendCycleTextBox.Text));
        }

        /// <summary>
        ///     自动发送关闭
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void AutoSendCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            AutoSendTimer.Stop();
        }

        /// <summary>
        ///     清空发送区
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ClearSendButton_Click(object sender, RoutedEventArgs e)
        {
            SendTextBox.Clear();
        }

        /// <summary>
        ///     清空计数器
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CountClearButton_Click(object sender, RoutedEventArgs e)
        {
            // 接收、发送计数清零
            ReceiveBytesCount = 0;
            SendBytesCount = 0;
            // 更新数据显示
            StatusReceiveByteTextBlock.Text = ReceiveBytesCount.ToString();
            StatusSendByteTextBlock.Text = SendBytesCount.ToString();
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
                switch (FrameHeader)
                {
                    case "FE":
                        {
                            // 获取所需解析数据
                            ParameterAcquisition_FE(out var strHeader, out var strCommand, out var strAddress,
                                out var strProtocolVendor, out var strHandler, out var strGroup);
                            // 功能码 / 数据类型
                            var strFunctionData = "01 00";
                            // 写操作数据区
                            string strHandlerContent;
                            if (RegularDataUpdateRate.Text != "")
                                strHandlerContent = Convert.ToString(Convert.ToInt32(RegularDataUpdateRate.Text), 16)
                                    .ToUpper().PadLeft(4, '0').Insert(2, " ");
                            else if (ResSleepTime.Text != "")
                                strHandlerContent = FrameContent.Substring(36, 5);
                            else
                                strHandlerContent = "00 00";

                            // 合成数据域
                            var strContent = strAddress + " " + strProtocolVendor + " " + strHandler + " " + strGroup +
                                             " " + strFunctionData + " " + strHandlerContent;
                            // 计算长度域（不包含命令域）
                            var intLength = (strContent.Length + 1) / 3;
                            var strLength = Convert.ToString(intLength, 16).ToUpper().PadLeft(2, '0');
                            var strInner = strLength + " " + strCommand + " " + strContent;
                            // 计算异或校验码
                            var strCyclicRedundancyCheck = CalCheckCode_FE(CleanHexString("00 " + strInner + " 00"));
                            // 合成返回值
                            str = CleanHexString(strHeader + " " + strInner + " " + strCyclicRedundancyCheck);
                        }
                        break;
                    case "7E":
                        {
                            if (IsLoRaFlag)
                            {
                                // 获取所需解析数据
                                ParameterAcquisition_7E(out var strHeader, out _, out _,
                                    out var strProtocolVendor, out var strHandler, out var strGroup);
                                // 功能码 / 数据类型
                                var strFunctionData = "01 00";
                                // 写操作数据区
                                string strHandlerContent;
                                if (RegularDataUpdateRate.Text != "")
                                    strHandlerContent = Convert.ToString(Convert.ToInt32(RegularDataUpdateRate.Text), 16)
                                        .ToUpper().PadLeft(4, '0').Insert(2, " ");
                                else if (ResSleepTime.Text != "")
                                    strHandlerContent = FrameContent.Substring(36, 5);
                                else
                                    strHandlerContent = "00 00";

                                // 合成数据域
                                var strContent = strProtocolVendor + " " + strHandler + " " + strGroup + " " +
                                                 strFunctionData + " " + strHandlerContent;
                                // 计算长度域（包含命令域）
                                var intLength = (strContent.Length + 1) / 3;
                                var strLength = Convert.ToString(intLength, 16).ToUpper().PadLeft(4, '0').Insert(2, " ");
                                var strInner = strLength + " " + strContent;
                                // 计算异或校验码
                                var strCyclicRedundancyCheck = CalCheckCode_7E(CleanHexString("00 " + strInner + " 00"));
                                // 合成返回值
                                str = CleanHexString(strHeader + " " + strInner + " " + strCyclicRedundancyCheck);
                            }
                            else
                            {
                                // 获取所需解析数据
                                ParameterAcquisition_7E(out var strHeader, out var strCommand, out var strAddress,
                                    out var strProtocolVendor, out var strHandler, out var strGroup);
                                // 功能码 / 数据类型
                                var strFunctionData = "01 00";
                                // 写操作数据区
                                string strHandlerContent;
                                if (RegularDataUpdateRate.Text != "")
                                    strHandlerContent = Convert.ToString(Convert.ToInt32(RegularDataUpdateRate.Text), 16)
                                        .ToUpper().PadLeft(4, '0').Insert(2, " ");
                                else if (ResSleepTime.Text != "")
                                    strHandlerContent = FrameContent.Substring(36, 5);
                                else
                                    strHandlerContent = "00 00";

                                // 合成数据域
                                var strContent = strCommand + " " + strAddress + " " +
                                                 FrameUnparsed.Remove(FrameUnparsed.Length - 3, 3) + " 00 00 " +
                                                 strProtocolVendor + " " + strHandler + " " + strGroup + " " +
                                                 strFunctionData + " " + strHandlerContent;
                                // 计算长度域（包含命令域）
                                var intLength = (strContent.Length + 1) / 3;
                                var strLength = Convert.ToString(intLength, 16).ToUpper().PadLeft(4, '0').Insert(2, " ");
                                var strInner = strLength + " " + strContent;
                                // 计算异或校验码
                                var strCyclicRedundancyCheck = CalCheckCode_7E(CleanHexString("00 " + strInner + " 00"));
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
                switch (FrameHeader)
                {
                    case "FE":
                        {
                            // 获取所需解析数据
                            ParameterAcquisition_FE(out var strHeader, out var strCommand, out var strAddress,
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
                            var strCyclicRedundancyCheck = CalCheckCode_FE(CleanHexString("00 " + strInner + " 00"));
                            // 合成返回值
                            str = CleanHexString(strHeader + " " + strInner + " " + strCyclicRedundancyCheck);
                        }
                        break;
                    case "7E":
                        {
                            if (IsLoRaFlag)
                            {
                                // 获取所需解析数据
                                ParameterAcquisition_7E(out var strHeader, out _, out _,
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
                                var strCyclicRedundancyCheck = CalCheckCode_7E(CleanHexString("00 " + strInner + " 00"));
                                // 合成返回值
                                str = CleanHexString(strHeader + " " + strInner + " " + strCyclicRedundancyCheck);
                            }
                            else
                            {
                                // 获取所需解析数据
                                ParameterAcquisition_7E(out var strHeader, out var strCommand, out var strAddress,
                                    out var strProtocolVendor, out var strHandler, out var strGroup);
                                // 写操作数据区
                                // 功能码 / 数据类型
                                var strFunctionData = "01 01";
                                var strHandlerContent = "";
                                // 合成数据域
                                var strContent = strCommand + " " + strAddress + " " +
                                                 FrameUnparsed.Remove(FrameUnparsed.Length - 3, 3) + " 00 00 " +
                                                 strProtocolVendor + " " + strHandler + " " + strGroup + " " +
                                                 strFunctionData + " " + strHandlerContent;
                                // 计算长度域（包含命令域）
                                var intLength = (strContent.Length + 1) / 3;
                                var strLength = Convert.ToString(intLength, 16).ToUpper().PadLeft(4, '0').Insert(2, " ");
                                var strInner = strLength + " " + strContent;
                                // 计算异或校验码
                                var strCyclicRedundancyCheck = CalCheckCode_7E(CleanHexString("00 " + strInner + " 00"));
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
        ///     建立连接处理程序
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void EstablishConnectionButton_Checked(object sender, RoutedEventArgs e)
        {
            // 判断仪表参数是否解析完成
            if (ResCyclicRedundancyCheck.Text == "通过")
            {
                try
                {
                    // 发送下行报文建立连接 
                    //// 生成16进制字符串
                    SendTextBox.Text = EstablishBuild_Text();
                    //// 标定连接发送（已替换为通过connectFlag自动发送）
                    //SerialPortSend(sendTextBox.Text);
                    //serialPort.Write(EstablishBuild_Text());
                    // 指示灯变绿
                    if (true)
                    {
                        ConnectionStatusEllipse.Fill = Brushes.Yellow;
                        ConnectFlag = true;
                    }

                    EstablishConnectionButton.Content = "关闭连接";
                    EstablishConnectionButton.IsEnabled = false;
                    // 更新率锁定
                    RegularDataUpdateRate.IsEnabled = false;
                    StatusTextBlock.Text = "正在建立连接……";
                }
                catch (Exception ex)
                {
                    var str = ex.StackTrace;
                    Console.WriteLine(str);
                    // 异常时显示提示文字
                    StatusTextBlock.Text = "建立连接出错！";
                    // 指示灯变灰
                    EstablishConnectionButton.IsEnabled = true;
                    EstablishConnectionButton.Content = "建立连接";
                    ConnectFlag = false;
                    ConnectionStatusEllipse.Fill = Brushes.Gray;
                }
            }
            else
            {
                StatusTextBlock.Text = "请先解析仪表参数！";
                EstablishConnectionButton.IsChecked = false;
            }
        }

        /// <summary>
        ///     建立连接帧
        /// </summary>
        /// <returns></returns>
        private string EstablishBuild_Text()
        {
            var str = "";
            switch (FrameHeader)
            {
                case "FE":
                    {
                        // 获取所需解析数据
                        ParameterAcquisition_FE(out var strHeader, out var strCommand, out var strAddress,
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
                        var strCyclicRedundancyCheck = CalCheckCode_FE(CleanHexString("00 " + strInner + " 00"));
                        // 合成返回值
                        str = CleanHexString(strHeader + " " + strInner + " " + strCyclicRedundancyCheck);
                    }
                    break;
                case "7E":
                    {
                        if (IsLoRaFlag)
                        {
                            // 获取所需解析数据
                            ParameterAcquisition_7E(out var strHeader, out _, out _,
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
                            var strCyclicRedundancyCheck = CalCheckCode_7E(CleanHexString("00 " + strInner + " 00"));
                            // 合成返回值
                            str = CleanHexString(strHeader + " " + strInner + " " + strCyclicRedundancyCheck);
                        }
                        else
                        {
                            // 获取所需解析数据
                            ParameterAcquisition_7E(out var strHeader, out var strCommand, out var strAddress,
                                out var strProtocolVendor, out var strHandler, out var strGroup);
                            // 写操作数据区
                            // 功能码 / 数据类型
                            var strFunctionData = "00 80";
                            var strHandlerContent = "F0";
                            // 合成数据域
                            var strContent = strCommand + " " + strAddress + " " +
                                             FrameUnparsed.Remove(FrameUnparsed.Length - 3, 3) + " 00 00 " +
                                             strProtocolVendor + " " + strHandler + " " + strGroup + " " +
                                             strFunctionData + " " + strHandlerContent;
                            // 计算长度域（包含命令域）
                            var intLength = (strContent.Length + 1) / 3;
                            var strLength = Convert.ToString(intLength, 16).ToUpper().PadLeft(4, '0').Insert(2, " ");
                            var strInner = strLength + " " + strContent;
                            // 计算异或校验码
                            var strCyclicRedundancyCheck = CalCheckCode_7E(CleanHexString("00 " + strInner + " 00"));
                            // 合成返回值
                            str = CleanHexString(strHeader + " " + strInner + " " + strCyclicRedundancyCheck);
                        }
                    }
                    break;
            }

            return str;
        }

        /// <summary>
        ///     断开连接处理程序
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void EstablishConnectionButton_Unchecked(object sender, RoutedEventArgs e)
        {
            // 判断仪表参数是否解析完成
            if (ResCyclicRedundancyCheck.Text == "通过")
                try
                {
                    // 发送下行报文建立连接 
                    //// 生成16进制字符串
                    SendTextBox.Text = EstablishDisconnect_Text();
                    //// 标定连接发送（已替换为通过connectFlag自动发送）
                    SerialPortSend(SendTextBox.Text);
                    // serialPort.Write(EstablishBuild_Text());
                    // 指示灯变灰
                    EstablishConnectionButton.Content = "建立连接";
                    ConnectFlag = false;
                    ConnectionStatusEllipse.Fill = Brushes.Gray;
                    // 更新率不锁定
                    RegularDataUpdateRate.IsEnabled = true;
                    EstablishConnectionButton.IsEnabled = true;
                    StatusTextBlock.Text = "连接已断开";
                }
                catch (Exception ex)
                {
                    var str = ex.StackTrace;
                    Console.WriteLine(str);
                    // 异常时显示提示文字
                    StatusTextBlock.Text = "断开连接出错！";
                    // 指示灯变灰
                    EstablishConnectionButton.Content = "关闭连接";
                    EstablishConnectionButton.IsEnabled = true;
                    ConnectionStatusEllipse.Fill = Brushes.Green;
                }
            else StatusTextBlock.Text = "请先解析仪表参数！";
        }

        /// <summary>
        ///     断开连接帧
        /// </summary>
        /// <returns></returns>
        private string EstablishDisconnect_Text()
        {
            var str = "";
            switch (FrameHeader)
            {
                case "FE":
                    {
                        // 获取所需解析数据
                        ParameterAcquisition_FE(out var strHeader, out var strCommand, out var strAddress,
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
                        var strCyclicRedundancyCheck = CalCheckCode_FE(CleanHexString("00 " + strInner + " 00"));
                        // 合成返回值
                        str = CleanHexString(strHeader + " " + strInner + " " + strCyclicRedundancyCheck);
                    }
                    break;
                case "7E":
                    {
                        if (IsLoRaFlag)
                        {
                            // 获取所需解析数据
                            ParameterAcquisition_7E(out var strHeader, out _, out _,
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
                            var strCyclicRedundancyCheck = CalCheckCode_7E(CleanHexString("00 " + strInner + " 00"));
                            // 合成返回值
                            str = CleanHexString(strHeader + " " + strInner + " " + strCyclicRedundancyCheck);
                        }
                        else
                        {
                            // 获取所需解析数据
                            ParameterAcquisition_7E(out var strHeader, out var strCommand, out var strAddress,
                                out var strProtocolVendor, out var strHandler, out var strGroup);
                            // 写操作数据区
                            // 功能码 / 数据类型
                            var strFunctionData = "00 80";
                            var strHandlerContent = "FF";
                            // 合成数据域
                            var strContent = strCommand + " " + strAddress + " " +
                                             FrameUnparsed.Remove(FrameUnparsed.Length - 3, 3) + " 00 00 " +
                                             strProtocolVendor + " " + strHandler + " " + strGroup + " " +
                                             strFunctionData + " " + strHandlerContent;
                            // 计算长度域（包含命令域）
                            var intLength = (strContent.Length + 1) / 3;
                            var strLength = Convert.ToString(intLength, 16).ToUpper().PadLeft(4, '0').Insert(2, " ");
                            var strInner = strLength + " " + strContent;
                            // 计算异或校验码
                            var strCyclicRedundancyCheck = CalCheckCode_7E(CleanHexString("00 " + strInner + " 00"));
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
            if (ResCyclicRedundancyCheck.Text == "通过" && ConnectionStatusEllipse.Fill == Brushes.Green)
                try
                {
                    // 发送下行报文建立连接 
                    // 生成16进制字符串
                    SendTextBox.Text = DescribeCalibration_Text();
                    // 标定连接发送
                    SerialPortSend(SendTextBox.Text);
                    //if (true)
                    //{
                    //    establishConnectionButton.IsChecked = false;
                    //}
                    StatusTextBlock.Text = "描述标定已发送！";
                }
                catch (Exception ex)
                {
                    var str = ex.StackTrace;
                    Console.WriteLine(str);
                    // 异常时显示提示文字
                    StatusTextBlock.Text = "描述标定出错！";
                }
            else StatusTextBlock.Text = "请先建立连接！";
        }

        /// <summary>
        ///     生成描述标定数据
        /// </summary>
        /// <returns></returns>
        private string DescribeCalibration_Text()
        {
            var str = "";
            switch (FrameHeader)
            {
                case "FE":
                    {
                        // 获取所需解析数据
                        ParameterAcquisition_FE(out var strHeader, out var strCommand, out var strAddress,
                            out var strProtocolVendor, out var strHandler, out var strGroup);
                        // 获取设备描述标定信息
                        // 功能码 / 数据类型
                        var strFunctionData = "00 80";
                        // 写操作数据区
                        var strHandlerContent = "F1 " + CalibrationInstrumentModelTextBox.Text.Trim() + " " +
                                                CalibrationSerialNumberTextBox.Text.Trim() + " " +
                                                CalibrationIpRatingTextBox.Text.Trim() + " " +
                                                CalibrationExplosionProofLevelTextBox.Text.Trim() + " " +
                                                CalibrationInstructionsTextBox.Text.Trim();
                        // 合成数据域
                        var strContent = strAddress + " " + strProtocolVendor + " " + strHandler + " " + strGroup + " " +
                                         strFunctionData + " " + strHandlerContent;
                        // 计算长度域
                        var intLength = (strContent.Length + 1) / 3;
                        var strLength = Convert.ToString(intLength, 16).ToUpper().PadLeft(2, '0');
                        var strInner = strLength + " " + strCommand + " " + strContent;
                        // 计算异或校验码
                        var strCyclicRedundancyCheck = HexCyclicRedundancyCheck(CleanHexString(strInner));
                        // 合成返回值
                        str = CleanHexString(strHeader + " " + strInner + " " + strCyclicRedundancyCheck);
                    }
                    break;
                case "7E":
                    {
                        if (IsLoRaFlag)
                        {
                            // 获取所需解析数据
                            ParameterAcquisition_7E(out var strHeader, out _, out _,
                                out var strProtocolVendor, out var strHandler, out var strGroup);
                            // 获取设备描述标定信息
                            // 功能码 / 数据类型
                            var strFunctionData = "00 80";
                            // 写操作数据区
                            var strHandlerContent = "F1 " + CalibrationInstrumentModelTextBox.Text.Trim() + " " +
                                                    CalibrationSerialNumberTextBox.Text.Trim() + " " +
                                                    CalibrationIpRatingTextBox.Text.Trim() + " " +
                                                    CalibrationExplosionProofLevelTextBox.Text.Trim() + " " +
                                                    CalibrationInstructionsTextBox.Text.Trim();
                            // 合成数据域
                            var strContent = strProtocolVendor + " " + strHandler + " " + strGroup + " " +
                                             strFunctionData + " " + strHandlerContent;
                            // 计算长度域（包含命令域）
                            var intLength = (strContent.Length + 1) / 3;
                            var strLength = Convert.ToString(intLength, 16).ToUpper().PadLeft(4, '0').Insert(2, " ");
                            var strInner = strLength + " " + strContent;
                            // 计算异或校验码
                            var strCyclicRedundancyCheck = CalCheckCode_7E(CleanHexString("00 " + strInner + " 00"));
                            // 合成返回值
                            str = CleanHexString(strHeader + " " + strInner + " " + strCyclicRedundancyCheck);
                        }
                        else
                        {
                            // 获取所需解析数据
                            ParameterAcquisition_7E(out var strHeader, out var strCommand, out var strAddress,
                                out var strProtocolVendor, out var strHandler, out var strGroup);
                            // 获取设备描述标定信息
                            // 功能码 / 数据类型
                            var strFunctionData = "00 80";
                            // 写操作数据区
                            var strHandlerContent = "F1 " + CalibrationInstrumentModelTextBox.Text.Trim() + " " +
                                                    CalibrationSerialNumberTextBox.Text.Trim() + " " +
                                                    CalibrationIpRatingTextBox.Text.Trim() + " " +
                                                    CalibrationExplosionProofLevelTextBox.Text.Trim() + " " +
                                                    CalibrationInstructionsTextBox.Text.Trim();
                            // 合成数据域
                            var strContent = strCommand + " " + strAddress + " " +
                                             FrameUnparsed.Remove(FrameUnparsed.Length - 3, 3) + " 00 00 " +
                                             strProtocolVendor + " " + strHandler + " " + strGroup + " " +
                                             strFunctionData + " " + strHandlerContent;
                            // 计算长度域
                            var intLength = (strContent.Length + 1) / 3;
                            var strLength = Convert.ToString(intLength, 16).ToUpper().PadLeft(4, '0').Insert(2, " ");
                            var strInner = strLength + " " + strContent;
                            // 计算异或校验码
                            var strCyclicRedundancyCheck = CalCheckCode_7E(CleanHexString("00 " + strInner + " 00"));
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
            if (ResCyclicRedundancyCheck.Text == "通过" && ConnectionStatusEllipse.Fill == Brushes.Green)
                try
                {
                    // 发送下行报文建立连接 
                    // 生成16进制字符串
                    SendTextBox.Text = ParameterCalibration_Text();
                    SerialPortSend(SendTextBox.Text);
                    StatusTextBlock.Text = "参数标定已发送！";

                    // 标定连接发送
                    // SerialPortSend();
                }
                catch (Exception ex)
                {
                    var str = ex.StackTrace;
                    Console.WriteLine(str);
                    // 异常时显示提示文字
                    StatusTextBlock.Text = "描述标定出错！";
                }
            else StatusTextBlock.Text = "请先建立连接！";
        }

        /// <summary>
        ///     生成参数标定数据
        /// </summary>
        /// <returns></returns>
        private string ParameterCalibration_Text()
        {
            var str = "";
            switch (FrameHeader)
            {
                case "FE":
                    {
                        // 获取所需解析数据
                        ParameterAcquisition_FE(out var strHeader, out var strCommand, out var strAddress,
                            out var strProtocolVendor, out var strHandler, out var strGroup);
                        // 获取设备描述标定信息
                        // 功能码 / 数据类型
                        var strFunctionData = "00 80";
                        // 标定数据
                        var calibrationParameters = FloatStrToHexStr(CalibrationParametersContentTextBox.Text);
                        // 写操作数据区
                        var strHandlerContent = "F2 " + CalibrationParametersComboBox.Text.Substring(2, 2).Trim() + " " +
                                                CalibrationUnitTextBox.Text.Trim() + " " + calibrationParameters;
                        // 合成数据域
                        var strContent = strAddress + " " + strProtocolVendor + " " + strHandler + " " + strGroup + " " +
                                         strFunctionData + " " + strHandlerContent;
                        // 计算长度域
                        var intLength = (strContent.Length + 1) / 3;
                        var strLength = Convert.ToString(intLength, 16).ToUpper().PadLeft(2, '0');
                        var strInner = strLength + " " + strCommand + " " + strContent;
                        // 计算异或校验码
                        var strCyclicRedundancyCheck = HexCyclicRedundancyCheck(CleanHexString(strInner));
                        // 合成返回值
                        str = CleanHexString(strHeader + " " + strInner + " " + strCyclicRedundancyCheck);
                    }
                    break;
                case "7E":
                    {
                        if (IsLoRaFlag)
                        {
                            // 获取所需解析数据
                            ParameterAcquisition_7E(out var strHeader, out _, out _,
                                out var strProtocolVendor, out var strHandler, out var strGroup);
                            // 获取设备描述标定信息
                            // 功能码 / 数据类型
                            var strFunctionData = "00 80";
                            // 标定数据
                            var calibrationParameters = FloatStrToHexStr(CalibrationParametersContentTextBox.Text);
                            // 写操作数据区
                            var strHandlerContent = "F2 " + CalibrationParametersComboBox.Text.Substring(2, 2).Trim() +
                                                    " " + CalibrationUnitTextBox.Text.Trim() + " " +
                                                    calibrationParameters;
                            // 合成数据域
                            var strContent = strProtocolVendor + " " + strHandler + " " + strGroup + " " +
                                             strFunctionData + " " + strHandlerContent;
                            // 计算长度域（包含命令域）
                            var intLength = (strContent.Length + 1) / 3;
                            var strLength = Convert.ToString(intLength, 16).ToUpper().PadLeft(4, '0').Insert(2, " ");
                            var strInner = strLength + " " + strContent;
                            // 计算异或校验码
                            var strCyclicRedundancyCheck = CalCheckCode_7E(CleanHexString("00 " + strInner + " 00"));
                            // 合成返回值
                            str = CleanHexString(strHeader + " " + strInner + " " + strCyclicRedundancyCheck);
                        }
                        else
                        {
                            // 获取所需解析数据
                            ParameterAcquisition_7E(out var strHeader, out var strCommand, out var strAddress,
                                out var strProtocolVendor, out var strHandler, out var strGroup);
                            // 获取设备描述标定信息
                            // 功能码 / 数据类型
                            var strFunctionData = "00 80";
                            // 标定数据
                            var calibrationParameters = FloatStrToHexStr(CalibrationParametersContentTextBox.Text);
                            // 写操作数据区
                            var strHandlerContent = "F2 " + CalibrationParametersComboBox.Text.Substring(2, 2).Trim() +
                                                    " " + CalibrationUnitTextBox.Text.Trim() + " " +
                                                    calibrationParameters;
                            // 合成数据域
                            var strContent = strCommand + " " + strAddress + " " +
                                             FrameUnparsed.Remove(FrameUnparsed.Length - 3, 3) + " 00 00 " +
                                             strProtocolVendor + " " + strHandler + " " + strGroup + " " +
                                             strFunctionData + " " + strHandlerContent;
                            // 计算长度域
                            var intLength = (strContent.Length + 1) / 3;
                            var strLength = Convert.ToString(intLength, 16).ToUpper().PadLeft(4, '0').Insert(2, " ");
                            var strInner = strLength + " " + strContent;
                            // 计算异或校验码
                            var strCyclicRedundancyCheck = CalCheckCode_7E(CleanHexString("00 " + strInner + " 00"));
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
            strAddress = FrameAddress;
            // 协议和厂商号为数据内容前四位
            strProtocolVendor = FrameContent.Substring(0, 11);
            // 仪表类型：手操器
            strHandler = "1F 10";
            // 组号表号
            strGroup = FrameContent.Substring(18, 5);
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
            if (IsLoRaFlag)
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
                strAddress = FrameAddress;
            }

            // 协议和厂商号
            strProtocolVendor = FrameContent.Substring(0, 11);
            // 仪表类型：手操器
            strHandler = "1F 10";
            // 组号表号
            strGroup = FrameContent.Substring(18, 5);
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
                // 先判断帧头
                switch (sepStr[i])
                {
                    case "FE":
                        handleStr = str.Substring(i * 3, str.Length - i * 3);
                        sepHandleStr = handleStr.Split(' ');
                        // 通过长度域判断是否为完整报文,
                        if (sepHandleStr.Length >= 5 && sepHandleStr.Length >= Convert.ToInt32(sepHandleStr[1], 16) + 5)
                        {
                            var useFrame = new string[Convert.ToInt32(sepHandleStr[1], 16) + 5];
                            Array.Copy(sepHandleStr, useFrame, Convert.ToInt32(sepHandleStr[1], 16) + 5);
                            var useFrameStr = string.Join(" ", useFrame);
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

                        break;
                    case "7E":
                        handleStr = str.Substring(i * 3, str.Length - i * 3);
                        sepHandleStr = handleStr.Split(' ');
                        // 通过长度域判断是否为完整报文
                        if (sepHandleStr.Length >= 5) tmpStr = sepHandleStr[1] + sepHandleStr[2];

                        if (sepHandleStr.Length >= 5 && sepHandleStr.Length >=
                            Convert.ToInt32(sepHandleStr[1] + sepHandleStr[2], 16) + 4)
                        {
                            var a = new string[Convert.ToInt32(tmpStr, 16) + 4];
                            Array.Copy(sepHandleStr, a, Convert.ToInt32(tmpStr, 16) + 4);
                            var useFrameStr = string.Join(" ", a);
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

                        break;
                }

            // 提取尾帧
            if (mantissaStartFrame < sepStr.Length)
                outPutStr.SetValue(str.Substring(mantissaStartFrame * 3, str.Length - mantissaStartFrame * 3),
                    outPutStrTag + 1);

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
            if (hexStr1 == null) throw new ArgumentNullException(nameof(hexStr1));
            // 两个十六进制字符串的长度和长度差的绝对值以及异或结果
            var iHexStr1Len = hexStr1.Length;
            var iHexStr2Len = hexStr2.Length;
            var result = string.Empty;
            // 获取这两个十六进制字符串长度的差值
            var iGap = iHexStr1Len - iHexStr2Len;
            // 获取这两个十六进制字符串长度最小的那一个
            var iHexStrLenLow = iHexStr1Len < iHexStr2Len ? iHexStr1Len : iHexStr2Len;
            // 将这两个字符串转换成字节数组
            var bHexStr1 = HexStrToBytes(hexStr1);
            var bHexStr2 = HexStrToBytes(hexStr2);
            var i = 0;
            //先把每个字节异或后得到一个0~15范围内的整数，再转换成十六进制字符
            for (; i < iHexStrLenLow; ++i) result += (bHexStr1[i] ^ bHexStr2[i]).ToString("X");

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
            if (ori == null) throw new ArgumentNullException(nameof(ori));

            var hexvalue = ori.Trim().Split(' ', '	');
            var j = "";
            foreach (var hex in hexvalue) j = HexStrXor(j, hex);

            return j;
        }

        /// <summary>
        ///     将十六进制字符串转换为十六进制数组
        /// </summary>
        /// <param name="hexStr"></param>
        /// <returns></returns>
        public byte[] HexStrToBytes(string hexStr)
        {
            if (hexStr == null) throw new ArgumentNullException(nameof(hexStr));

            var bytes = new byte[hexStr.Length];
            if (bytes == null) throw new ArgumentNullException(nameof(bytes));
            try
            {
                for (var i = 0; i < bytes.Length; ++i)
                    //将每个16进制字符转换成对应的1个字节
                    bytes[i] = Convert.ToByte(hexStr.Substring(i, 1), 16);
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
            if (hexStr == null) throw new ArgumentNullException(nameof(hexStr));

            hexStr = hexStr.Replace(" ", "");
            if (hexStr.Length != 8) throw new ArgumentNullException(nameof(hexStr));

            var data1 = Convert.ToInt32(hexStr.Substring(0, 2), 16);
            var data2 = Convert.ToInt32(hexStr.Substring(2, 2), 16);
            var data3 = Convert.ToInt32(hexStr.Substring(4, 2), 16);
            var data4 = Convert.ToInt32(hexStr.Substring(6, 2), 16);

            var data = (data1 << 24) | (data2 << 16) | (data3 << 8) | data4;

            int nSign;
            if ((data & 0x80000000) > 0)
                nSign = -1;
            else
                nSign = 1;

            var nExp = data & 0x7F800000;
            nExp >>= 23;
            float nMantissa = data & 0x7FFFFF;

            if (nMantissa != 0)
                nMantissa = 1 + nMantissa / 8388608;

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
            ResProtocol.Clear();
            ResAddress.Clear();
            ResVendor.Clear();
            ResType.Clear();
            ResGroup.Clear();
            ResFunctionData.Clear();
            ResSucRate.Clear();
            ResBatVol.Clear();
            ResSleepTime.Clear();
            ResStatue.Clear();
            ResData.Clear();
            ResTime.Clear();
            // 仪表参数清空
            ResModel.Clear();
            ResSerialNumber.Clear();
            ResFirmwareVersion.Clear();
            ResSoftwareVersion.Clear();
            ResLowRange.Clear();
            ResHighRange.Clear();
            ResMeasurementAccuracy.Clear();
            ResProtectionLevel.Clear();
            ResExplosionProofGrade.Clear();
            ResIllustrate.Clear();
            // 校验码清空
            ResCyclicRedundancyCheck.Clear();
            // 将前景色改为黑色
            // 实时数据改色
            ResProtocol.Foreground = new SolidColorBrush(Colors.Black);
            ResAddress.Foreground = new SolidColorBrush(Colors.Black);
            ResVendor.Foreground = new SolidColorBrush(Colors.Black);
            ResType.Foreground = new SolidColorBrush(Colors.Black);
            ResGroup.Foreground = new SolidColorBrush(Colors.Black);
            ResFunctionData.Foreground = new SolidColorBrush(Colors.Black);
            ResSucRate.Foreground = new SolidColorBrush(Colors.Black);
            ResBatVol.Foreground = new SolidColorBrush(Colors.Black);
            ResSleepTime.Foreground = new SolidColorBrush(Colors.Black);
            ResStatue.Foreground = new SolidColorBrush(Colors.Black);
            ResData.Foreground = new SolidColorBrush(Colors.Black);
            ResTime.Foreground = new SolidColorBrush(Colors.Black);
            ResCyclicRedundancyCheck.Foreground = new SolidColorBrush(Colors.Black);
            // 仪表参数改色
            ResModel.Foreground = new SolidColorBrush(Colors.Black);
            ResSerialNumber.Foreground = new SolidColorBrush(Colors.Black);
            ResFirmwareVersion.Foreground = new SolidColorBrush(Colors.Black);
            ResSoftwareVersion.Foreground = new SolidColorBrush(Colors.Black);
            ResLowRange.Foreground = new SolidColorBrush(Colors.Black);
            ResHighRange.Foreground = new SolidColorBrush(Colors.Black);
            ResMeasurementAccuracy.Foreground = new SolidColorBrush(Colors.Black);
            ResProtectionLevel.Foreground = new SolidColorBrush(Colors.Black);
            ResExplosionProofGrade.Foreground = new SolidColorBrush(Colors.Black);
            ResIllustrate.Foreground = new SolidColorBrush(Colors.Black);
            // 校验码改色
            ResCyclicRedundancyCheck.Foreground = new SolidColorBrush(Colors.Black);
            // 隐藏字段
            // 实时数据隐藏字段
            ResProtocolDockPanel.Visibility = Visibility.Collapsed;
            ResAddressDockPanel.Visibility = Visibility.Collapsed;
            ResVendorDockPanel.Visibility = Visibility.Collapsed;
            ResTypeDockPanel.Visibility = Visibility.Collapsed;
            ResGroupDockPanel.Visibility = Visibility.Collapsed;
            ResFunctionDataDockPanel.Visibility = Visibility.Collapsed;
            ResSucRateDockPanel.Visibility = Visibility.Collapsed;
            ResBatVolDockPanel.Visibility = Visibility.Collapsed;
            ResSleepTimeDockPanel.Visibility = Visibility.Collapsed;
            ResStatueDockPanel.Visibility = Visibility.Collapsed;
            ResDataDockPanel.Visibility = Visibility.Collapsed;
            ResTimeDockPanel.Visibility = Visibility.Collapsed;
            // 仪表参数隐藏字段
            ResModelDockPanel.Visibility = Visibility.Collapsed;
            ResSerialNumberDockPanel.Visibility = Visibility.Collapsed;
            ResFirmwareVersionDockPanel.Visibility = Visibility.Collapsed;
            ResSoftwareVersionDockPanel.Visibility = Visibility.Collapsed;
            ResLowRangeDockPanel.Visibility = Visibility.Collapsed;
            ResHighRangeDockPanel.Visibility = Visibility.Collapsed;
            ResMeasurementAccuracyDockPanel.Visibility = Visibility.Collapsed;
            ResProtectionLevelDockPanel.Visibility = Visibility.Collapsed;
            ResExplosionProofGradeDockPanel.Visibility = Visibility.Collapsed;
            ResIllustrateDockPanel.Visibility = Visibility.Collapsed;
            // 校验码隐藏字段
            ResCyclicRedundancyCheckDockPanel.Visibility = Visibility.Collapsed;
        }

        #endregion

        #region 绘图方法

        /// <summary>
        ///     标定栏的数据预览
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CalibrationInstrumentModelTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            // 将光标移至文字末尾
            CalibrationInstrumentModelTextBox.SelectionStart = CalibrationInstrumentModelTextBox.Text.Length;
            var hexadecimalCollection = Regex.Matches(e.Text, @"[\da-fA-F]");
            foreach (Match mat in hexadecimalCollection) CalibrationInstrumentModelTextBox.AppendText(mat.Value);

            // 每输入两个字符自动添加空格
            CalibrationInstrumentModelTextBox.Text = CalibrationInstrumentModelTextBox.Text.Replace(" ", "");
            CalibrationInstrumentModelTextBox.Text = string.Join(" ",
                Regex.Split(CalibrationInstrumentModelTextBox.Text, "(?<=\\G.{2})(?!$)"));
            CalibrationInstrumentModelTextBox.SelectionStart = CalibrationInstrumentModelTextBox.Text.Length;
            e.Handled = true;
        }

        private void CalibrationSerialNumberTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            // 将光标移至文字末尾
            CalibrationSerialNumberTextBox.SelectionStart = CalibrationSerialNumberTextBox.Text.Length;
            var hexadecimalCollection = Regex.Matches(e.Text, @"[\da-fA-F]");
            foreach (Match mat in hexadecimalCollection) CalibrationSerialNumberTextBox.AppendText(mat.Value);

            // 每输入两个字符自动添加空格
            CalibrationSerialNumberTextBox.Text = CalibrationSerialNumberTextBox.Text.Replace(" ", "");
            CalibrationSerialNumberTextBox.Text = string.Join(" ",
                Regex.Split(CalibrationSerialNumberTextBox.Text, "(?<=\\G.{2})(?!$)"));
            CalibrationSerialNumberTextBox.SelectionStart = CalibrationSerialNumberTextBox.Text.Length;
            e.Handled = true;
        }

        private void CalibrationIPRatingTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            // 将光标移至文字末尾
            CalibrationIpRatingTextBox.SelectionStart = CalibrationIpRatingTextBox.Text.Length;
            var hexadecimalCollection = Regex.Matches(e.Text, @"[\da-fA-F]");
            foreach (Match mat in hexadecimalCollection) CalibrationIpRatingTextBox.AppendText(mat.Value);

            // 每输入两个字符自动添加空格
            CalibrationIpRatingTextBox.Text = CalibrationIpRatingTextBox.Text.Replace(" ", "");
            CalibrationIpRatingTextBox.Text =
                string.Join(" ", Regex.Split(CalibrationIpRatingTextBox.Text, "(?<=\\G.{2})(?!$)"));
            CalibrationIpRatingTextBox.SelectionStart = CalibrationIpRatingTextBox.Text.Length;
            e.Handled = true;
        }

        private void CalibrationExplosionProofLevelTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            // 将光标移至文字末尾
            CalibrationExplosionProofLevelTextBox.SelectionStart = CalibrationExplosionProofLevelTextBox.Text.Length;
            var hexadecimalCollection = Regex.Matches(e.Text, @"[\da-fA-F]");
            foreach (Match mat in hexadecimalCollection) CalibrationExplosionProofLevelTextBox.AppendText(mat.Value);

            // 每输入两个字符自动添加空格
            CalibrationExplosionProofLevelTextBox.Text = CalibrationExplosionProofLevelTextBox.Text.Replace(" ", "");
            CalibrationExplosionProofLevelTextBox.Text = string.Join(" ",
                Regex.Split(CalibrationExplosionProofLevelTextBox.Text, "(?<=\\G.{2})(?!$)"));
            CalibrationExplosionProofLevelTextBox.SelectionStart = CalibrationExplosionProofLevelTextBox.Text.Length;
            e.Handled = true;
        }

        private void CalibrationInstructionsTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            // 将光标移至文字末尾
            CalibrationInstructionsTextBox.SelectionStart = CalibrationInstructionsTextBox.Text.Length;
            var hexadecimalCollection = Regex.Matches(e.Text, @"[\da-fA-F]");
            foreach (Match mat in hexadecimalCollection) CalibrationInstructionsTextBox.AppendText(mat.Value);

            // 每输入两个字符自动添加空格
            CalibrationInstructionsTextBox.Text = CalibrationInstructionsTextBox.Text.Replace(" ", "");
            CalibrationInstructionsTextBox.Text = string.Join(" ",
                Regex.Split(CalibrationInstructionsTextBox.Text, "(?<=\\G.{2})(?!$)"));
            CalibrationInstructionsTextBox.SelectionStart = CalibrationInstructionsTextBox.Text.Length;
            e.Handled = true;
        }


        /// <summary>
        ///     画折线图
        /// </summary>
        public ObservableDataSource<Point> DataSource { get; private set; } = new ObservableDataSource<Point>();

        public int PlotPointX { get; private set; } = 1;
        public double PlotPointY { get; private set; }
        public bool ConnectFlag { get; private set; }

        private void AnimatedPlot()
        {
            double x = PlotPointX;
            try
            {
                PlotPointY = RealTimeData;
            }
            catch (Exception ex)
            {
                var str = ex.StackTrace;
                Console.WriteLine(str);
            }

            var point = new Point(x, PlotPointY);
            DataSource.AppendAsync(Dispatcher, point);
            PlotPointX++;
        }


        private void Plot_Loaded()
        {
            Plotter.AxisGrid.Visibility = Visibility.Hidden;
            Plotter.AddLineGraph(DataSource, Colors.Blue, 2, "实时数据");
            Plotter.Viewport.Visible = new Rect(0, -1, 5, 24);
            Plotter.Viewport.FitToView();
        }

        private void RegularDataUpdateRate_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            {
                if (RegularDataUpdateRate.Text != "")
                    if (IsValid(RegularDataUpdateRate.Text) == false ||
                        Convert.ToInt32(RegularDataUpdateRate.Text, 16) > 65535 ||
                        Convert.ToInt32(RegularDataUpdateRate.Text, 16) < 0)
                    {
                        MessageBox.Show("请输入0 - 65535整数");
                        RegularDataUpdateRate.Text = "8";
                    }
            }
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
            CalibrationSqlConnect = new SqlConnection(ConnectString);


            // 连接到数据库服务器
            try
            {
                CalibrationSqlConnect.Open();
                MessageBox.Show($"与服务器{SqlServer}建立连接：操作数据库为{SqlDatabase}。", "建立连接", MessageBoxButton.OK,
                    MessageBoxImage.Information);
                SqlConnectButton.Content = "断开数据库";
                SqlConnectEllipse.Fill = Brushes.Green;
            }
            catch (Exception ex)
            {
                MessageBox.Show("建立连接失败：" + ex.Message, "建立连接", MessageBoxButton.OK,
                    MessageBoxImage.Exclamation);
                SqlConnectButton.IsChecked = false;
                SqlConnectButton.Content = "连接数据库";
                SqlConnectEllipse.Fill = Brushes.Gray;
            }
            string command = $"create table {WorkSheet} ( id int identity(1, 1) primary key, type varchar(MAX), protocol varchar(MAX), address varchar(MAX), data varchar(MAX), statue varchar(MAX))";
            // 创建数据表（如果已经存在就不创建了）
            ExecuteSqlCommand(command, CalibrationSqlConnect);
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
                CalibrationSqlConnect.Close();
                MessageBox.Show($"与服务器{SqlServer}断开连接：操作数据库为{SqlDatabase}。", "断开连接", MessageBoxButton.OK,
                    MessageBoxImage.Information);
                SqlConnectButton.Content = "连接数据库";
                SqlConnectEllipse.Fill = Brushes.Gray;
            }
            catch (Exception ex)
            {
                MessageBox.Show("断开连接失败：" + ex.Message, "断开连接", MessageBoxButton.OK,
                    MessageBoxImage.Exclamation);
                SqlConnectButton.IsChecked = true;
                SqlConnectButton.Content = "断开数据库";
                SqlConnectEllipse.Fill = Brushes.Green;
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
                var returnValue = ExecuteSqlCommand(command, sql);
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


    }

}