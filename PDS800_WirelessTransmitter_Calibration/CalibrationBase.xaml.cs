using Microsoft.Win32;
using System;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using System.Data;
using System.Data.SqlClient;
using System.Collections;
using System.Diagnostics;
using Microsoft.Research.DynamicDataDisplay.DataSources;
using Microsoft.Research.DynamicDataDisplay;
using System.Reflection;

namespace PDS800_WirelessTransmitter_Calibration
{
    /// <summary>
    /// CalibrationBaseUserControl.xaml 的交互逻辑
    /// </summary>
    public partial class CalibrationBase : UserControl
    {
        #region 基本定义
        // 串行端口
        private SerialPort serialPort = new SerialPort();
        // 自动发送定时器
        private DispatcherTimer autoSendTimer = new DispatcherTimer();
        // 自动检测定时器
        private DispatcherTimer autoDetectionTimer = new DispatcherTimer();
        // 自动获取当前时间定时器
        private DispatcherTimer GetCurrentTimer = new DispatcherTimer();
        // 字符编码设定
        private Encoding setEncoding = Encoding.Default;

        // 变量定义
        // 日期
        private string dateStr = "";
        // 时刻
        private string timeStr = "";
        //// 发送和接收队列
        //private Queue receiveData = new Queue();
        //private Queue sendData = new Queue();
        // 发送和接收字节数
        private uint receiveBytesCount = 0;
        private uint sendBytesCount = 0;
        // 发送和接收次数
        private uint receiveCount = 0;
        private uint sendCount = 0;
        // 帧头
        private string frameHeader;
        // 长度域
        private string frameLength;
        // 命令域
        private string frameCommand;
        // 数据地址域
        private string frameAddress;
        // 非解析数据
        private string frameUnparsed;
        // 数据内容域
        private string frameContent;
        // 校验码
        private string frameCRC;
        // 实时数据
        private double realTimeData = 0.0;
        // Lora标志
        private bool LoRaFlag;
        /// <summary>
        /// 关闭窗口
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>        
        private void WindowClosed(object sender, ExecutedRoutedEventArgs e)
        {

        }

        #endregion

        #region 串口初始化/串口变更检测
        /// <summary>
        /// 串口初始化
        /// </summary>
        public CalibrationBase()
        {
            // 初始化组件
            InitializeComponent();
            // 检测和添加串口
            AddPortName();
            // 开启串口检测定时器，并设置自动检测1秒1次
            autoDetectionTimer.Tick += new EventHandler(AutoDetectionTimer_Tick);
            autoDetectionTimer.Interval = new TimeSpan(0, 0, 0, 1, 0);
            autoDetectionTimer.Start();
            // 开启当前时间定时器，并设置自动检测100毫秒1次
            GetCurrentTimer.Tick += new EventHandler(GetCurrentTime);
            GetCurrentTimer.Interval = new TimeSpan(0, 0, 0, 0, 100);
            GetCurrentTimer.Start();
            // 设置自动发送定时器，并设置自动检测100毫秒1次
            autoSendTimer.Tick += new EventHandler(AutoSendTimer_Tick);
            // 设置定时时间，开启定时器
            autoSendTimer.Interval = new TimeSpan(0, 0, 0, 0, Convert.ToInt32(autoSendCycleTextBox.Text));
            // 设置状态栏提示
            statusTextBlock.Text = "准备就绪";
        }
        /// <summary>
        /// 显示当前时间
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public void GetCurrentTime(object sender, EventArgs e)
        {
            dateStr = DateTime.Now.ToString("yyyy-MM-dd");
            timeStr = DateTime.Now.ToString("HH:mm:ss");
            operationTime.Text = dateStr + " " + timeStr;
        }
        /// <summary>
        /// 在初始化串口时进行串口检测和添加
        /// </summary>
        private void AddPortName()
        {
            // 检测有效串口，去掉重复串口
            string[] serialPortName = SerialPort.GetPortNames().Distinct().ToArray();
            // 在有效串口号中遍历当前打开的串口号
            foreach (string name in serialPortName)
            {
                // 如果检测到的串口不存在于portNameComboBox中，则添加
                if (portNameComboBox.Items.Contains(name) == false)
                {
                    portNameComboBox.Items.Add(name);
                }

            }
            portNameComboBox.SelectedIndex = 0;
        }
        /// <summary>
        /// 在串口运行时进行串口检测和更改
        /// </summary>
        /// <param name="sender">事件源的对象</param>
        /// <param name="e">事件数据的对象</param>
        private void AutoDetectionTimer_Tick(object sender, EventArgs e)
        {
            try
            {
                // 检测有效串口，去掉重复串口
                string[] serialPortName = SerialPort.GetPortNames().Distinct().ToArray();
                // 如果没有运行
                //AddPortName();
                // 如果正在运行
                if (turnOnButton.IsChecked == true)
                {
                    // 在有效串口号中遍历当前打开的串口号
                    foreach (string name in serialPortName)
                    {
                        // 如果找到串口，说明串口仍然有效，跳出循环
                        if (serialPort.PortName == name)
                            return;
                    }
                    // 如果找不到, 说明串口失效了，关闭串口并移除串口名
                    turnOnButton.IsChecked = false;
                    portNameComboBox.Items.Remove(serialPort.PortName);
                    portNameComboBox.SelectedIndex = 0;
                    // 输出提示信息
                    statusTextBlock.Text = "串口失效，已自动断开";
                }
                else
                {
                    // 检查有效串口和ComboBox中的串口号个数是否不同
                    if (portNameComboBox.Items.Count != serialPortName.Length)
                    {
                        // 串口数不同，清空ComboBox
                        portNameComboBox.Items.Clear();
                        // 重新添加有效串口
                        foreach (string name in serialPortName)
                        {
                            portNameComboBox.Items.Add(name);
                        }
                        portNameComboBox.SelectedIndex = -1;
                        // 输出提示信息
                        statusTextBlock.Text = "串口列表已更新！";
                    }
                }
            }
            catch (Exception ex)
            {
                string str = ex.StackTrace;
                Console.WriteLine(str);
                turnOnButton.IsChecked = false;
                statusTextBlock.Text = "串口检测错误！";

            }
        }
        #endregion

        #region 打开/关闭串口
        /// <summary>
        /// 串口配置面板
        /// </summary>
        /// <param name="state">使能状态</param>
        private void SerialSettingControlState(bool state)
        {
            // state状态为true时, ComboBox不可用, 反之可用
            portNameComboBox.IsEnabled = state;
            baudRateComboBox.IsEnabled = state;
            parityComboBox.IsEnabled = state;
            dataBitsComboBox.IsEnabled = state;
            stopBitsComboBox.IsEnabled = state;
        }
        /// <summary>
        /// 打开串口按钮
        /// </summary>
        /// <param name="sender">事件源的对象</param>
        /// <param name="e">事件数据的对象</param>
        private void TurnOnButton_Checked(object sender, RoutedEventArgs e)
        {
            try
            {
                // 获取面板中的配置, 并设置到串口属性中
                serialPort.PortName = portNameComboBox.Text;
                serialPort.BaudRate = Convert.ToInt32(baudRateComboBox.Text);
                serialPort.Parity = (Parity)Enum.Parse(typeof(Parity), parityComboBox.Text);
                serialPort.DataBits = Convert.ToInt16(dataBitsComboBox.Text);
                serialPort.StopBits = (StopBits)Enum.Parse(typeof(StopBits), stopBitsComboBox.Text);
                serialPort.Encoding = setEncoding;
                // 添加串口事件处理, 设置委托
                serialPort.DataReceived += new SerialDataReceivedEventHandler(ReceiveData);
                // 关闭串口配置面板, 开启串口, 变更按钮文本, 打开绿灯, 显示提示文字
                SerialSettingControlState(false);
                serialPort.Open();
                statusTextBlock.Text = "串口已开启";
                serialPortStatusEllipse.Fill = Brushes.Green;
                turnOnButton.Content = "关闭串口";
                // 设置超时
                serialPort.ReadTimeout = 500;
                serialPort.WriteTimeout = 500;
                // 清空缓冲区
                serialPort.DiscardInBuffer();
                serialPort.DiscardOutBuffer();
                // 使能作图区
                dataSource = new ObservableDataSource<Point>();
                Plot_Loaded();
            }
            catch (Exception ex)
            {
                string str = ex.StackTrace;
                Console.WriteLine(str);
                // 异常时显示提示文字
                statusTextBlock.Text = "开启串口出错！";
                serialPort.Close();
                autoSendTimer.Stop();
                turnOnButton.IsChecked = false;
                SerialSettingControlState(true);
            }
        }
        /// <summary>
        /// 关闭串口按钮
        /// </summary>
        /// <param name="sender">事件源的对象</param>
        /// <param name="e">事件数据的对象</param>
        private void TurnOnButton_Unchecked(object sender, RoutedEventArgs e)// 关闭串口
        {
            try
            {
                // 关闭端口, 关闭自动发送定时器, 使能串口配置面板, 变更按钮文本, 关闭绿灯, 显示提示文字 
                serialPort.Close();
                autoSendTimer.Stop();
                SerialSettingControlState(true);
                statusTextBlock.Text = "串口已关闭";
                serialPortStatusEllipse.Fill = Brushes.Gray;
                turnOnButton.Content = "打开串口";
                // 关闭作图
                plotter.Children.RemoveAll(typeof(LineGraph));
                plotPointX = 1;
                // 关闭连接

            }
            catch (Exception ex)
            {
                string str = ex.StackTrace;
                Console.WriteLine(str);
                // 异常时显示提示文字
                statusTextBlock.Text = "关闭串口出错！";
                turnOnButton.IsChecked = true;
            }
        }
        #endregion

        #region 串口数据接收处理/窗口显示清空功能
        string brokenFrameEnd = "";
        string receiveText = "";
        /// <summary>
        /// 接收串口数据, 转换为16进制字符串, 传递到显示功能
        /// </summary>
        /// <param name="sender">事件源的对象</param>
        /// <param name="e">事件数据的对象</param>
        public void ReceiveData(object sender, SerialDataReceivedEventArgs e)
        {
            receiveCount++;
            // Console.WriteLine("接收" + receiveCount + "次");
            // 读取缓冲区内所有字节
            try
            {
                byte[] receiveBuffer = new byte[serialPort.BytesToRead];
                serialPort.Read(receiveBuffer, 0, receiveBuffer.Length);
                // 字符串转换为十六进制字符串
                receiveText = BytestoHexStr(receiveBuffer);
            }
            catch (Exception ex)
            {
                string str = ex.StackTrace;
                Console.WriteLine(str);
                receiveText = "";
            }
            // 加入上一次断帧尾数
            receiveText = (brokenFrameEnd + " " + receiveText).Trim(' ');
            // 对字符串进行断帧
            string[] segmentationStr = MessageSegmentationExtraction(receiveText);
            // 断帧情况判断
            switch (segmentationStr.Length)
            {
                // 如果只有一帧，要不是空帧，要不是废帧
                case 1:
                    brokenFrameEnd = segmentationStr[0];
                    break;
                // 如果有两帧，只有一个头帧和一个可用帧
                case 2:
                    AvailablMessageHandler(segmentationStr[1]);
                    break;
                // 如果有多于三帧，则头帧，可用帧，尾帧都有
                default:
                    string[] useStr = new string[segmentationStr.Length - 2];
                    Array.Copy(segmentationStr, 1, useStr, 0, segmentationStr.Length - 2);
                    foreach (var item in useStr)
                    {
                        AvailablMessageHandler(item);
                    }
                    brokenFrameEnd = segmentationStr[segmentationStr.Length - 1];
                    break;
            }

            //// 传参 (Invoke方法暂停工作线程, BeginInvoke方法不暂停)
            //AvailablMessageHandler(receiveText);
        }
        /// <summary>
        /// 根据帧头和内容不同进行不同方法处理
        /// </summary>
        /// <param name="receiveText"></param>
        /// <returns></returns>
        private void AvailablMessageHandler(string receiveText)
        {
            if (receiveText.Length >= 2)
            {
                try
                {
                    switch (receiveText.Substring(0, 2))
                    {
                        // Zigbee 四信解析
                        case "FE":
                            {
                                // 不解析确认帧，只显示
                                if (receiveText.Length >= 8 && receiveText.Substring(receiveText.Length - 8, 5) == "F0 55")
                                {
                                    connectFlag = false;
                                    establishConnectionButton.IsEnabled = true;
                                    statusReceiveByteTextBlock.Dispatcher.Invoke(PartialPanelDisplay(receiveText));
                                    break;
                                }
                                // 如果是能解析的帧（常规数据帧或基本参数帧），就全面板显示
                                if (((receiveText.Length + 1) / 3) == 27 || ((receiveText.Length + 1) / 3) == 81)
                                {
                                    statusReceiveByteTextBlock.Dispatcher.Invoke(FullPanelDisplay(receiveText));
                                }
                                // 如果是不能解析的帧，就部分显示
                                else if (receiveText.Replace(" ", "") != "")
                                {
                                    statusReceiveByteTextBlock.Dispatcher.Invoke(PartialPanelDisplay(receiveText));
                                }
                            }
                            break;
                        // Zigbee Digi和LoRa解析
                        case "7E":
                            {
                                // 不解析确认帧，只显示
                                if (receiveText.Length >= 8 && receiveText.Substring(receiveText.Length - 8, 5) == "F0 55")
                                {
                                    connectFlag = false;
                                    establishConnectionButton.IsEnabled = true;
                                    statusReceiveByteTextBlock.Dispatcher.Invoke(PartialPanelDisplay(receiveText));
                                    break;
                                }
                                // 如果是能解析的LoRa帧（常规数据帧或基本参数帧），就全面板显示
                                if (((receiveText.Length + 1) / 3) == 24 || ((receiveText.Length + 1) / 3) == 78)
                                {
                                    LoRaFlag = true;
                                    statusReceiveByteTextBlock.Dispatcher.Invoke(FullPanelDisplay(receiveText));
                                }
                                // 如果是能解析的Digi帧（常规数据帧或基本参数帧），就全面板显示
                                else if (((receiveText.Length + 1) / 3) == 42 || ((receiveText.Length + 1) / 3) == 96)
                                {
                                    LoRaFlag = false;
                                    statusReceiveByteTextBlock.Dispatcher.Invoke(FullPanelDisplay(receiveText));
                                }
                                // 如果是不能解析的帧，就部分显示
                                else if (receiveText.Replace(" ", "") != "")
                                {
                                    statusReceiveByteTextBlock.Dispatcher.Invoke(PartialPanelDisplay(receiveText));
                                }

                            }
                            break;

                        default:
                            statusReceiveByteTextBlock.Dispatcher.Invoke(PartialPanelDisplay(receiveText));
                            break;
                    }
                }
                catch (Exception ex)
                {
                    string str = ex.StackTrace;
                    Console.WriteLine(str);
                }
            }
        }

        private Action PartialPanelDisplay(string receiveText)
        {
            return new Action(delegate
            {
                ShowReceiveData(receiveText);
            });
        }

        private Action FullPanelDisplay(string receiveText)
        {
            return new Action(delegate
            {
                ShowReceiveData(receiveText);
                InstumentDataSegmentionText(receiveText);
                ShowParseParameter(receiveText);
                SendConfirmationFrame(receiveText);
            });
        }

        /// <summary>
        /// 自动发送确认帧
        /// </summary>
        /// <param name="receiveText"></param>
        private void SendConfirmationFrame(string receiveText)
        {
            // 如果不需要建立连接，发送常规确认帧
            if (connectFlag == false)
            {
                try
                {
                    switch (receiveText.Substring(0, 2))
                    {
                        case "FE":
                            {
                                if (((receiveText.Length + 1) / 3) == 27)
                                {
                                    string str = RegularDataConfirmationFrame();
                                    SerialPortSend(str);
                                }
                                else if (((receiveText.Length + 1) / 3) == 81)
                                {
                                    string str = BasicInformationConfirmationFrame();
                                    SerialPortSend(str);
                                }
                            }
                            break;
                        case "7E":
                            {
                                if (((receiveText.Length + 1) / 3) == 42 || ((receiveText.Length + 1) / 3) == 24)
                                {
                                    string str = RegularDataConfirmationFrame();
                                    SerialPortSend(str);
                                }
                                else if (((receiveText.Length + 1) / 3) == 96 || ((receiveText.Length + 1) / 3) == 78)
                                {
                                    string str = BasicInformationConfirmationFrame();
                                    SerialPortSend(str);
                                }
                            }
                            break;
                        default:
                            break;
                    }

                }
                catch (Exception ex)
                {
                    string str = ex.StackTrace;
                    Console.WriteLine(str);
                }
            }
            // 如果需要建立连接，用建立连接帧替代确认帧
            else
            {
                try
                {
                    switch (receiveText.Substring(0, 2))
                    {
                        case "FE":
                            {
                                if (((receiveText.Length + 1) / 3) == 27)
                                {
                                    string str = EstablishBuild_Text();
                                    SerialPortSend(str);
                                }
                            }
                            break;
                        case "7E":
                            {

                                if (((receiveText.Length + 1) / 3) == 42 || ((receiveText.Length + 1) / 3) == 24)
                                {
                                    string str = EstablishBuild_Text();
                                    SerialPortSend(str);
                                }
                                else if (((receiveText.Length + 1) / 3) == 96 || ((receiveText.Length + 1) / 3) == 78)
                                {
                                    string str = BasicInformationConfirmationFrame();
                                    SerialPortSend(str);
                                }
                            }
                            break;
                        default:
                            break;
                    }

                }
                catch (Exception ex)
                {
                    string str = ex.StackTrace;
                    Console.WriteLine(str);
                }
            }

        }

        public void PrintValues(IEnumerable myCollection)
        {
            foreach (object obj in myCollection)
            {
                Console.Write("{0}", obj);
            }

            Console.WriteLine();
        }
        /// <summary>
        /// 接收窗口显示功能
        /// </summary>
        /// <param name="receiveText">需要窗口显示的字符串</param>
        private void ShowReceiveData(string receiveText)
        {
            // 更新接收字节数           
            receiveBytesCount += (uint)((receiveText.Length + 1) / 3);
            statusReceiveByteTextBlock.Text = receiveBytesCount.ToString();
            // 在接收窗口中显示字符串
            if (receiveText.Replace(" ", "").Length >= 0)
            {
                // 接收窗口自动清空
                if (autoClearCheckBox.IsChecked == true)
                {
                    displayTextBox.Clear();
                }
                displayTextBox.AppendText(DateTime.Now.ToString() + " <-- " + receiveText + "\r\n");
                displayScrollViewer.ScrollToEnd();
            }

        }
        /// <summary>
        /// 仪表数据分段功能
        /// </summary>
        /// <param name="receiveText"></param>
        public void InstumentDataSegmentionText(string receiveText)
        {
            // 仪表上行数据分段
            try
            {
                switch (receiveText.Substring(0, 2))
                {
                    case "FE":
                        {
                            // 帧头 (1位)
                            frameHeader = receiveText.Substring(0 * 3, (1 * 3) - 1);
                            // 长度域 (1位, 最长为FF = 255)
                            frameLength = receiveText.Substring((0 + 1) * 3, (1 * 3) - 1);
                            // 命令域 (2位)
                            frameCommand = receiveText.Substring((0 + 1 + 1) * 3, (2 * 3) - 1);
                            // 数据域 (长度域指示长度)
                            // 数据地址域 (2位)
                            frameAddress = receiveText.Substring((0 + 1 + 1 + 2) * 3, (2 * 3) - 1);
                            // 数据内容域 (去掉头6位，尾1位)
                            frameContent = receiveText.Substring((6 * 3), receiveText.Length - (6 * 3) - 3);
                            // 校验码 (1位)
                            frameCRC = receiveText.Substring(receiveText.Length - 2, 2);
                        }
                        break;
                    case "7E":
                        {
                            // 如果是LoRa
                            if (LoRaFlag)
                            {
                                // 帧头 (1位)
                                frameHeader = receiveText.Substring(0 * 3, (1 * 3) - 1);
                                // 长度域 (2位, 最长为FF = 65535)
                                frameLength = receiveText.Substring((0 + 1) * 3, (2 * 3) - 1);
                                // 命令域 (0位，指示是否收到数据)
                                frameCommand = "";
                                // 数据地址域 (0位)
                                frameAddress = "";
                                // 非解析帧 (0位)
                                frameUnparsed = receiveText.Substring((0 + 1 + 2 + 1 + 8) * 3, (9 * 3) - 1);
                                // 数据内容域 (去掉头3位，尾1位)
                                frameContent = receiveText.Substring((3 * 3), receiveText.Length - (3 * 3) - 3);
                                // 校验码 (1位)
                                frameCRC = receiveText.Substring(receiveText.Length - 2, 2);
                            }
                            // 如果是Digi
                            else
                            {
                                // 帧头 (1位)
                                frameHeader = receiveText.Substring(0 * 3, (1 * 3) - 1);
                                // 长度域 (2位, 最长为FF = 65535)
                                frameLength = receiveText.Substring((0 + 1) * 3, (2 * 3) - 1);
                                // 命令域 (1位，指示是否收到数据)
                                frameCommand = receiveText.Substring((0 + 1 + 2) * 3, (1 * 3) - 1);
                                // 数据地址域 (8位)
                                frameAddress = receiveText.Substring((0 + 1 + 2 + 1) * 3, (8 * 3) - 1);
                                // 非解析帧 (9位)
                                frameUnparsed = receiveText.Substring((0 + 1 + 2 + 1 + 8) * 3, (9 * 3) - 1);
                                // 数据内容域 (去掉头21位，尾1位)
                                frameContent = receiveText.Substring((21 * 3), receiveText.Length - (21 * 3) - 3);
                                // 校验码 (1位)
                                frameCRC = receiveText.Substring(receiveText.Length - 2, 2);
                            }

                        }
                        break;
                    default:
                        break;
                }
            }
            catch (Exception ex)
            {
                string str = ex.StackTrace;
                Console.WriteLine(str);
                // 异常时显示提示文字
                statusTextBlock.Text = "文本解析出错！";
            }
        }

        /// <summary>
        /// 仪表参数解析面板显示功能
        /// </summary>
        /// <param name="receiveText"></param>
        private void ShowParseParameter(string receiveText)
        {
            // 面板清空
            ParseParameterClear();
            // 仪表参数解析面板写入
            try
            {
                switch (receiveText.Substring(0, 2))
                {
                    case "FE":
                        {
                            //字符串校验
                            string j = CalCheckCode_FE(receiveText);
                            if (j == frameCRC)
                            {
                                resCRC.Text = "通过";
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
                                        resProtocol.Text = "ZigBee SZ9-GRM V3.01油田专用通讯协议（国产四信）";
                                        resProtocolDockPanel.Visibility = Visibility.Visible;
                                        //        break;
                                        //    default:
                                        //        resProtocol.Text = "未知";
                                        //        resProtocol.Foreground = new SolidColorBrush(Colors.Red);
                                        //        break;
                                        //}
                                    }
                                    catch (Exception ex)
                                    {
                                        string str = ex.StackTrace;
                                        Console.WriteLine(str);
                                        // 异常时显示提示文字
                                        statusTextBlock.Text = "通信协议解析出错！";
                                        turnOnButton.IsChecked = false;
                                        return;
                                    }
                                    // 网络地址
                                    try
                                    {
                                        string frameContentAddress = (frameAddress.Substring(3, 2) + frameAddress.Substring(0, 2)).Replace(" ", "");
                                        int intFrameContentAddress = Convert.ToInt32(frameContentAddress, 16);
                                        resAddress.Text = intFrameContentAddress.ToString();
                                        resAddress.Visibility = Visibility.Visible;
                                    }
                                    catch (Exception ex)
                                    {
                                        string str = ex.StackTrace;
                                        Console.WriteLine(str);
                                        // 异常时显示提示文字
                                        statusTextBlock.Text = "网络地址解析出错！";
                                        turnOnButton.IsChecked = false;
                                        return;
                                    }
                                    // 厂商号
                                    try
                                    {
                                        string frameContentVendor = frameContent.Substring(6, 5).Replace(" ", "");
                                        int intFrameContentVendor = Convert.ToInt32(frameContentVendor, 16);
                                        // 1 0x0001 厂商1
                                        // 2 0x0002 厂商2
                                        // 3 0x0003 厂商3
                                        // 4 ......
                                        // N 0x8001~0xFFFF 预留
                                        if (intFrameContentVendor > 0x0000 && intFrameContentVendor < 0x8001)
                                        {
                                            resVendor.Text = "厂商" + intFrameContentVendor;
                                        }
                                        else if (intFrameContentVendor >= 0x8001 && intFrameContentVendor <= 0xFFFF)
                                            resVendor.Text = "预留厂商";
                                        else
                                        {
                                            resVendor.Text = "未定义";
                                            resVendor.Foreground = new SolidColorBrush(Colors.Red);
                                        }
                                        resVendorDockPanel.Visibility = Visibility.Visible;
                                    }
                                    catch (Exception ex)
                                    {
                                        string str = ex.StackTrace;
                                        Console.WriteLine(str);
                                        // 异常时显示提示文字
                                        statusTextBlock.Text = "厂商号解析出错！";
                                        turnOnButton.IsChecked = false;
                                        return;
                                    }
                                    // 仪表类型
                                    try
                                    {
                                        string frameContentType = frameContent.Substring(12, 5).Replace(" ", "");
                                        int intFrameContentType = Convert.ToInt32(frameContentType, 16);
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
                                            case 0x0001: resType.Text = "无线一体化负荷"; break;
                                            case 0x0002: resType.Text = "无线压力"; break;
                                            case 0x0003: resType.Text = "无线温度"; break;
                                            case 0x0004: resType.Text = "无线电量"; break;
                                            case 0x0005: resType.Text = "无线角位移"; break;
                                            case 0x0006: resType.Text = "无线载荷"; break;
                                            case 0x0007: resType.Text = "无线扭矩"; break;
                                            case 0x0008: resType.Text = "无线动液面"; break;
                                            case 0x0009: resType.Text = "计量车"; break;
                                            case 0x000B: resType.Text = "无线压力温度一体化变送器"; break;
                                            case 0x1F00: resType.Text = "控制器(RTU)设备"; break;
                                            case 0x1F10: resType.Text = "手操器"; break;
                                            // 自定义
                                            case 0x2000: resType.Text = "温度型"; break;
                                            case 0x3000: resType.Text = "无线拉线位移校准传感器"; break;
                                            case 0x3001: resType.Text = "无线拉线位移功图校准传感器"; break;
                                            default: resType.Clear(); break;
                                        }
                                        while (resType.Text.Trim() == string.Empty)
                                        {
                                            if (intFrameContentType <= 0x4000 && intFrameContentType >= 0x3000)
                                            {
                                                resType.Text = "自定义";
                                            }
                                            else
                                            {
                                                resType.Text = "未定义";
                                                resType.Foreground = new SolidColorBrush(Colors.Red);
                                            }
                                        }
                                        resTypeDockPanel.Visibility = Visibility.Visible;
                                    }
                                    catch (Exception ex)
                                    {
                                        string str = ex.StackTrace;
                                        Console.WriteLine(str);
                                        // 异常时显示提示文字
                                        statusTextBlock.Text = "仪表类型解析出错！";
                                        turnOnButton.IsChecked = false;
                                        return;
                                    }
                                    // 仪表组号
                                    try
                                    {
                                        resGroup.Text = Convert.ToInt32(frameContent.Substring(18, 2).Replace(" ", ""), 16) + "组" + Convert.ToInt32(frameContent.Substring(21, 2).Replace(" ", ""), 16) + "号";
                                        resGroup.Visibility = Visibility.Visible;
                                    }
                                    catch (Exception ex)
                                    {
                                        string str = ex.StackTrace;
                                        Console.WriteLine(str);
                                        // 异常时显示提示文字
                                        statusTextBlock.Text = "仪表组号解析出错！";
                                        turnOnButton.IsChecked = false;
                                        return;
                                    }
                                    // 数据类型
                                    try
                                    {
                                        string frameContentFunctionData = frameContent.Substring(24, 5).Replace(" ", "");
                                        int intFrameContentFunctionData = Convert.ToInt32(frameContentFunctionData, 16);
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
                                            case 0x0000: resFunctionData.Text = "常规数据"; break;
                                            case 0x0010: resFunctionData.Text = "仪表参数"; break;
                                            case 0x0020: resFunctionData.Text = "读数据命令"; break;
                                            case 0x0100: resFunctionData.Text = "控制器参数写应答（控制器应答命令）"; break;
                                            case 0x0101: resFunctionData.Text = "控制器读仪表参数应答（控制器应答命令）"; break;
                                            case 0x0200: resFunctionData.Text = "控制器应答一体化载荷位移示功仪功图参数应答（控制器应答命令触发功图采集）"; break;
                                            case 0x0201: resFunctionData.Text = "控制器应答功图数据命令"; break;
                                            case 0x0202: resFunctionData.Text = "控制器读功图数据应答（控制器应答命令读已有功图）"; break;
                                            case 0x0300: resFunctionData.Text = "控制器(RTU)对仪表控制命令"; break;
                                            default: resType.Clear(); break;
                                        }
                                        while (resType.Text.Trim() == string.Empty)
                                        {

                                            if (intFrameContentFunctionData >= 0x400 && intFrameContentFunctionData <= 0x47f)
                                            {
                                                resFunctionData.Text = "配置协议命令";
                                            }
                                            else if (intFrameContentFunctionData >= 0x480 && intFrameContentFunctionData <= 0x5ff)
                                            {
                                                resFunctionData.Text = "标定协议命令";
                                            }
                                            else if (intFrameContentFunctionData >= 0x1000 && intFrameContentFunctionData <= 0x2000)
                                            {
                                                resFunctionData.Text = "厂家自定义数据类型";
                                            }
                                            else if (intFrameContentFunctionData >= 0x8000 && intFrameContentFunctionData <= 0xffff)
                                            {
                                                resFunctionData.Text = "预留";
                                            }
                                            else
                                            {
                                                resFunctionData.Text = "未定义";
                                                resFunctionData.Foreground = new SolidColorBrush(Colors.Red);
                                                break;
                                            }

                                        }
                                        resTypeDockPanel.Visibility = Visibility.Visible;
                                    }
                                    catch (Exception ex)
                                    {
                                        string str = ex.StackTrace;
                                        Console.WriteLine(str);
                                        // 异常时显示提示文字
                                        statusTextBlock.Text = "数据类型解析出错！";
                                        turnOnButton.IsChecked = false;
                                        return;
                                    }
                                }
                                // 无线仪表数据段
                                // 通信效率
                                try
                                {
                                    resSucRate.Text = Convert.ToInt32(frameContent.Substring(30, 2), 16).ToString() + "%";
                                    resSucRateDockPanel.Visibility = Visibility.Visible;
                                }
                                catch (Exception ex)
                                {
                                    string str = ex.StackTrace;
                                    Console.WriteLine(str);
                                    // 异常时显示提示文字
                                    statusTextBlock.Text = "通信效率解析出错！";
                                    turnOnButton.IsChecked = false;
                                    return;
                                }
                                // 电池电压
                                try
                                {
                                    resBatVol.Text = Convert.ToInt32(frameContent.Substring(33, 2), 16) + "%";
                                    resBatVolDockPanel.Visibility = Visibility.Visible;
                                }
                                catch (Exception ex)
                                {
                                    string str = ex.StackTrace;
                                    Console.WriteLine(str);
                                    // 异常时显示提示文字
                                    statusTextBlock.Text = "电池电压解析出错！";
                                    turnOnButton.IsChecked = false;
                                    return;
                                }
                                // 休眠时间
                                try
                                {
                                    int sleeptime = Convert.ToInt32(frameContent.Substring(36, 5).Replace(" ", ""), 16);
                                    resSleepTime.Text = sleeptime + "秒";
                                    resSleepTimeDockPanel.Visibility = Visibility.Visible;
                                    if (regularDataUpdateRate.Text == "")
                                    {
                                        regularDataUpdateRate.Text = Convert.ToString(sleeptime);
                                    }
                                }
                                catch (Exception ex)
                                {
                                    string str = ex.StackTrace;
                                    Console.WriteLine(str);
                                    // 异常时显示提示文字
                                    statusTextBlock.Text = "休眠时间解析出错！";
                                    turnOnButton.IsChecked = false;
                                    return;
                                }
                                // 仪表状态
                                try
                                {
                                    string frameStatue = frameContent.Substring(42, 5).Replace(" ", "");
                                    string binFrameStatue = Convert.ToString(Convert.ToInt32(frameStatue, 16), 2).PadLeft(8, '0');
                                    if (Convert.ToInt32(binFrameStatue.Replace(" ", ""), 2) != 0)
                                    {
                                        resStatue.Text = "故障";
                                        string failureMessage = "";
                                        int count = 0;
                                        // 1 Bit0 仪表故障
                                        // 2 Bit1 参数错误
                                        // 3 Bit2 电池欠压，日月协议中仍然保留
                                        // 4 Bit3 AI1 上限报警
                                        // 5 Bit4 AI1 下限报警
                                        // 6 Bit5 AI2 上限报警
                                        // 7 Bit6 AI2 下限报警
                                        // 8 Bit7 预留
                                        for (int a = 0; a < 8; a++)
                                        // 从第0位到第7位
                                        {
                                            if (binFrameStatue.Substring(a, 1) == "1")
                                            {
                                                switch (a)
                                                {
                                                    case 0: failureMessage += ++count + " 仪表故障\n"; break;
                                                    case 1: failureMessage += ++count + " 参数故障\n"; break;
                                                    case 2: failureMessage += ++count + " 电池欠压\n"; break;
                                                    case 3: failureMessage += ++count + " 压力上限报警\n"; break;
                                                    case 4: failureMessage += ++count + " 压力下限报警\n"; break;
                                                    case 5: failureMessage += ++count + " 温度上限报警\n"; break;
                                                    case 6: failureMessage += ++count + " 温度下限报警\n"; break;
                                                    case 7: failureMessage += ++count + " 未定义故障\n"; break;
                                                    default: failureMessage += "参数错误\n"; break;
                                                }
                                            }
                                        }
                                        statusTextBlock.Text = failureMessage;
                                        //string messageBoxText = "设备上报" + count + "个故障: \n" + failureMessage;
                                        //string caption = "设备故障";
                                        //MessageBoxButton button = MessageBoxButton.OK;
                                        //MessageBoxImage icon = MessageBoxImage.Error;
                                        //MessageBox.Show(messageBoxText, caption, button, icon);
                                    }
                                    else
                                    {
                                        resStatue.Text = "正常";
                                    }
                                    resStatueDockPanel.Visibility = Visibility.Visible;
                                }
                                catch (Exception ex)
                                {
                                    string str = ex.StackTrace;
                                    Console.WriteLine(str);
                                    // 异常时显示提示文字
                                    statusTextBlock.Text = "仪表状态解析出错！";
                                    turnOnButton.IsChecked = false;
                                    return;
                                }
                                // 运行时间
                                try
                                {
                                    resTime.Text = Convert.ToInt32(frameContent.Substring(48, 5).Replace(" ", ""), 16).ToString() + "小时";
                                    resTimeDockPanel.Visibility = Visibility.Visible;
                                }
                                catch (Exception ex)
                                {
                                    string str = ex.StackTrace;
                                    Console.WriteLine(str);
                                    // 异常时显示提示文字
                                    statusTextBlock.Text = "运行时间解析出错！";
                                    turnOnButton.IsChecked = false;
                                    return;
                                }
                                // 实时数据
                                try
                                {

                                    string frameresData = Convert.ToInt32(frameContent.Substring(54, 5).Replace(" ", ""), 16).ToString();
                                    resData.Text = frameresData;
                                    resDataDockPanel.Visibility = Visibility.Visible;
                                    try
                                    {
                                        realTimeData = Convert.ToDouble(frameresData);
                                    }
                                    catch { }
                                    AnimatedPlot();

                                }
                                catch (Exception ex)
                                {
                                    string str = ex.StackTrace;
                                    Console.WriteLine(str);
                                    // 异常时显示提示文字
                                    statusTextBlock.Text = "实时数据解析出错！";
                                    turnOnButton.IsChecked = false;
                                    return;
                                }
                            }
                            else
                            {
                                resCRC.Text = "未通过";
                                resCRC.Foreground = new SolidColorBrush(Colors.Red);
                            }
                            resCRCDockPanel.Visibility = Visibility.Visible;
                        }
                        break;
                    case "7E":
                        {
                            //字符串校验
                            string hexj = CalCheckCode_7E(receiveText);
                            if (hexj == frameCRC)
                            //if (true)
                            {
                                resCRC.Text = "通过";
                                // 校验成功写入其他解析参数
                                // 无线仪表数据域帧头
                                {
                                    // 通信协议
                                    try
                                    {
                                        // 1 0x0001 ZigBee（Digi International）
                                        //string frameProtocol = frameContent.Substring(0, 5).Replace(" ", "");
                                        //int intFrameProtocol = Convert.ToInt32(frameProtocol, 16);
                                        //switch (intFrameProtocol)
                                        //{
                                        //    case 0x0001:
                                        if (LoRaFlag)
                                        {
                                            resProtocol.Text = "LoRa（Semtech）";
                                        }
                                        else
                                        {
                                            resProtocol.Text = "ZigBee（Digi International）";
                                        }
                                        resProtocolDockPanel.Visibility = Visibility.Visible;
                                        //        break;
                                        //    default:
                                        //        resProtocol.Text = "未知";
                                        //        resProtocol.Foreground = new SolidColorBrush(Colors.Red);
                                        //        break;
                                        //}
                                    }
                                    catch (Exception ex)
                                    {
                                        string str = ex.StackTrace;
                                        Console.WriteLine(str);
                                        // 异常时显示提示文字
                                        statusTextBlock.Text = "通信协议解析出错！";
                                        turnOnButton.IsChecked = false;
                                        return;
                                    }
                                    // 网络地址
                                    try
                                    {
                                        if (LoRaFlag)
                                        {
                                            resAddress.Text = "透传模式";
                                            resAddressDockPanel.Visibility = Visibility.Visible;
                                        }
                                        else
                                        {
                                            string frameContentAddress = (frameAddress.Substring(6, 2) + frameAddress.Substring(3, 2)).Replace(" ", "");
                                            int intFrameContentAddress = Convert.ToInt32(frameContentAddress, 16);
                                            resAddress.Text = intFrameContentAddress.ToString();
                                            resAddressDockPanel.Visibility = Visibility.Visible;
                                        }

                                    }
                                    catch (Exception ex)
                                    {
                                        string str = ex.StackTrace;
                                        Console.WriteLine(str);
                                        // 异常时显示提示文字
                                        statusTextBlock.Text = "网络地址解析出错！";
                                        turnOnButton.IsChecked = false;
                                        return;
                                    }
                                    // 厂商号
                                    try
                                    {
                                        string frameContentVendor = frameContent.Substring(6, 5).Replace(" ", "");
                                        int intFrameContentVendor = Convert.ToInt32(frameContentVendor, 16);
                                        // 1 0x0001 厂商1
                                        // 2 0x0002 厂商2
                                        // 3 0x0003 厂商3
                                        // 4 ......
                                        // N 0x8001~0xFFFF 预留
                                        if (intFrameContentVendor > 0x0000 && intFrameContentVendor < 0x8001)
                                        {
                                            resVendor.Text = "厂商" + intFrameContentVendor;
                                        }
                                        else if (intFrameContentVendor >= 0x8001 && intFrameContentVendor <= 0xFFFF)
                                            resVendor.Text = "预留厂商";
                                        else
                                        {
                                            resVendor.Text = "未定义";
                                            resVendor.Foreground = new SolidColorBrush(Colors.Red);
                                        }
                                        resVendorDockPanel.Visibility = Visibility.Visible;
                                    }
                                    catch (Exception ex)
                                    {
                                        string str = ex.StackTrace;
                                        Console.WriteLine(str);
                                        // 异常时显示提示文字
                                        statusTextBlock.Text = "厂商号解析出错！";
                                        turnOnButton.IsChecked = false;
                                        return;
                                    }
                                    // 仪表类型
                                    try
                                    {
                                        string frameContentType = frameContent.Substring(12, 5).Replace(" ", "");
                                        int intFrameContentType = Convert.ToInt32(frameContentType, 16);
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

                                            case 0x0001: resType.Text = "无线一体化负荷"; break;
                                            case 0x0002: resType.Text = "无线压力"; break;
                                            case 0x0003: resType.Text = "无线温度"; break;
                                            case 0x0004: resType.Text = "无线电量"; break;
                                            case 0x0005: resType.Text = "无线角位移"; break;
                                            case 0x0006: resType.Text = "无线载荷"; break;
                                            case 0x0007: resType.Text = "无线扭矩"; break;
                                            case 0x0008: resType.Text = "无线动液面"; break;
                                            case 0x0009: resType.Text = "计量车"; break;
                                            case 0x000B: resType.Text = "无线压力温度一体化变送器"; break;
                                            case 0x1F00: resType.Text = "控制器(RTU)设备"; break;
                                            case 0x1F10: resType.Text = "手操器"; break;
                                            // 自定义
                                            case 0x2000: resType.Text = "温度型"; break;
                                            case 0x3000: resType.Text = "无线拉线位移校准传感器"; break;
                                            case 0x3001: resType.Text = "无线拉线位移功图校准传感器"; break;
                                            default: resType.Clear(); break;
                                        }
                                        while (resType.Text.Trim() == string.Empty)
                                        {
                                            if (intFrameContentType <= 0x4000 && intFrameContentType >= 0x3000)
                                            {
                                                resType.Text = "自定义";
                                            }
                                            else
                                            {
                                                resType.Text = "未定义";
                                                resType.Foreground = new SolidColorBrush(Colors.Red);
                                            }
                                        }
                                        resTypeDockPanel.Visibility = Visibility.Visible;
                                    }
                                    catch (Exception ex)
                                    {
                                        string str = ex.StackTrace;
                                        Console.WriteLine(str);
                                        // 异常时显示提示文字
                                        statusTextBlock.Text = "仪表类型解析出错！";
                                        turnOnButton.IsChecked = false;
                                        return;
                                    }
                                    // 仪表组号
                                    try
                                    {
                                        resGroup.Text = Convert.ToInt32(frameContent.Substring(18, 2).Replace(" ", ""), 16) + "组" + Convert.ToInt32(frameContent.Substring(21, 2).Replace(" ", ""), 16) + "号";
                                        resGroupDockPanel.Visibility = Visibility.Visible;
                                    }
                                    catch (Exception ex)
                                    {
                                        string str = ex.StackTrace;
                                        Console.WriteLine(str);
                                        // 异常时显示提示文字
                                        statusTextBlock.Text = "仪表组号解析出错！";
                                        turnOnButton.IsChecked = false;
                                        return;
                                    }
                                    // 根据不同数据类型解析其他数据
                                    try
                                    {
                                        string frameContentFunctionData = frameContent.Substring(24, 5).Replace(" ", "");
                                        int intFrameContentFunctionData = Convert.ToInt32(frameContentFunctionData, 16);
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
                                                resFunctionData.Text = "常规数据（仪表实时数据）";
                                                if (((receiveText.Length + 1) / 3) == 42 || ((receiveText.Length + 1) / 3) == 24)
                                                {
                                                    // 无线仪表数据段
                                                    // 通信效率
                                                    try
                                                    {
                                                        resSucRate.Text = Convert.ToInt32(frameContent.Substring(30, 2), 16).ToString() + "%";
                                                        resSucRateDockPanel.Visibility = Visibility.Visible;
                                                    }
                                                    catch (Exception ex)
                                                    {
                                                        string str = ex.StackTrace;
                                                        Console.WriteLine(str);
                                                        // 异常时显示提示文字
                                                        statusTextBlock.Text = "通信效率解析出错！";
                                                        turnOnButton.IsChecked = false;
                                                        return;
                                                    }
                                                    // 电池电压
                                                    try
                                                    {
                                                        resBatVol.Text = Convert.ToInt32(frameContent.Substring(33, 2), 16) + "%";
                                                        resBatVolDockPanel.Visibility = Visibility.Visible;
                                                    }
                                                    catch (Exception ex)
                                                    {
                                                        string str = ex.StackTrace;
                                                        Console.WriteLine(str);
                                                        // 异常时显示提示文字
                                                        statusTextBlock.Text = "电池电压解析出错！";
                                                        turnOnButton.IsChecked = false;
                                                        return;
                                                    }
                                                    // 休眠时间
                                                    try
                                                    {
                                                        int sleeptime = Convert.ToInt32(frameContent.Substring(36, 5).Replace(" ", ""), 16);
                                                        resSleepTime.Text = sleeptime + "秒";
                                                        resSleepTimeDockPanel.Visibility = Visibility.Visible;
                                                        if (regularDataUpdateRate.Text == "")
                                                        {
                                                            regularDataUpdateRate.Text = Convert.ToString(sleeptime);
                                                        }
                                                    }
                                                    catch (Exception ex)
                                                    {
                                                        string str = ex.StackTrace;
                                                        Console.WriteLine(str);
                                                        // 异常时显示提示文字
                                                        statusTextBlock.Text = "休眠时间解析出错！";
                                                        turnOnButton.IsChecked = false;
                                                        return;
                                                    }
                                                    // 仪表状态
                                                    try
                                                    {
                                                        string frameStatue = frameContent.Substring(42, 5).Replace(" ", "");
                                                        string binFrameStatue = Convert.ToString(Convert.ToInt32(frameStatue, 16), 2).PadLeft(8, '0');
                                                        if (Convert.ToInt32(binFrameStatue.Replace(" ", ""), 2) != 0)
                                                        {
                                                            resStatue.Text = "故障";
                                                            string failureMessage = "";
                                                            int count = 0;
                                                            // 1 Bit0 仪表故障
                                                            // 2 Bit1 参数错误
                                                            // 3 Bit2 电池欠压，日月协议中仍然保留
                                                            // 4 Bit3 AI1 上限报警
                                                            // 5 Bit4 AI1 下限报警
                                                            // 6 Bit5 AI2 上限报警
                                                            // 7 Bit6 AI2 下限报警
                                                            // 8 Bit7 预留
                                                            for (int a = 0; a < 8; a++)
                                                            // 从第0位到第7位
                                                            {
                                                                if (binFrameStatue.Substring(a, 1) == "1")
                                                                {
                                                                    switch (a)
                                                                    {
                                                                        case 0: failureMessage += ++count + " 仪表故障\n"; break;
                                                                        case 1: failureMessage += ++count + " 参数故障\n"; break;
                                                                        case 2: failureMessage += ++count + " 电池欠压\n"; break;
                                                                        case 3: failureMessage += ++count + " 压力上限报警\n"; break;
                                                                        case 4: failureMessage += ++count + " 压力下限报警\n"; break;
                                                                        case 5: failureMessage += ++count + " 温度上限报警\n"; break;
                                                                        case 6: failureMessage += ++count + " 温度下限报警\n"; break;
                                                                        case 7: failureMessage += ++count + " 未定义故障\n"; break;
                                                                        default: failureMessage += "参数错误\n"; break;
                                                                    }
                                                                }
                                                            }
                                                            statusTextBlock.Text = failureMessage;
                                                            //string messageBoxText = "设备上报" + count + "个故障: \n" + failureMessage;
                                                            //string caption = "设备故障";
                                                            //MessageBoxButton button = MessageBoxButton.OK;
                                                            //MessageBoxImage icon = MessageBoxImage.Error;
                                                            //MessageBox.Show(messageBoxText, caption, button, icon);
                                                        }
                                                        else
                                                        {
                                                            resStatue.Text = "正常";
                                                        }
                                                        resStatueDockPanel.Visibility = Visibility.Visible;
                                                    }
                                                    catch (Exception ex)
                                                    {
                                                        string str = ex.StackTrace;
                                                        Console.WriteLine(str);
                                                        // 异常时显示提示文字
                                                        statusTextBlock.Text = "仪表状态解析出错！";
                                                        turnOnButton.IsChecked = false;
                                                        return;
                                                    }
                                                    // 运行时间
                                                    try
                                                    {
                                                        resTime.Text = Convert.ToInt32(frameContent.Substring(48, 5).Replace(" ", ""), 16).ToString() + "小时";
                                                        resTimeDockPanel.Visibility = Visibility.Visible;
                                                    }
                                                    catch (Exception ex)
                                                    {
                                                        string str = ex.StackTrace;
                                                        Console.WriteLine(str);
                                                        // 异常时显示提示文字
                                                        statusTextBlock.Text = "运行时间解析出错！";
                                                        turnOnButton.IsChecked = false;
                                                        return;
                                                    }
                                                    // 实时数据
                                                    try
                                                    {

                                                        //string frameresData = frameContent.Substring(54, 5).Replace(" ", "").TrimStart('0');
                                                        //resData.Text = Convert.ToInt32(frameresData, 16) + "MPa";
                                                        // 十六进制字符串转换为浮点数字符串
                                                        string frameresData = frameContent.Substring(48, 11).Replace(" ", "");
                                                        float flFrameData = HexStrToFloat(frameresData);
                                                        realTimeData = flFrameData;
                                                        // 单位类型
                                                        string frameContentType = frameContent.Substring(12, 5).Replace(" ", "");
                                                        int intFrameContentType = Convert.ToInt32(frameContentType, 16);
                                                        string type = "";
                                                        //switch (intFrameContentType)
                                                        //{
                                                        //    case 0x0002: type = "Pa"; break;
                                                        //    case 0x0003: type = "℃"; break;
                                                        //    default: type = ""; break;
                                                        //}
                                                        resData.Text = flFrameData.ToString() + type;
                                                        resDataDockPanel.Visibility = Visibility.Visible;
                                                        AnimatedPlot();
                                                    }
                                                    catch (Exception ex)
                                                    {
                                                        string str = ex.StackTrace;
                                                        Console.WriteLine(str);
                                                        // 异常时显示提示文字
                                                        statusTextBlock.Text = "实时数据解析出错！";
                                                        turnOnButton.IsChecked = false;
                                                        return;
                                                    }

                                                }
                                                break;
                                            case 0x0010:
                                                resFunctionData.Text = "常规数据（仪表基本参数）";
                                                if (((receiveText.Length + 1) / 3) == 96 || ((receiveText.Length + 1) / 3) == 78)
                                                {
                                                    // 无线仪表数据段
                                                    // 仪表型号
                                                    try
                                                    {
                                                        resModel.Text = frameContent.Substring(30, 23);
                                                        resModelDockPanel.Visibility = Visibility.Visible;
                                                    }
                                                    catch (Exception ex)
                                                    {
                                                        string str = ex.StackTrace;
                                                        Console.WriteLine(str);
                                                        // 异常时显示提示文字
                                                        statusTextBlock.Text = "仪表型号解析出错！";
                                                        turnOnButton.IsChecked = false;
                                                        return;
                                                    }
                                                    // 系列号
                                                    try
                                                    {
                                                        resFirmwareVersion.Text = frameContent.Substring(54, 47);
                                                        resFirmwareVersionDockPanel.Visibility = Visibility.Visible;
                                                    }
                                                    catch (Exception ex)
                                                    {
                                                        string str = ex.StackTrace;
                                                        Console.WriteLine(str);
                                                        // 异常时显示提示文字
                                                        statusTextBlock.Text = "系列号解析出错！";
                                                        turnOnButton.IsChecked = false;
                                                        return;
                                                    }
                                                    // 固件版本
                                                    try
                                                    {
                                                        resFirmwareVersion.Text = frameContent.Substring(102, 5);
                                                        resFirmwareVersionDockPanel.Visibility = Visibility.Visible;
                                                    }
                                                    catch (Exception ex)
                                                    {
                                                        string str = ex.StackTrace;
                                                        Console.WriteLine(str);
                                                        // 异常时显示提示文字
                                                        statusTextBlock.Text = "固件版本解析出错！";
                                                        turnOnButton.IsChecked = false;
                                                        return;
                                                    }
                                                    // 软件版本
                                                    try
                                                    {
                                                        resSoftwareVersion.Text = frameContent.Substring(108, 5);
                                                        resSoftwareVersionDockPanel.Visibility = Visibility.Visible;
                                                    }
                                                    catch (Exception ex)
                                                    {
                                                        string str = ex.StackTrace;
                                                        Console.WriteLine(str);
                                                        // 异常时显示提示文字
                                                        statusTextBlock.Text = "软件版本解析出错！";
                                                        turnOnButton.IsChecked = false;
                                                        return;
                                                    }
                                                    // 量程下限
                                                    try
                                                    {
                                                        resLowRange.Text = frameContent.Substring(114, 5);
                                                        resLowRangeDockPanel.Visibility = Visibility.Visible;
                                                    }
                                                    catch (Exception ex)
                                                    {
                                                        string str = ex.StackTrace;
                                                        Console.WriteLine(str);
                                                        // 异常时显示提示文字
                                                        statusTextBlock.Text = "量程下限解析出错！";
                                                        turnOnButton.IsChecked = false;
                                                        return;
                                                    }
                                                    // 量程上限
                                                    try
                                                    {
                                                        resHighRange.Text = frameContent.Substring(120, 5);
                                                        resHighRangeDockPanel.Visibility = Visibility.Visible;
                                                    }
                                                    catch (Exception ex)
                                                    {
                                                        string str = ex.StackTrace;
                                                        Console.WriteLine(str);
                                                        // 异常时显示提示文字
                                                        statusTextBlock.Text = "量程上限解析出错！";
                                                        turnOnButton.IsChecked = false;
                                                        return;
                                                    }
                                                    // 测量精度
                                                    try
                                                    {
                                                        resMeasurementAccuracy.Text = frameContent.Substring(126, 5);
                                                        resMeasurementAccuracyDockPanel.Visibility = Visibility.Visible;
                                                    }
                                                    catch (Exception ex)
                                                    {
                                                        string str = ex.StackTrace;
                                                        Console.WriteLine(str);
                                                        // 异常时显示提示文字
                                                        statusTextBlock.Text = "测量精度解析出错！";
                                                        turnOnButton.IsChecked = false;
                                                        return;
                                                    }
                                                    // 防护等级
                                                    try
                                                    {
                                                        resProtectionLevel.Text = frameContent.Substring(132, 23);
                                                        resProtectionLevelDockPanel.Visibility = Visibility.Visible;
                                                    }
                                                    catch (Exception ex)
                                                    {
                                                        string str = ex.StackTrace;
                                                        Console.WriteLine(str);
                                                        // 异常时显示提示文字
                                                        statusTextBlock.Text = "防护等级解析出错！";
                                                        turnOnButton.IsChecked = false;
                                                        return;
                                                    }
                                                    // 防爆等级
                                                    try
                                                    {
                                                        resExplosionProofGrade.Text = frameContent.Substring(156, 35);
                                                        resExplosionProofGradeDockPanel.Visibility = Visibility.Visible;
                                                    }
                                                    catch (Exception ex)
                                                    {
                                                        string str = ex.StackTrace;
                                                        Console.WriteLine(str);
                                                        // 异常时显示提示文字
                                                        statusTextBlock.Text = "防爆等级解析出错！";
                                                        turnOnButton.IsChecked = false;
                                                        return;
                                                    }
                                                    // 说明
                                                    try
                                                    {
                                                        resIllustrate.Text = frameContent.Substring(191, 29);
                                                        resIllustrateDockPanel.Visibility = Visibility.Visible;
                                                    }
                                                    catch (Exception ex)
                                                    {
                                                        string str = ex.StackTrace;
                                                        Console.WriteLine(str);
                                                        // 异常时显示提示文字
                                                        statusTextBlock.Text = "说明解析出错！";
                                                        turnOnButton.IsChecked = false;
                                                        return;
                                                    }


                                                }
                                                break;
                                            case 0x0020: resFunctionData.Text = "读数据命令"; break;
                                            case 0x0100: resFunctionData.Text = "控制器参数写应答（控制器应答命令）"; break;
                                            case 0x0101: resFunctionData.Text = "控制器读仪表参数应答（控制器应答命令）"; break;
                                            case 0x0200: resFunctionData.Text = "控制器应答一体化载荷位移示功仪功图参数应答（控制器应答命令触发功图采集）"; break;
                                            case 0x0201: resFunctionData.Text = "控制器应答功图数据命令"; break;
                                            case 0x0202: resFunctionData.Text = "控制器读功图数据应答（控制器应答命令读已有功图）"; break;
                                            case 0x0300: resFunctionData.Text = "控制器(RTU)对仪表控制命令"; break;
                                            default: resType.Clear(); break;
                                        }
                                        while (resType.Text.Trim() == string.Empty)
                                        {

                                            if (intFrameContentFunctionData >= 0x400 && intFrameContentFunctionData <= 0x47f)
                                            {
                                                resFunctionData.Text = "配置协议命令";
                                            }
                                            else if (intFrameContentFunctionData >= 0x480 && intFrameContentFunctionData <= 0x5ff)
                                            {
                                                resFunctionData.Text = "标定协议命令";
                                            }
                                            else if (intFrameContentFunctionData >= 0x1000 && intFrameContentFunctionData <= 0x2000)
                                            {
                                                resFunctionData.Text = "厂家自定义数据类型";
                                            }
                                            else if (intFrameContentFunctionData >= 0x8000 && intFrameContentFunctionData <= 0xffff)
                                            {
                                                resFunctionData.Text = "预留";
                                            }
                                            else
                                            {
                                                resFunctionData.Text = "未定义";
                                                resFunctionData.Foreground = new SolidColorBrush(Colors.Red);
                                                break;
                                            }

                                        }
                                        resFunctionDataDockPanel.Visibility = Visibility.Visible;
                                    }
                                    catch (Exception ex)
                                    {
                                        string str = ex.StackTrace;
                                        Console.WriteLine(str);
                                        // 异常时显示提示文字
                                        statusTextBlock.Text = "数据类型解析出错！";
                                        turnOnButton.IsChecked = false;
                                        return;
                                    }
                                }

                            }
                            else
                            {
                                resCRC.Text = "未通过";
                                resCRC.Foreground = new SolidColorBrush(Colors.Red);
                            }
                            resCRCDockPanel.Visibility = Visibility.Visible;

                        }
                        break;
                    default:
                        resProtocol.Text = "未知";
                        resAddress.Foreground = new SolidColorBrush(Colors.Red);
                        break;
                }

            }
            catch (Exception ex)
            {
                string str = ex.StackTrace;
                Console.WriteLine(str);
                // 异常时显示提示文字
                statusTextBlock.Text = "参数解析出错！";
                turnOnButton.IsChecked = false;
            }
            // 更新数据曲线
            //string strConn = @"Data Source=.;Initial Catalog=Test; integrated security=True;";
            //SqlConnection conn = new SqlConnection(strConn);


        }

        private string CalCheckCode_7E(string receiveText)
        {
            int j = 0;
            string txt = receiveText.Trim();
            string[] hexvalue = txt.Remove(0, 9).Remove(txt.Length - 12).Split(' ');
            // 0xFF - 字符串求和
            try
            {
                foreach (string hex in hexvalue) j = j + Convert.ToInt32(hex, 16);
                string hexj = (0xFF - Convert.ToInt32(j.ToString("X").Substring(j.ToString("X").Length - 2, 2), 16)).ToString("X");
                return hexj;
            }
            catch (Exception ex)
            {
                string str = ex.StackTrace;
                Console.WriteLine(str);
                return "";
            }

        }

        private string CalCheckCode_FE(string receiveText)
        {
            string j = "";
            string txt = receiveText.Trim();
            string[] hexvalue = txt.Remove(0, 3).Remove(txt.Length - 6).Split(' ');
            // 求字符串异或值
            foreach (string hex in hexvalue) j = HexStrXor(j, hex);
            return j;
        }

        /// <summary>
        /// 接收窗口清空按钮
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ClearReceiveButton_Click(object sender, RoutedEventArgs e)
        {
            displayTextBox.Clear();
        }
        #endregion

        #region 串口数据发送/定时发送/窗口清空功能
        /// <summary>
        /// 在发送窗口中写入数据
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SendTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            // 将光标移至文字末尾
            sendTextBox.SelectionStart = sendTextBox.Text.Length;
            //MatchCollection hexadecimalCollection = Regex.Matches(e.Text, @"[(0(X|x))?\da-fA-F]");
            MatchCollection hexadecimalCollection = Regex.Matches(e.Text, @"[\da-fA-F]");
            foreach (Match mat in hexadecimalCollection)
            {
                sendTextBox.AppendText(mat.Value);
            }
            // 每输入两个字符自动添加空格
            sendTextBox.Text = sendTextBox.Text.Replace(" ", "");
            sendTextBox.Text = string.Join(" ", Regex.Split(sendTextBox.Text, "(?<=\\G.{2})(?!$)"));
            sendTextBox.SelectionStart = sendTextBox.Text.Length;
            e.Handled = true;
        }
        /// <summary>
        /// 串口数据发送逻辑
        /// </summary>
        private void SerialPortSend()
        {
            sendCount++;
            //Console.WriteLine("发送" + sendCount + "次");
            // 清空发送缓冲区
            try
            {
                serialPort.DiscardOutBuffer();

            }
            catch (Exception ex)
            {
                string std = ex.StackTrace;
                Console.WriteLine(std);
            }

            if (!serialPort.IsOpen)
            {
                statusTextBlock.Text = "请先打开串口！";
                return;
            }
            // 去掉十六进制前缀
            sendTextBox.Text.Replace("0x", "");
            sendTextBox.Text.Replace("0X", "");
            string sendData = sendTextBox.Text;

            // 十六进制数据发送
            try
            {
                // 分割字符串
                string[] strArray = sendData.Split(new char[] { ' ' });
                // 写入数据缓冲区
                byte[] sendBuffer = new byte[strArray.Length];
                int i = 0;
                foreach (string str in strArray)
                {
                    try
                    {
                        int j = Convert.ToInt16(str, 16);
                        sendBuffer[i] = Convert.ToByte(j);
                        i++;
                    }
                    catch (Exception ex)
                    {
                        string std = ex.StackTrace;
                        Console.WriteLine(std);
                        serialPort.DiscardOutBuffer();
                        MessageBox.Show("字节越界，请逐个字节输入！", "Error");
                        autoSendCheckBox.IsChecked = false;// 关闭自动发送
                    }
                }
                //foreach (byte b in sendBuffer)
                //{
                //    Console.Write(b.ToString("X2"));
                //}
                //Console.WriteLine("");
                try
                {
                    serialPort.Write(sendBuffer, 0, sendBuffer.Length);
                }
                catch (Exception ex)
                {
                    string str = ex.StackTrace;
                    Console.WriteLine(str);
                    statusTextBlock.Text = "串口异常";
                }
                // 更新发送数据计数
                sendBytesCount += (uint)sendBuffer.Length;
                statusSendByteTextBlock.Text = sendBytesCount.ToString();
            }
            catch (Exception ex)
            {
                string str = ex.StackTrace;
                Console.WriteLine(str);
                statusTextBlock.Text = "当前为16进制发送模式，请输入16进制数据";
                autoSendCheckBox.IsChecked = false;// 关闭自动发送
            }
        }

        private void SerialPortSend(string hexstr)
        {
            sendCount++;
            //Console.WriteLine("发送" + sendCount + "次");
            // 清空发送缓冲区
            serialPort.DiscardOutBuffer();
            if (!serialPort.IsOpen)
            {
                statusTextBlock.Text = "请先打开串口！";
                return;
            }
            // 去掉十六进制前缀
            hexstr.Replace("0x", "");
            hexstr.Replace("0X", "");
            string sendData = hexstr;

            // 十六进制数据发送
            try
            {
                // 分割字符串
                string[] strArray = sendData.Split(new char[] { ' ' });
                // 写入数据缓冲区
                byte[] sendBuffer = new byte[strArray.Length];
                int i = 0;
                foreach (string str in strArray)
                {
                    try
                    {
                        int j = Convert.ToInt16(str, 16);
                        sendBuffer[i] = Convert.ToByte(j);
                        i++;
                    }
                    catch (Exception ex)
                    {
                        string std = ex.StackTrace;
                        Console.WriteLine(std);
                        serialPort.DiscardOutBuffer();
                        MessageBox.Show("字节越界，请逐个字节输入！", "Error");
                        autoSendCheckBox.IsChecked = false;// 关闭自动发送
                    }
                }
                //foreach (byte b in sendBuffer)
                //{
                //    Console.Write(b.ToString("X2"));
                //}
                //Console.WriteLine("");
                try
                {
                    serialPort.Write(sendBuffer, 0, sendBuffer.Length);
                }
                catch (Exception ex)
                {
                    string str = ex.StackTrace;
                    Console.WriteLine(str);
                    statusTextBlock.Text = "串口异常";
                }
                // 更新发送数据计数
                sendBytesCount += (uint)sendBuffer.Length;
                statusSendByteTextBlock.Text = sendBytesCount.ToString();
            }
            catch (Exception ex)
            {
                string str = ex.StackTrace;
                Console.WriteLine(str);
                statusTextBlock.Text = "当前为16进制发送模式，请输入16进制数据";
                autoSendCheckBox.IsChecked = false;// 关闭自动发送
            }
        }
        /// <summary>
        /// 手动单击按钮发送
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SendButton_Click(object sender, RoutedEventArgs e)
        {
            SerialPortSend();
        }
        /// <summary>
        /// 自动发送开启
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void AutoSendCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            autoSendTimer.Start();
        }
        /// <summary>
        /// 在每个自动发送周期执行
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void AutoSendTimer_Tick(object sender, EventArgs e)
        {
            autoSendTimer.Interval = new TimeSpan(0, 0, 0, 0, Convert.ToInt32(autoSendCycleTextBox.Text));
            // 发送数据
            SerialPortSend();
            // 设置新的定时时间           
            // autoSendTimer.Interval = new TimeSpan(0, 0, 0, 0, Convert.ToInt32(autoSendCycleTextBox.Text));
        }
        /// <summary>
        /// 自动发送关闭
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void AutoSendCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            autoSendTimer.Stop();
        }
        /// <summary>
        /// 清空发送区
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ClearSendButton_Click(object sender, RoutedEventArgs e)
        {
            sendTextBox.Clear();
        }
        /// <summary>
        /// 清空计数器
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CountClearButton_Click(object sender, RoutedEventArgs e)
        {
            // 接收、发送计数清零
            receiveBytesCount = 0;
            sendBytesCount = 0;
            // 更新数据显示
            statusReceiveByteTextBlock.Text = receiveBytesCount.ToString();
            statusSendByteTextBlock.Text = sendBytesCount.ToString();
        }
        #endregion

        #region 文件读取与保存 (文件I/O)
        /// <summary>
        /// 读取文件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void FileOpen(object sender, ExecutedRoutedEventArgs e)
        {
            // 打开文件对话框 (默认选择serialCom.txt, 默认格式为文本文档)
            OpenFileDialog openFile = new OpenFileDialog
            {
                FileName = "serialCom",
                DefaultExt = ".txt",
                Filter = "文本文档|*.txt"
            };
            // 如果用户单击确定(选好了文本文档文件)
            if (openFile.ShowDialog() == true)
            {
                // 将文本文档中所有文字读取到发送区
                sendTextBox.Text = File.ReadAllText(openFile.FileName, setEncoding);
                // 将文本文档的文件名读取到串口发送面板的文本框中
                fileNameTextBox.Text = openFile.FileName;
            }
        }
        /// <summary>
        /// 读取接收区并保存文件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void FileSave(object sender, ExecutedRoutedEventArgs e)
        {
            // 判断接收区是否有字段
            if (displayTextBox.Text == string.Empty)
            {
                // 如果没有字段，弹出失败提示
                statusTextBlock.Text = "接收区为空，保存失败。";
            }
            else
            {
                SaveFileDialog saveFile = new SaveFileDialog
                {
                    DefaultExt = ".txt",
                    Filter = "文本文档|*.txt"
                };
                // 如果用户单击确定(确定了文本文档保存的位置和名称)
                if (saveFile.ShowDialog() == true)
                {
                    // 在文本文档中写入当前时间
                    File.AppendAllText(saveFile.FileName, "\r\n******" + DateTime.Now.ToString() + "\r\n******");
                    // 将接收区所有字段写入到文本文档
                    File.AppendAllText(saveFile.FileName, displayTextBox.Text);
                    // 弹出成功提示
                    statusTextBlock.Text = "保存成功！";
                }
            }
        }
        #endregion

        #region 上位机响应仪表方法
        /// <summary>
        /// 常规数据确认帧
        /// </summary>
        /// <returns></returns>
        private string RegularDataConfirmationFrame()
        {
            try
            {
                string str = "";
                switch (frameHeader)
                {
                    case "FE":
                        {
                            // 获取所需解析数据
                            ParameterAcquisition_FE(out string strHeader, out string strCommand, out string strAddress, out string strProtocolVendor, out string strHandler, out string strGroup);
                            // 功能码 / 数据类型
                            string strFunctionData = "01 00";
                            // 写操作数据区
                            string strHandlerContent;
                            if (regularDataUpdateRate.Text != "")
                            {
                                strHandlerContent = Convert.ToString(Convert.ToInt32(regularDataUpdateRate.Text), 16).ToUpper().PadLeft(4, '0').Insert(2, " ");
                            }
                            else if (resSleepTime.Text != "")
                            {
                                strHandlerContent = frameContent.Substring(36, 5);
                            }
                            else
                            {
                                strHandlerContent = "00 00";
                            }
                            // 合成数据域
                            string strContent = strAddress + " " + strProtocolVendor + " " + strHandler + " " + strGroup + " " + strFunctionData + " " + strHandlerContent;
                            // 计算长度域（不包含命令域）
                            int intLength = (strContent.Length + 1) / 3;
                            string strLength = Convert.ToString(intLength, 16).ToUpper().PadLeft(2, '0');
                            string strInner = strLength + " " + strCommand + " " + strContent;
                            // 计算异或校验码
                            string strCRC = CalCheckCode_FE("00 " + strInner + " 00");
                            // 合成返回值
                            str = strHeader + " " + strInner + " " + strCRC;
                        }
                        break;
                    case "7E":
                        {
                            if (LoRaFlag)
                            {
                                // 获取所需解析数据
                                ParameterAcquisition_7E(out string strHeader, out string strCommand, out string strAddress, out string strProtocolVendor, out string strHandler, out string strGroup);
                                // 功能码 / 数据类型
                                string strFunctionData = "01 00";
                                // 写操作数据区
                                string strHandlerContent;
                                if (regularDataUpdateRate.Text != "")
                                {
                                    strHandlerContent = Convert.ToString(Convert.ToInt32(regularDataUpdateRate.Text), 16).ToUpper().PadLeft(4, '0').Insert(2, " ");
                                }
                                else if (resSleepTime.Text != "")
                                {
                                    strHandlerContent = frameContent.Substring(36, 5);
                                }
                                else
                                {
                                    strHandlerContent = "00 00";
                                }
                                // 合成数据域
                                string strContent = strProtocolVendor + " " + strHandler + " " + strGroup + " " + strFunctionData + " " + strHandlerContent;
                                // 计算长度域（包含命令域）
                                int intLength = (strContent.Length + 1) / 3;
                                string strLength = Convert.ToString(intLength, 16).ToUpper().PadLeft(4, '0').Insert(2, " ");
                                string strInner = strLength + " " + strContent;
                                // 计算异或校验码
                                string strCRC = CalCheckCode_7E("00 " + strInner + " 00");
                                // 合成返回值
                                str = strHeader + " " + strInner + " " + strCRC;
                            }
                            else
                            {
                                // 获取所需解析数据
                                ParameterAcquisition_7E(out string strHeader, out string strCommand, out string strAddress, out string strProtocolVendor, out string strHandler, out string strGroup);
                                // 功能码 / 数据类型
                                string strFunctionData = "01 00";
                                // 写操作数据区
                                string strHandlerContent;
                                if (regularDataUpdateRate.Text != "")
                                {
                                    strHandlerContent = Convert.ToString(Convert.ToInt32(regularDataUpdateRate.Text), 16).ToUpper().PadLeft(4, '0').Insert(2, " ");
                                }
                                else if (resSleepTime.Text != "")
                                {
                                    strHandlerContent = frameContent.Substring(36, 5);
                                }
                                else
                                {
                                    strHandlerContent = "00 00";
                                }
                                // 合成数据域
                                string strContent = strCommand + " " + strAddress + " " + frameUnparsed.Remove(frameUnparsed.Length - 3, 3) + " 00 00 " + strProtocolVendor + " " + strHandler + " " + strGroup + " " + strFunctionData + " " + strHandlerContent;
                                // 计算长度域（包含命令域）
                                int intLength = (strContent.Length + 1) / 3;
                                string strLength = Convert.ToString(intLength, 16).ToUpper().PadLeft(4, '0').Insert(2, " ");
                                string strInner = strLength + " " + strContent;
                                // 计算异或校验码
                                string strCRC = CalCheckCode_7E("00 " + strInner + " 00");
                                // 合成返回值
                                str = strHeader + " " + strInner + " " + strCRC;
                            }
                        }
                        break;
                    default:
                        break;
                }

                return str;
            }
            catch (Exception ex)
            {
                string str = ex.StackTrace;
                Console.WriteLine(str);
                return "";
            }

        }
        /// <summary>
        /// 基本信息确认帧
        /// </summary>
        /// <returns></returns>
        private string BasicInformationConfirmationFrame()
        {
            try
            {
                string str = "";
                switch (frameHeader)
                {
                    case "FE":
                        {
                            // 获取所需解析数据
                            ParameterAcquisition_FE(out string strHeader, out string strCommand, out string strAddress, out string strProtocolVendor, out string strHandler, out string strGroup);
                            // 写操作数据区
                            // 功能码 / 数据类型
                            string strFunctionData = "01 01";
                            string strHandlerContent = "";
                            // 合成数据域
                            string strContent = strAddress + " " + strProtocolVendor + " " + strHandler + " " + strGroup + " " + strFunctionData + " " + strHandlerContent;
                            // 计算长度域（不包含命令域）
                            int intLength = (strContent.Length + 1) / 3;
                            string strLength = Convert.ToString(intLength, 16).ToUpper().PadLeft(2, '0');
                            string strInner = strLength + " " + strCommand + " " + strContent;
                            // 计算异或校验码
                            string strCRC = CalCheckCode_FE("00 " + strInner + " 00");
                            // 合成返回值
                            str = strHeader + " " + strInner + " " + strCRC;
                        }
                        break;
                    case "7E":
                        {
                            if (LoRaFlag)
                            {
                                // 获取所需解析数据
                                ParameterAcquisition_7E(out string strHeader, out string strCommand, out string strAddress, out string strProtocolVendor, out string strHandler, out string strGroup);
                                // 写操作数据区
                                // 功能码 / 数据类型
                                string strFunctionData = "01 01";
                                string strHandlerContent = "";
                                // 合成数据域
                                string strContent = strProtocolVendor + " " + strHandler + " " + strGroup + " " + strFunctionData + " " + strHandlerContent;
                                // 计算长度域（包含命令域）
                                int intLength = (strContent.Length + 1) / 3;
                                string strLength = Convert.ToString(intLength, 16).ToUpper().PadLeft(4, '0').Insert(2, " ");
                                string strInner = strLength + " " + strContent;
                                // 计算异或校验码
                                string strCRC = CalCheckCode_7E("00 " + strInner + " 00");
                                // 合成返回值
                                str = strHeader + " " + strInner + " " + strCRC;
                            }
                            else
                            {
                                // 获取所需解析数据
                                ParameterAcquisition_7E(out string strHeader, out string strCommand, out string strAddress, out string strProtocolVendor, out string strHandler, out string strGroup);
                                // 写操作数据区
                                // 功能码 / 数据类型
                                string strFunctionData = "01 01";
                                string strHandlerContent = "";
                                // 合成数据域
                                string strContent = strCommand + " " + strAddress + " " + frameUnparsed.Remove(frameUnparsed.Length - 3, 3) + " 00 00 " + strProtocolVendor + " " + strHandler + " " + strGroup + " " + strFunctionData + " " + strHandlerContent;
                                // 计算长度域（包含命令域）
                                int intLength = (strContent.Length + 1) / 3;
                                string strLength = Convert.ToString(intLength, 16).ToUpper().PadLeft(4, '0').Insert(2, " ");
                                string strInner = strLength + " " + strContent;
                                // 计算异或校验码
                                string strCRC = CalCheckCode_7E("00 " + strInner + " 00");
                                // 合成返回值
                                str = strHeader + " " + strInner + " " + strCRC;
                            }

                        }
                        break;
                    default:
                        break;
                }

                return str;
            }

            catch (Exception ex)
            {
                string str = ex.StackTrace;
                Console.WriteLine(str);
                return "";
            }
        }
        /// <summary>
        /// 建立连接处理程序
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void EstablishConnectionButton_Checked(object sender, RoutedEventArgs e)
        {
            // 判断仪表参数是否解析完成
            if (resCRC.Text == "通过")
            {
                try
                {
                    // 发送下行报文建立连接 
                    //// 生成16进制字符串
                    sendTextBox.Text = EstablishBuild_Text();
                    //// 标定连接发送（已替换为通过connectFlag自动发送）
                    //SerialPortSend(sendTextBox.Text);
                    //serialPort.Write(EstablishBuild_Text());
                    // 指示灯变绿
                    if (true)
                    {
                        connectionStatusEllipse.Fill = Brushes.Green;
                        connectFlag = true;
                    }
                    establishConnectionButton.Content = "关闭连接";
                    establishConnectionButton.IsEnabled = false;
                    // 更新率锁定
                    regularDataUpdateRate.IsEnabled = false;
                }
                catch (Exception ex)
                {
                    string str = ex.StackTrace;
                    Console.WriteLine(str);
                    // 异常时显示提示文字
                    statusTextBlock.Text = "建立连接出错！";
                    // 指示灯变灰
                    establishConnectionButton.IsEnabled = true;
                    establishConnectionButton.Content = "建立连接";
                    connectFlag = false;
                    connectionStatusEllipse.Fill = Brushes.Gray;
                }
            }
            else statusTextBlock.Text = "请先解析仪表参数！";
        }
        /// <summary>
        /// 建立连接帧
        /// </summary>
        /// <returns></returns>
        private string EstablishBuild_Text()
        {
            string str = "";
            switch (frameHeader)
            {
                case "FE":
                    {
                        // 获取所需解析数据
                        ParameterAcquisition_FE(out string strHeader, out string strCommand, out string strAddress, out string strProtocolVendor, out string strHandler, out string strGroup);
                        // 写操作数据区
                        // 功能码 / 数据类型
                        string strFunctionData = "00 80";
                        string strHandlerContent = "F0";
                        // 合成数据域
                        string strContent = strAddress + " " + strProtocolVendor + " " + strHandler + " " + strGroup + " " + strFunctionData + " " + strHandlerContent;
                        // 计算长度域（不包含命令域）
                        int intLength = (strContent.Length + 1) / 3;
                        string strLength = Convert.ToString(intLength, 16).ToUpper().PadLeft(2, '0');
                        string strInner = strLength + " " + strCommand + " " + strContent;
                        // 计算异或校验码
                        string strCRC = CalCheckCode_FE("00 " + strInner + " 00");
                        // 合成返回值
                        str = strHeader + " " + strInner + " " + strCRC;
                    }
                    break;
                case "7E":
                    {
                        if (LoRaFlag)
                        {
                            // 获取所需解析数据
                            ParameterAcquisition_7E(out string strHeader, out string strCommand, out string strAddress, out string strProtocolVendor, out string strHandler, out string strGroup);
                            // 写操作数据区
                            // 功能码 / 数据类型
                            string strFunctionData = "00 80";
                            string strHandlerContent = "F0";
                            // 合成数据域
                            string strContent = strProtocolVendor + " " + strHandler + " " + strGroup + " " + strFunctionData + " " + strHandlerContent;
                            // 计算长度域（包含命令域）
                            int intLength = (strContent.Length + 1) / 3;
                            string strLength = Convert.ToString(intLength, 16).ToUpper().PadLeft(4, '0').Insert(2, " ");
                            string strInner = strLength + " " + strContent;
                            // 计算异或校验码
                            string strCRC = CalCheckCode_7E("00 " + strInner + " 00");
                            // 合成返回值
                            str = strHeader + " " + strInner + " " + strCRC;
                        }
                        else
                        {
                            // 获取所需解析数据
                            ParameterAcquisition_7E(out string strHeader, out string strCommand, out string strAddress, out string strProtocolVendor, out string strHandler, out string strGroup);
                            // 写操作数据区
                            // 功能码 / 数据类型
                            string strFunctionData = "00 80";
                            string strHandlerContent = "F0";
                            // 合成数据域
                            string strContent = strCommand + " " + strAddress + " " + frameUnparsed.Remove(frameUnparsed.Length - 3, 3) + " 00 00 " + strProtocolVendor + " " + strHandler + " " + strGroup + " " + strFunctionData + " " + strHandlerContent;
                            // 计算长度域（包含命令域）
                            int intLength = (strContent.Length + 1) / 3;
                            string strLength = Convert.ToString(intLength, 16).ToUpper().PadLeft(4, '0').Insert(2, " ");
                            string strInner = strLength + " " + strContent;
                            // 计算异或校验码
                            string strCRC = CalCheckCode_7E("00 " + strInner + " 00");
                            // 合成返回值
                            str = strHeader + " " + strInner + " " + strCRC;
                        }
                    }
                    break;
                default:
                    break;
            }
            return str;

        }
        /// <summary>
        /// 断开连接处理程序
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void EstablishConnectionButton_Unchecked(object sender, RoutedEventArgs e)
        {
            // 判断仪表参数是否解析完成
            if (resCRC.Text == "通过")
            {
                try
                {
                    // 发送下行报文建立连接 
                    //// 生成16进制字符串
                    sendTextBox.Text = EstablishDisconnect_Text();
                    //// 标定连接发送（已替换为通过connectFlag自动发送）
                    //SerialPortSend(sendTextBox.Text);
                    serialPort.Write(EstablishBuild_Text());
                    // 指示灯变灰
                    establishConnectionButton.Content = "建立连接";
                    connectFlag = false;
                    connectionStatusEllipse.Fill = Brushes.Gray;
                    // 更新率不锁定
                    regularDataUpdateRate.IsEnabled = true;
                }
                catch (Exception ex)
                {
                    string str = ex.StackTrace;
                    Console.WriteLine(str);
                    // 异常时显示提示文字
                    statusTextBlock.Text = "断开连接出错！";
                    // 指示灯变灰
                    establishConnectionButton.Content = "关闭连接";
                    establishConnectionButton.IsEnabled = false;
                    connectionStatusEllipse.Fill = Brushes.Green;
                }
            }
            else statusTextBlock.Text = "请先解析仪表参数！";
        }
        /// <summary>
        /// 断开连接帧
        /// </summary>
        /// <returns></returns>
        private string EstablishDisconnect_Text()
        {
            string str = "";
            switch (frameHeader)
            {
                case "FE":
                    {
                        // 获取所需解析数据
                        ParameterAcquisition_FE(out string strHeader, out string strCommand, out string strAddress, out string strProtocolVendor, out string strHandler, out string strGroup);
                        // 写操作数据区
                        // 功能码 / 数据类型
                        string strFunctionData = "00 80";
                        string strHandlerContent = "FF";
                        // 合成数据域
                        string strContent = strAddress + " " + strProtocolVendor + " " + strHandler + " " + strGroup + " " + strFunctionData + " " + strHandlerContent;
                        // 计算长度域（不包含命令域）
                        int intLength = (strContent.Length + 1) / 3;
                        string strLength = Convert.ToString(intLength, 16).ToUpper().PadLeft(2, '0');
                        string strInner = strLength + " " + strCommand + " " + strContent;
                        // 计算异或校验码
                        string strCRC = CalCheckCode_FE("00 " + strInner + " 00");
                        // 合成返回值
                        str = strHeader + " " + strInner + " " + strCRC;
                    }
                    break;
                case "7E":
                    {
                        if (LoRaFlag)
                        {
                            // 获取所需解析数据
                            ParameterAcquisition_7E(out string strHeader, out string strCommand, out string strAddress, out string strProtocolVendor, out string strHandler, out string strGroup);
                            // 写操作数据区
                            // 功能码 / 数据类型
                            string strFunctionData = "00 80";
                            string strHandlerContent = "FF";
                            // 合成数据域
                            string strContent = strProtocolVendor + " " + strHandler + " " + strGroup + " " + strFunctionData + " " + strHandlerContent;
                            // 计算长度域（包含命令域）
                            int intLength = (strContent.Length + 1) / 3;
                            string strLength = Convert.ToString(intLength, 16).ToUpper().PadLeft(4, '0').Insert(2, " ");
                            string strInner = strLength + " " + strContent;
                            // 计算异或校验码
                            string strCRC = CalCheckCode_7E("00 " + strInner + " 00");
                            // 合成返回值
                            str = strHeader + " " + strInner + " " + strCRC;
                        }
                        else
                        {
                            // 获取所需解析数据
                            ParameterAcquisition_7E(out string strHeader, out string strCommand, out string strAddress, out string strProtocolVendor, out string strHandler, out string strGroup);
                            // 写操作数据区
                            // 功能码 / 数据类型
                            string strFunctionData = "00 80";
                            string strHandlerContent = "FF";
                            // 合成数据域
                            string strContent = strCommand + " " + strAddress + " " + frameUnparsed.Remove(frameUnparsed.Length - 3, 3) + " 00 00 " + strProtocolVendor + " " + strHandler + " " + strGroup + " " + strFunctionData + " " + strHandlerContent;
                            // 计算长度域（包含命令域）
                            int intLength = (strContent.Length + 1) / 3;
                            string strLength = Convert.ToString(intLength, 16).ToUpper().PadLeft(4, '0').Insert(2, " ");
                            string strInner = strLength + " " + strContent;
                            // 计算异或校验码
                            string strCRC = CalCheckCode_7E("00 " + strInner + " 00");
                            // 合成返回值
                            str = strHeader + " " + strInner + " " + strCRC;
                        }

                    }
                    break;
                default:
                    break;
            }
            return str;

        }
        /// <summary>
        /// 描述标定处理程序
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void DescriptionCalibrationButton_Click(object sender, RoutedEventArgs e)
        {
            // 判断仪表参数是否解析完成和是否处于连接状态
            if (resCRC.Text == "通过" && connectionStatusEllipse.Fill == Brushes.Green)
            {
                try
                {
                    // 发送下行报文建立连接 
                    // 生成16进制字符串
                    sendTextBox.Text = DescribeCalibration_Text();
                    // 标定连接发送
                    // SerialPortSend();
                    //if (true)
                    //{
                    //    establishConnectionButton.IsChecked = false;
                    //}
                }
                catch (Exception ex)
                {
                    string str = ex.StackTrace;
                    Console.WriteLine(str);
                    // 异常时显示提示文字
                    statusTextBlock.Text = "描述标定出错！";
                }
            }
            else statusTextBlock.Text = "请先建立连接！";
        }
        /// <summary>
        /// 生成描述标定数据
        /// </summary>
        /// <returns></returns>
        private string DescribeCalibration_Text()
        {
            string str = "";
            switch (frameHeader)
            {
                case "FE":
                    {
                        // 获取所需解析数据
                        ParameterAcquisition_FE(out string strHeader, out string strCommand, out string strAddress, out string strProtocolVendor, out string strHandler, out string strGroup);
                        // 获取设备描述标定信息
                        // 功能码 / 数据类型
                        string strFunctionData = "00 80";
                        // 写操作数据区
                        string strHandlerContent = "F1 " + calibrationInstrumentModelTextBox.Text.Trim() + " " + calibrationSerialNumberTextBox.Text.Trim() + " " + calibrationIPRatingTextBox.Text.Trim() + " " + calibrationExplosionProofLevelTextBox.Text.Trim() + " " + calibrationInstructionsTextBox.Text.Trim();
                        // 合成数据域
                        string strContent = strAddress + " " + strProtocolVendor + " " + strHandler + " " + strGroup + " " + strFunctionData + " " + strHandlerContent;
                        // 计算长度域
                        int intLength = (strContent.Length + 1) / 3;
                        string strLength = Convert.ToString(intLength, 16).ToUpper().PadLeft(2, '0');
                        string strInner = strLength + " " + strCommand + " " + strContent;
                        // 计算异或校验码
                        string strCRC = HexCRC(strInner);
                        // 合成返回值
                        str = strHeader + " " + strInner + " " + strCRC;
                    }
                    break;
                case "7E":
                    {
                        if (LoRaFlag)
                        {
                            // 获取所需解析数据
                            ParameterAcquisition_7E(out string strHeader, out string strCommand, out string strAddress, out string strProtocolVendor, out string strHandler, out string strGroup);
                            // 获取设备描述标定信息
                            // 功能码 / 数据类型
                            string strFunctionData = "00 80";
                            // 写操作数据区
                            string strHandlerContent = "F1 " + calibrationInstrumentModelTextBox.Text.Trim() + " " + calibrationSerialNumberTextBox.Text.Trim() + " " + calibrationIPRatingTextBox.Text.Trim() + " " + calibrationExplosionProofLevelTextBox.Text.Trim() + " " + calibrationInstructionsTextBox.Text.Trim();
                            // 合成数据域
                            string strContent = strProtocolVendor + " " + strHandler + " " + strGroup + " " + strFunctionData + " " + strHandlerContent;
                            // 计算长度域（包含命令域）
                            int intLength = (strContent.Length + 1) / 3;
                            string strLength = Convert.ToString(intLength, 16).ToUpper().PadLeft(4, '0').Insert(2, " ");
                            string strInner = strLength + " " + strContent;
                            // 计算异或校验码
                            string strCRC = CalCheckCode_7E("00 " + strInner + " 00");
                            // 合成返回值
                            str = strHeader + " " + strInner + " " + strCRC;
                        }
                        else
                        {
                            // 获取所需解析数据
                            ParameterAcquisition_7E(out string strHeader, out string strCommand, out string strAddress, out string strProtocolVendor, out string strHandler, out string strGroup);
                            // 获取设备描述标定信息
                            // 功能码 / 数据类型
                            string strFunctionData = "00 80";
                            // 写操作数据区
                            string strHandlerContent = "F1 " + calibrationInstrumentModelTextBox.Text.Trim() + " " + calibrationSerialNumberTextBox.Text.Trim() + " " + calibrationIPRatingTextBox.Text.Trim() + " " + calibrationExplosionProofLevelTextBox.Text.Trim() + " " + calibrationInstructionsTextBox.Text.Trim();
                            // 合成数据域
                            string strContent = strCommand + " " + strAddress + " " + frameUnparsed.Remove(frameUnparsed.Length - 3, 3) + " 00 00 " + strProtocolVendor + " " + strHandler + " " + strGroup + " " + strFunctionData + " " + strHandlerContent;
                            // 计算长度域
                            int intLength = (strContent.Length + 1) / 3;
                            string strLength = Convert.ToString(intLength, 16).ToUpper().PadLeft(4, '0').Insert(2, " ");
                            string strInner = strLength + " " + strContent;
                            // 计算异或校验码
                            string strCRC = CalCheckCode_7E("00 " + strInner + " 00");
                            // 合成返回值
                            str = strHeader + " " + strInner + " " + strCRC;
                        }

                    }
                    break;
                default:
                    break;
            }

            return str;
        }
        /// <summary>
        /// 参数标定处理程序
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ParameterCalibrationButton_Click(object sender, RoutedEventArgs e)
        {
            // 判断仪表参数是否解析完成和是否处于连接状态
            if (resCRC.Text == "通过" && connectionStatusEllipse.Fill == Brushes.Green)
            {
                try
                {
                    // 发送下行报文建立连接 
                    // 生成16进制字符串
                    sendTextBox.Text = ParameterCalibration_Text();
                    // 标定连接发送
                    // SerialPortSend();
                }
                catch (Exception ex)
                {
                    string str = ex.StackTrace;
                    Console.WriteLine(str);
                    // 异常时显示提示文字
                    statusTextBlock.Text = "描述标定出错！";
                }
            }
            else statusTextBlock.Text = "请先建立连接！";
        }
        /// <summary>
        /// 生成参数标定数据
        /// </summary>
        /// <returns></returns>
        private string ParameterCalibration_Text()
        {
            string str = "";
            switch (frameHeader)
            {
                case "FE":
                    {
                        // 获取所需解析数据
                        ParameterAcquisition_FE(out string strHeader, out string strCommand, out string strAddress, out string strProtocolVendor, out string strHandler, out string strGroup);
                        // 获取设备描述标定信息
                        // 功能码 / 数据类型
                        string strFunctionData = "00 80";
                        // 标定数据
                        string calibrationParameters = FloatStrToHexStr(calibrationParametersContentTextBox.Text);
                        // 写操作数据区
                        string strHandlerContent = "F2 " + calibrationParametersComboBox.Text.Substring(2, 2).Trim() + " " + calibrationUnitTextBox.Text.Trim() + " " + calibrationParameters;
                        // 合成数据域
                        string strContent = strAddress + " " + strProtocolVendor + " " + strHandler + " " + strGroup + " " + strFunctionData + " " + strHandlerContent;
                        // 计算长度域
                        int intLength = (strContent.Length + 1) / 3;
                        string strLength = Convert.ToString(intLength, 16).ToUpper().PadLeft(2, '0');
                        string strInner = strLength + " " + strCommand + " " + strContent;
                        // 计算异或校验码
                        string strCRC = HexCRC(strInner);
                        // 合成返回值
                        str = strHeader + " " + strInner + " " + strCRC;
                    }
                    break;
                case "7E":
                    {
                        if (LoRaFlag)
                        {
                            // 获取所需解析数据
                            ParameterAcquisition_7E(out string strHeader, out string strCommand, out string strAddress, out string strProtocolVendor, out string strHandler, out string strGroup);
                            // 获取设备描述标定信息
                            // 功能码 / 数据类型
                            string strFunctionData = "00 80";
                            // 标定数据
                            string calibrationParameters = FloatStrToHexStr(calibrationParametersContentTextBox.Text);
                            // 写操作数据区
                            string strHandlerContent = "F2 " + calibrationParametersComboBox.Text.Substring(2, 2).Trim() + " " + calibrationUnitTextBox.Text.Trim() + " " + calibrationParameters;
                            // 合成数据域
                            string strContent = strProtocolVendor + " " + strHandler + " " + strGroup + " " + strFunctionData + " " + strHandlerContent;
                            // 计算长度域（包含命令域）
                            int intLength = (strContent.Length + 1) / 3;
                            string strLength = Convert.ToString(intLength, 16).ToUpper().PadLeft(4, '0').Insert(2, " ");
                            string strInner = strLength + " " + strContent;
                            // 计算异或校验码
                            string strCRC = CalCheckCode_7E("00 " + strInner + " 00");
                            // 合成返回值
                            str = strHeader + " " + strInner + " " + strCRC;
                        }
                        else
                        {
                            // 获取所需解析数据
                            ParameterAcquisition_7E(out string strHeader, out string strCommand, out string strAddress, out string strProtocolVendor, out string strHandler, out string strGroup);
                            // 获取设备描述标定信息
                            // 功能码 / 数据类型
                            string strFunctionData = "00 80";
                            // 标定数据
                            string calibrationParameters = FloatStrToHexStr(calibrationParametersContentTextBox.Text);
                            // 写操作数据区
                            string strHandlerContent = "F2 " + calibrationParametersComboBox.Text.Substring(2, 2).Trim() + " " + calibrationUnitTextBox.Text.Trim() + " " + calibrationParameters;
                            // 合成数据域
                            string strContent = strCommand + " " + strAddress + " " + frameUnparsed.Remove(frameUnparsed.Length - 3, 3) + " 00 00 " + strProtocolVendor + " " + strHandler + " " + strGroup + " " + strFunctionData + " " + strHandlerContent;
                            // 计算长度域
                            int intLength = (strContent.Length + 1) / 3;
                            string strLength = Convert.ToString(intLength, 16).ToUpper().PadLeft(4, '0').Insert(2, " ");
                            string strInner = strLength + " " + strContent;
                            // 计算异或校验码
                            string strCRC = CalCheckCode_7E("00 " + strInner + " 00");
                            // 合成返回值
                            str = strHeader + " " + strInner + " " + strCRC;
                        }

                    }
                    break;
                default:
                    break;
            }

            return str;
        }


        /// <summary>
        /// FE标定公用数据
        /// </summary>
        /// <param name="strHeader"></param>
        /// <param name="strCommand"></param>
        /// <param name="strAddress"></param>
        /// <param name="strProtocolVendor"></param>
        /// <param name="strHandler"></param>
        /// <param name="strGroup"></param>
        /// <param name="strFunctionData"></param>        
        private void ParameterAcquisition_FE(out string strHeader, out string strCommand, out string strAddress, out string strProtocolVendor, out string strHandler, out string strGroup)
        {
            // 获取所需解析数据
            // 帧头
            strHeader = "FE";
            // 发送命令域
            strCommand = "24 5F";
            // 发送地址
            strAddress = frameAddress;
            // 协议和厂商号为数据内容前四位
            strProtocolVendor = frameContent.Substring(0, 11);
            // 仪表类型：手操器
            strHandler = "1F 10";
            // 组号表号
            strGroup = frameContent.Substring(18, 5);
        }
        /// <summary>
        /// 7E标定公用数据
        /// </summary>
        /// <param name="strHeader"></param>
        /// <param name="strCommand"></param>
        /// <param name="strAddress"></param>
        /// <param name="strProtocolVendor"></param>
        /// <param name="strHandler"></param>
        /// <param name="strGroup"></param>
        /// <param name="strFunctionData"></param>
        private void ParameterAcquisition_7E(out string strHeader, out string strCommand, out string strAddress, out string strProtocolVendor, out string strHandler, out string strGroup)
        {
            // 帧头
            strHeader = "7E";
            if (LoRaFlag)
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
                strAddress = frameAddress;
            }
            // 协议和厂商号
            strProtocolVendor = frameContent.Substring(0, 11);
            // 仪表类型：手操器
            strHandler = "1F 10";
            // 组号表号
            strGroup = frameContent.Substring(18, 5);
        }

        #endregion

        #region 其他方法
        /// <summary>
        /// 对十六进制报文字符串进行分割
        /// </summary>
        /// <param name="str"></param>
        /// <returns>第0位为头字节，第1至length - 2位为可用字节，第length - 1位为尾字节</returns>
        private string[] MessageSegmentationExtraction(string str)
        {
            string[] sepStr = str.Split(' ');
            string handleStr = "";
            string[] sepHandleStr;
            string tmpstr = "";
            int mantissaStartFrame = 0;
            // 定义输出格式
            int outPutStrTag = 0;
            string[] outPutStr = new string[1000];
            // 寻找可用报文
            for (int i = 0; i < sepStr.Length; i++)
            {
                // 先判断帧头
                switch (sepStr[i])
                {
                    case "FE":
                        handleStr = str.Substring(i * 3, str.Length - i * 3);
                        sepHandleStr = handleStr.Split(' ');
                        // 通过长度域判断是否为完整报文,
                        if (sepHandleStr.Length >= 5 && sepHandleStr.Length >= Convert.ToInt32(sepHandleStr[1], 16) + 5)
                        {
                            string[] useFrame = new string[Convert.ToInt32(sepHandleStr[1], 16) + 5];
                            Array.Copy(sepHandleStr, useFrame, Convert.ToInt32(sepHandleStr[1], 16) + 5);
                            string useFrameStr = string.Join(" ", useFrame);
                            // 提取头帧（如果有）
                            if (outPutStrTag == 0 && i != 0)
                            {
                                string[] headFrame = new string[i];
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
                        if (sepHandleStr.Length >= 5)
                        {
                            tmpstr = sepHandleStr[1] + sepHandleStr[2];
                        }
                        if (sepHandleStr.Length >= 5 && sepHandleStr.Length >= Convert.ToInt32(sepHandleStr[1] + sepHandleStr[2], 16) + 4)
                        {
                            string[] a = new string[Convert.ToInt32(tmpstr, 16) + 4];
                            Array.Copy(sepHandleStr, a, Convert.ToInt32(tmpstr, 16) + 4);
                            string useFrameStr = string.Join(" ", a);
                            // 提取头帧（如果有）
                            if (outPutStrTag == 0 && i != 0)
                            {
                                string[] headFrame = new string[i];
                                Array.Copy(sepStr, headFrame, i);
                                outPutStr.SetValue(string.Join(" ", headFrame), 0);
                            }
                            // 提取可用帧
                            outPutStr.SetValue(useFrameStr, ++outPutStrTag);
                            i = i + Convert.ToInt32(tmpstr, 16) + 3;
                            mantissaStartFrame = i + 1;
                        }
                        break;
                    default:
                        break;
                }
            }
            // 提取尾帧
            if (mantissaStartFrame < sepStr.Length)
            {
                outPutStr.SetValue(str.Substring(mantissaStartFrame * 3, str.Length - mantissaStartFrame * 3), outPutStrTag + 1);

            }
            //Console.WriteLine();
            if (outPutStrTag == 0)
            {
                string[] useOutPutStr = new string[1];
                useOutPutStr.SetValue(str, 0);
                return useOutPutStr;
            }
            else if (outPutStr[0] == null)
            {
                outPutStr = outPutStr.Where(s => !string.IsNullOrEmpty(s)).ToArray();
                string[] useOutPutStr = new string[outPutStr.Length + 1];
                Array.Copy(outPutStr, 0, useOutPutStr, 1, outPutStr.Length);
                return useOutPutStr;
            }
            else
            {
                outPutStr = outPutStr.Where(s => !string.IsNullOrEmpty(s)).ToArray();
                return outPutStr;
            }
        }
        /// <summary>
        /// 二进制字符串转换为十六进制字符串并格式化
        /// </summary>
        /// <param name="bytes"></param>
        private string BytestoHexStr(byte[] bytes)
        {
            string HexStr = "";
            for (int i = 0; i < bytes.Length; i++)
            {
                HexStr += string.Format("{0:X2} ", bytes[i]);
            }
            HexStr = HexStr.Trim();
            return HexStr;
        }
        /// <summary>
        /// 两个十六进制字符串求异或
        /// </summary>
        /// <param name="HexStr1"></param>
        /// <param name="HexStr2"></param>
        /// <returns></returns>
        public string HexStrXor(string HexStr1, string HexStr2)
        {
            // 两个十六进制字符串的长度和长度差的绝对值以及异或结果
            int iHexStr1Len = HexStr1.Length;
            int iHexStr2Len = HexStr2.Length;
            int iGap, iHexStrLenLow;
            string result = string.Empty;
            // 获取这两个十六进制字符串长度的差值
            iGap = iHexStr1Len - iHexStr2Len;
            // 获取这两个十六进制字符串长度最小的那一个
            iHexStrLenLow = iHexStr1Len < iHexStr2Len ? iHexStr1Len : iHexStr2Len;
            // 将这两个字符串转换成字节数组
            byte[] bHexStr1 = HexStrToBytes(HexStr1);
            byte[] bHexStr2 = HexStrToBytes(HexStr2);
            int i = 0;
            //先把每个字节异或后得到一个0~15范围内的整数，再转换成十六进制字符
            for (; i < iHexStrLenLow; ++i)
            {
                result += (bHexStr1[i] ^ bHexStr2[i]).ToString("X");
            }

            result += iGap >= 0 ? HexStr1.Substring(i, iGap) : HexStr2.Substring(i, -iGap);
            return result;
        }

        /// <summary>
        /// 一串字符串求异或值
        /// </summary>
        /// <param name="ori"></param>
        /// <returns></returns>
        private string HexCRC(string ori)
        {
            if (ori == null)
            {
                throw new ArgumentNullException(nameof(ori));
            }
            string[] hexvalue = ori.Trim().Split(' ', '	');
            string j = "";
            foreach (string hex in hexvalue)
            {
                j = HexStrXor(j, hex);
            }
            return j;

        }
        /// <summary>
        /// 将十六进制字符串转换为十六进制数组
        /// </summary>
        /// <param name="HexStr"></param>
        /// <returns></returns>
        public byte[] HexStrToBytes(string HexStr)
        {
            if (HexStr == null)
            {
                throw new ArgumentNullException(nameof(HexStr));
            }

            byte[] Bytes = new byte[HexStr.Length];
            try
            {
                for (int i = 0; i < Bytes.Length; ++i)
                {
                    //将每个16进制字符转换成对应的1个字节
                    Bytes[i] = Convert.ToByte(HexStr.Substring(i, 1), 16);
                }
            }
            catch (Exception ex)
            {
                string str = ex.StackTrace;
                Console.WriteLine(str);
            }
            return Bytes;
        }
        /// <summary>
        /// 十六进制字符串转换为浮点数
        /// </summary>
        /// <param name="HexStr"></param>
        /// <returns></returns>
        private float HexStrToFloat(string HexStr)
        {
            if (HexStr == null)
            {
                throw new ArgumentNullException(nameof(HexStr));
            }
            HexStr = HexStr.Replace(" ", "");
            if (HexStr.Length != 8)
            {
                throw new ArgumentNullException(nameof(HexStr));
            }
            int data1 = Convert.ToInt32(HexStr.Substring(0, 2), 16);
            int data2 = Convert.ToInt32(HexStr.Substring(2, 2), 16);
            int data3 = Convert.ToInt32(HexStr.Substring(4, 2), 16);
            int data4 = Convert.ToInt32(HexStr.Substring(6, 2), 16);

            int data = data1 << 24 | data2 << 16 | data3 << 8 | data4;

            int nSign;
            if ((data & 0x80000000) > 0)
            {
                nSign = -1;
            }
            else
            {
                nSign = 1;
            }
            int nExp = data & (0x7F800000);
            nExp = nExp >> 23;
            float nMantissa = data & (0x7FFFFF);

            if (nMantissa != 0)
                nMantissa = 1 + nMantissa / 8388608;

            float value = nSign * nMantissa * (2 << (nExp - 128));
            return value;
        }
        /// <summary>
        /// 浮点数字符串转十六进制字符串
        /// </summary>
        /// <param name="floatStr"></param>
        /// <returns></returns>
        private string FloatStrToHexStr(string floatStr)
        {
            try
            {
                float tmpFloat = float.Parse(floatStr);
                string HexFloat = BitConverter.ToString(BitConverter.GetBytes(tmpFloat));
                return HexFloat = HexFloat.Replace("-", " ");
            }
            catch (Exception ex)
            {
                string str = ex.StackTrace;
                Console.WriteLine(str);
                return "";
            }
        }
        /// <summary>
        /// 仅接受数字
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        private bool IsValid(string input)
        {
            Regex regex = new Regex("[0-9]");
            return regex.IsMatch(input);
        }
        /// <summary>
        /// 仪表参数解析面板清空
        /// </summary>
        private void ParseParameterClear()
        {
            // 清空解析面板
            // 实时数据清空
            resProtocol.Clear(); resAddress.Clear(); resVendor.Clear();
            resType.Clear(); resGroup.Clear(); resFunctionData.Clear();
            resSucRate.Clear(); resBatVol.Clear(); resSleepTime.Clear();
            resStatue.Clear(); resData.Clear(); resTime.Clear();
            // 仪表参数清空
            resModel.Clear(); resSerialNumber.Clear(); resFirmwareVersion.Clear();
            resSoftwareVersion.Clear(); resLowRange.Clear(); resHighRange.Clear();
            resMeasurementAccuracy.Clear(); resProtectionLevel.Clear(); resExplosionProofGrade.Clear();
            resIllustrate.Clear();
            // 校验码清空
            resCRC.Clear();
            // 将前景色改为黑色
            // 实时数据改色
            resProtocol.Foreground = new SolidColorBrush(Colors.Black);
            resAddress.Foreground = new SolidColorBrush(Colors.Black);
            resVendor.Foreground = new SolidColorBrush(Colors.Black);
            resType.Foreground = new SolidColorBrush(Colors.Black);
            resGroup.Foreground = new SolidColorBrush(Colors.Black);
            resFunctionData.Foreground = new SolidColorBrush(Colors.Black);
            resSucRate.Foreground = new SolidColorBrush(Colors.Black);
            resBatVol.Foreground = new SolidColorBrush(Colors.Black);
            resSleepTime.Foreground = new SolidColorBrush(Colors.Black);
            resStatue.Foreground = new SolidColorBrush(Colors.Black);
            resData.Foreground = new SolidColorBrush(Colors.Black);
            resTime.Foreground = new SolidColorBrush(Colors.Black);
            resCRC.Foreground = new SolidColorBrush(Colors.Black);
            // 仪表参数改色
            resModel.Foreground = new SolidColorBrush(Colors.Black);
            resSerialNumber.Foreground = new SolidColorBrush(Colors.Black);
            resFirmwareVersion.Foreground = new SolidColorBrush(Colors.Black);
            resSoftwareVersion.Foreground = new SolidColorBrush(Colors.Black);
            resLowRange.Foreground = new SolidColorBrush(Colors.Black);
            resHighRange.Foreground = new SolidColorBrush(Colors.Black);
            resMeasurementAccuracy.Foreground = new SolidColorBrush(Colors.Black);
            resProtectionLevel.Foreground = new SolidColorBrush(Colors.Black);
            resExplosionProofGrade.Foreground = new SolidColorBrush(Colors.Black);
            resIllustrate.Foreground = new SolidColorBrush(Colors.Black);
            // 校验码改色
            resCRC.Foreground = new SolidColorBrush(Colors.Black);
            // 隐藏字段
            // 实时数据隐藏字段
            resProtocolDockPanel.Visibility = Visibility.Collapsed;
            resAddressDockPanel.Visibility = Visibility.Collapsed;
            resVendorDockPanel.Visibility = Visibility.Collapsed;
            resTypeDockPanel.Visibility = Visibility.Collapsed;
            resGroupDockPanel.Visibility = Visibility.Collapsed;
            resFunctionDataDockPanel.Visibility = Visibility.Collapsed;
            resSucRateDockPanel.Visibility = Visibility.Collapsed;
            resBatVolDockPanel.Visibility = Visibility.Collapsed;
            resSleepTimeDockPanel.Visibility = Visibility.Collapsed;
            resStatueDockPanel.Visibility = Visibility.Collapsed;
            resDataDockPanel.Visibility = Visibility.Collapsed;
            resTimeDockPanel.Visibility = Visibility.Collapsed;
            // 仪表参数隐藏字段
            resModelDockPanel.Visibility = Visibility.Collapsed;
            resSerialNumberDockPanel.Visibility = Visibility.Collapsed;
            resFirmwareVersionDockPanel.Visibility = Visibility.Collapsed;
            resSoftwareVersionDockPanel.Visibility = Visibility.Collapsed;
            resLowRangeDockPanel.Visibility = Visibility.Collapsed;
            resHighRangeDockPanel.Visibility = Visibility.Collapsed;
            resMeasurementAccuracyDockPanel.Visibility = Visibility.Collapsed;
            resProtectionLevelDockPanel.Visibility = Visibility.Collapsed;
            resExplosionProofGradeDockPanel.Visibility = Visibility.Collapsed;
            resIllustrateDockPanel.Visibility = Visibility.Collapsed;
            // 校验码隐藏字段
            resCRCDockPanel.Visibility = Visibility.Collapsed;
        }

        #endregion




        /// <summary>
        /// 标定栏的数据预览
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CalibrationInstrumentModelTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            // 将光标移至文字末尾
            calibrationInstrumentModelTextBox.SelectionStart = calibrationInstrumentModelTextBox.Text.Length;
            MatchCollection hexadecimalCollection = Regex.Matches(e.Text, @"[\da-fA-F]");
            foreach (Match mat in hexadecimalCollection)
            {
                calibrationInstrumentModelTextBox.AppendText(mat.Value);
            }
            // 每输入两个字符自动添加空格
            calibrationInstrumentModelTextBox.Text = calibrationInstrumentModelTextBox.Text.Replace(" ", "");
            calibrationInstrumentModelTextBox.Text = string.Join(" ", Regex.Split(calibrationInstrumentModelTextBox.Text, "(?<=\\G.{2})(?!$)"));
            calibrationInstrumentModelTextBox.SelectionStart = calibrationInstrumentModelTextBox.Text.Length;
            e.Handled = true;
        }
        private void CalibrationSerialNumberTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            // 将光标移至文字末尾
            calibrationSerialNumberTextBox.SelectionStart = calibrationSerialNumberTextBox.Text.Length;
            MatchCollection hexadecimalCollection = Regex.Matches(e.Text, @"[\da-fA-F]");
            foreach (Match mat in hexadecimalCollection)
            {
                calibrationSerialNumberTextBox.AppendText(mat.Value);
            }
            // 每输入两个字符自动添加空格
            calibrationSerialNumberTextBox.Text = calibrationSerialNumberTextBox.Text.Replace(" ", "");
            calibrationSerialNumberTextBox.Text = string.Join(" ", Regex.Split(calibrationSerialNumberTextBox.Text, "(?<=\\G.{2})(?!$)"));
            calibrationSerialNumberTextBox.SelectionStart = calibrationSerialNumberTextBox.Text.Length;
            e.Handled = true;
        }
        private void CalibrationIPRatingTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            // 将光标移至文字末尾
            calibrationIPRatingTextBox.SelectionStart = calibrationIPRatingTextBox.Text.Length;
            MatchCollection hexadecimalCollection = Regex.Matches(e.Text, @"[\da-fA-F]");
            foreach (Match mat in hexadecimalCollection)
            {
                calibrationIPRatingTextBox.AppendText(mat.Value);
            }
            // 每输入两个字符自动添加空格
            calibrationIPRatingTextBox.Text = calibrationIPRatingTextBox.Text.Replace(" ", "");
            calibrationIPRatingTextBox.Text = string.Join(" ", Regex.Split(calibrationIPRatingTextBox.Text, "(?<=\\G.{2})(?!$)"));
            calibrationIPRatingTextBox.SelectionStart = calibrationIPRatingTextBox.Text.Length;
            e.Handled = true;
        }
        private void CalibrationExplosionProofLevelTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            // 将光标移至文字末尾
            calibrationExplosionProofLevelTextBox.SelectionStart = calibrationExplosionProofLevelTextBox.Text.Length;
            MatchCollection hexadecimalCollection = Regex.Matches(e.Text, @"[\da-fA-F]");
            foreach (Match mat in hexadecimalCollection)
            {
                calibrationExplosionProofLevelTextBox.AppendText(mat.Value);
            }
            // 每输入两个字符自动添加空格
            calibrationExplosionProofLevelTextBox.Text = calibrationExplosionProofLevelTextBox.Text.Replace(" ", "");
            calibrationExplosionProofLevelTextBox.Text = string.Join(" ", Regex.Split(calibrationExplosionProofLevelTextBox.Text, "(?<=\\G.{2})(?!$)"));
            calibrationExplosionProofLevelTextBox.SelectionStart = calibrationExplosionProofLevelTextBox.Text.Length;
            e.Handled = true;
        }
        private void CalibrationInstructionsTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            // 将光标移至文字末尾
            calibrationInstructionsTextBox.SelectionStart = calibrationInstructionsTextBox.Text.Length;
            MatchCollection hexadecimalCollection = Regex.Matches(e.Text, @"[\da-fA-F]");
            foreach (Match mat in hexadecimalCollection)
            {
                calibrationInstructionsTextBox.AppendText(mat.Value);
            }
            // 每输入两个字符自动添加空格
            calibrationInstructionsTextBox.Text = calibrationInstructionsTextBox.Text.Replace(" ", "");
            calibrationInstructionsTextBox.Text = string.Join(" ", Regex.Split(calibrationInstructionsTextBox.Text, "(?<=\\G.{2})(?!$)"));
            calibrationInstructionsTextBox.SelectionStart = calibrationInstructionsTextBox.Text.Length;
            e.Handled = true;
        }



        /// <summary>
        /// 画折线图
        /// </summary>
        private ObservableDataSource<Point> dataSource = new ObservableDataSource<Point>();
        private int plotPointX = 1;
        double plotPointY = 0.0;
        private bool connectFlag = false;

        private void AnimatedPlot()
        {
            double x = plotPointX;
            try
            {
                plotPointY = realTimeData;
            }
            catch (Exception ex)
            {
                string str = ex.StackTrace;
                Console.WriteLine(str);
            }

            Point point = new Point(x, plotPointY);
            dataSource.AppendAsync(Dispatcher, point);
            //if (plotPointX >= 5)
            //{
            //    plotter.Viewport.Visible = new Rect(plotPointX - 5, -1, 5, 24);
            //}
            //else
            //{
            //    plotter.Viewport.Visible = new Rect(0, -1, 5, 24);
            //}
            //plotter.Viewport.FitToView();
            plotPointX++;
        }


        private void Plot_Loaded()
        {
            plotter.AxisGrid.Visibility = Visibility.Hidden;
            plotter.AddLineGraph(dataSource, Colors.Blue, 2, "实时数据");
            plotter.Viewport.Visible = new Rect(0, -1, 5, 24);
            plotter.Viewport.FitToView();
        }

        private void RegularDataUpdateRate_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            {
                if (regularDataUpdateRate.Text != "")
                {
                    if (IsValid(regularDataUpdateRate.Text) == false || Convert.ToInt32(regularDataUpdateRate.Text, 16) > 65535 || Convert.ToInt32(regularDataUpdateRate.Text, 16) < 0)
                    {
                        MessageBox.Show("请输入0 - 65535整数");
                        regularDataUpdateRate.Text = "8";
                    }
                }
            }
        }
    }


}
