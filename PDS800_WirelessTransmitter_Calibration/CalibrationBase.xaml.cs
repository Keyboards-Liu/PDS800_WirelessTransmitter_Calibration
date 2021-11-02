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
        private string DateStr { get; set; }
        // 时刻
        private string TimeStr { get; set; }
        //// 发送和接收队列
        //private static Queue receiveData = new Queue();
        //private static Queue sendData = new Queue();
        // 发送和接收字节数
        private static uint receiveBytesCount = 0;
        private static uint sendBytesCount = 0;
        // 发送和接收次数
        private static uint receiveCount = 0;
        private static uint sendCount = 0;
        // 帧头
        private static string frameHeader;
        // 长度域
        private static string frameLength;
        // 命令域
        private static string frameCommand;
        // 数据地址域
        private static string frameAddress;
        // 数据内容域
        private static string frameContent;
        // 校验码
        private static string frameCRC;
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
            DateStr = DateTime.Now.ToString("yyyy-MM-dd");
            TimeStr = DateTime.Now.ToString("HH:mm:ss");
            operationTime.Text = DateStr + " " + TimeStr;
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
            catch
            {
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
                // 清空缓冲区
                serialPort.DiscardInBuffer();
                serialPort.DiscardOutBuffer();
            }
            catch
            {
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
            }
            catch
            {
                // 异常时显示提示文字
                statusTextBlock.Text = "关闭串口出错！";
                turnOnButton.IsChecked = true;
            }
        }
        #endregion

        #region 串口数据接收处理/窗口显示清空功能
        /// <summary>
        /// 接收串口数据, 转换为16进制字符串, 传递到显示功能
        /// </summary>
        /// <param name="sender">事件源的对象</param>
        /// <param name="e">事件数据的对象</param>
        public void ReceiveData(object sender, SerialDataReceivedEventArgs e)
        {
            Thread.Sleep(70);
            receiveCount++;
            // Console.WriteLine("接收" + receiveCount + "次");
            // 读取缓冲区内所有字节
            byte[] receiveBuffer = new byte[serialPort.BytesToRead];
            serialPort.Read(receiveBuffer, 0, receiveBuffer.Length);
            // 字符串转换为十六进制字符串
            string receiveText = BytestoHexStr(receiveBuffer);
            // Console.WriteLine(receiveText);
            // 传参 (Invoke方法暂停工作线程, BeginInvoke方法不暂停)
            if (receiveText.Length >= 2)
            {
                try
                {
                    switch (receiveText.Substring(0, 2))
                    {
                        case "FE":
                            {
                                if (((receiveText.Length + 1) / 3) == 27)
                                {
                                    statusReceiveByteTextBlock.Dispatcher.Invoke(new Action(delegate
                                    {
                                        ShowReceiveData(receiveText);
                                        ShowParseText(receiveText);
                                        ShowParseParameter(receiveText);
                                    }));
                                }
                                else if (((receiveText.Length + 1) / 3) != 27 && receiveText.Replace(" ", "") != "")
                                {
                                    statusReceiveByteTextBlock.Dispatcher.Invoke(new Action(delegate
                                    {
                                        ShowReceiveData(receiveText);
                                    }));
                                }
                            }
                            break;
                        case "7E":
                            {
                                if (((receiveText.Length + 1) / 3) == 42)
                                {
                                    statusReceiveByteTextBlock.Dispatcher.Invoke(new Action(delegate
                                    {
                                        ShowReceiveData(receiveText);
                                        ShowParseText(receiveText);
                                        ShowParseParameter(receiveText);
                                    }));
                                }
                                else if (((receiveText.Length + 1) / 3) != 42 && receiveText.Replace(" ", "") != "")
                                {
                                    statusReceiveByteTextBlock.Dispatcher.Invoke(new Action(delegate
                                    {
                                        ShowReceiveData(receiveText);
                                    }));
                                }
                            }
                            break;
                        default:
                            break;
                    }

                }
                catch
                {

                }
            }


        }
        public static void PrintValues(IEnumerable myCollection)
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
        /// 接收文本解析面板显示功能
        /// </summary>
        /// <param name="receiveText"></param>
        public void ShowParseText(string receiveText)
        {
            // 接收文本解析面板写入
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
                            // 数据内容域 (长度域指示长度 - 2)
                            frameContent = receiveText.Substring((0 + 1 + 1 + 2 + 2) * 3, ((Convert.ToInt32(frameLength, 16) - 2) * 3) - 1);
                            // 校验码 (1位)
                            frameCRC = receiveText.Substring(receiveText.Length - 2, 2);
                        }
                        break;
                    case "7E":
                        {
                            // 帧头 (1位)
                            frameHeader = receiveText.Substring(0 * 3, (1 * 3) - 1);
                            // 长度域 (2位, 最长为FF = 65535)
                            frameLength = receiveText.Substring((0 + 1) * 3, (2 * 3) - 1);
                            // 命令域 (1位，指示是否收到数据)
                            frameCommand = receiveText.Substring((0 + 1 + 2) * 3, (1 * 3) - 1);
                            // 数据地址域 (8位)
                            frameAddress = receiveText.Substring((0 + 1 + 2 + 1) * 3, (8 * 3) - 1);
                            // 数据内容域 (长度域指示长度38 - 命令域长度1 - 地址域长度8 - 固定长度9 = 20)
                            frameContent = receiveText.Substring(receiveText.Length - (21 * 3) + 1, (20 * 3) - 1);
                            // 校验码 (1位)
                            frameCRC = receiveText.Substring(receiveText.Length - 2, 2);
                        }
                        break;
                    default:
                        break;
                }
            }
            catch
            {
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
                            string j = "";
                            string[] hexvalue = receiveText.Trim().Split(' ');
                            // 求字符串异或值
                            foreach (string hex in hexvalue) j = HexStrXor(j, hex);
                            if (j == frameHeader)
                            {
                                resCRC.Text = "通过";
                                // 校验成功写入其他解析参数
                                // 无线仪表数据域帧头
                                {
                                    // 通信协议
                                    try
                                    {
                                        // 1 0x0001 ZigBee SZ9-GRM V3.01油田专用通讯协议（国产四信）
                                        //string frameProtocol = frameContent.Substring(0, 5).Replace(" ", "");
                                        //int intFrameProtocol = Convert.ToInt32(frameProtocol, 16);
                                        //switch (intFrameProtocol)
                                        //{
                                        //    case 0x0001:
                                        resProtocol.Text = "ZigBee SZ9-GRM V3.01油田专用通讯协议（国产四信）";
                                        //        break;
                                        //    default:
                                        //        resProtocol.Text = "未知";
                                        //        resProtocol.Foreground = new SolidColorBrush(Colors.Red);
                                        //        break;
                                        //}
                                    }
                                    catch
                                    {
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
                                    }
                                    catch
                                    {
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
                                    }
                                    catch
                                    {
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
                                    }
                                    catch
                                    {
                                        // 异常时显示提示文字
                                        statusTextBlock.Text = "仪表类型解析出错！";
                                        turnOnButton.IsChecked = false;
                                        return;
                                    }
                                    // 仪表组号
                                    try
                                    {
                                        resGroup.Text = Convert.ToInt32(frameContent.Substring(18, 2).Replace(" ", ""), 16) + "组" + Convert.ToInt32(frameContent.Substring(21, 2).Replace(" ", ""), 16) + "号";
                                    }
                                    catch
                                    {
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
                                    }
                                    catch
                                    {
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
                                }
                                catch
                                {
                                    // 异常时显示提示文字
                                    statusTextBlock.Text = "通信效率解析出错！";
                                    turnOnButton.IsChecked = false;
                                    return;
                                }
                                // 电池电压
                                try
                                {
                                    resBatVol.Text = Convert.ToInt32(frameContent.Substring(33, 2), 16) + "%";
                                }
                                catch
                                {
                                    // 异常时显示提示文字
                                    statusTextBlock.Text = "电池电压解析出错！";
                                    turnOnButton.IsChecked = false;
                                    return;
                                }
                                // 休眠时间
                                try
                                {
                                    resSleepTime.Text = Convert.ToInt32(frameContent.Substring(36, 5).Replace(" ", ""), 16) + "秒";
                                }
                                catch
                                {
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
                                        string messageBoxText = "设备上报" + count + "个故障: \n" + failureMessage;
                                        string caption = "设备故障";
                                        MessageBoxButton button = MessageBoxButton.OK;
                                        MessageBoxImage icon = MessageBoxImage.Error;
                                        MessageBox.Show(messageBoxText, caption, button, icon);
                                    }
                                    else
                                    {
                                        resStatue.Text = "正常";
                                    }
                                }
                                catch
                                {
                                    // 异常时显示提示文字
                                    statusTextBlock.Text = "仪表状态解析出错！";
                                    turnOnButton.IsChecked = false;
                                    return;
                                }
                                // 运行时间
                                try
                                {
                                    resTime.Text = Convert.ToInt32(frameContent.Substring(48, 5).Replace(" ", ""), 16).ToString() + "小时";
                                }
                                catch
                                {
                                    // 异常时显示提示文字
                                    statusTextBlock.Text = "运行时间解析出错！";
                                    turnOnButton.IsChecked = false;
                                    return;
                                }
                                // 实时数据
                                try
                                {

                                    string frameresData = frameContent.Substring(54, 5).Replace(" ", "").TrimStart('0');
                                    resData.Text = frameresData + "MPa";
                                    // 十六进制字符串转换为浮点数字符串
                                    //string frameresData = frameContent.Substring(48, 11).Replace(" ", "");
                                    //double flFrameData = HexStrToFloat(frameresData);
                                    //resData.Text = flFrameData.ToString();
                                }
                                catch
                                {
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

                        }
                        break;
                    case "7E":
                        {
                            //字符串校验
                            int j = 0;
                            string txt = receiveText.Trim();
                            string[] hexvalue = txt.Remove(0, 9).Remove(txt.Length - 12, 3).Split(' ');
                            // 0x00 - 字符串求和
                            foreach (string hex in hexvalue) j = j + Convert.ToInt32(hex, 16);
                            string hexj = (0xFF - Convert.ToInt32((j.ToString("X").Substring(j.ToString("X").Length - 2, 2)), 16)).ToString("X");
                            if (hexj == frameCRC)
                            //if (j == frameHeader)
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
                                        resProtocol.Text = "ZigBee（Digi International）";
                                        //        break;
                                        //    default:
                                        //        resProtocol.Text = "未知";
                                        //        resProtocol.Foreground = new SolidColorBrush(Colors.Red);
                                        //        break;
                                        //}
                                    }
                                    catch
                                    {
                                        // 异常时显示提示文字
                                        statusTextBlock.Text = "通信协议解析出错！";
                                        turnOnButton.IsChecked = false;
                                        return;
                                    }
                                    // 网络地址
                                    try
                                    {
                                        //string frameContentAddress = (frameAddress.Substring(3, 2) + frameAddress.Substring(0, 2)).Replace(" ", "");
                                        //int intFrameContentAddress = Convert.ToInt32(frameContentAddress, 16);
                                        //resAddress.Text = intFrameContentAddress.ToString();
                                        resAddress.Text = frameAddress;
                                    }
                                    catch
                                    {
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
                                    }
                                    catch
                                    {
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
                                    }
                                    catch
                                    {
                                        // 异常时显示提示文字
                                        statusTextBlock.Text = "仪表类型解析出错！";
                                        turnOnButton.IsChecked = false;
                                        return;
                                    }
                                    // 仪表组号
                                    try
                                    {
                                        resGroup.Text = Convert.ToInt32(frameContent.Substring(18, 2).Replace(" ", ""), 16) + "组" + Convert.ToInt32(frameContent.Substring(21, 2).Replace(" ", ""), 16) + "号";
                                    }
                                    catch
                                    {
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
                                    }
                                    catch
                                    {
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
                                }
                                catch
                                {
                                    // 异常时显示提示文字
                                    statusTextBlock.Text = "通信效率解析出错！";
                                    turnOnButton.IsChecked = false;
                                    return;
                                }
                                // 电池电压
                                try
                                {
                                    resBatVol.Text = Convert.ToInt32(frameContent.Substring(33, 2), 16) + "%";
                                }
                                catch
                                {
                                    // 异常时显示提示文字
                                    statusTextBlock.Text = "电池电压解析出错！";
                                    turnOnButton.IsChecked = false;
                                    return;
                                }
                                // 休眠时间
                                try
                                {
                                    resSleepTime.Text = Convert.ToInt32(frameContent.Substring(36, 5).Replace(" ", ""), 16) + "秒";
                                }
                                catch
                                {
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
                                        string messageBoxText = "设备上报" + count + "个故障: \n" + failureMessage;
                                        string caption = "设备故障";
                                        MessageBoxButton button = MessageBoxButton.OK;
                                        MessageBoxImage icon = MessageBoxImage.Error;
                                        MessageBox.Show(messageBoxText, caption, button, icon);
                                    }
                                    else
                                    {
                                        resStatue.Text = "正常";
                                    }
                                }
                                catch
                                {
                                    // 异常时显示提示文字
                                    statusTextBlock.Text = "仪表状态解析出错！";
                                    turnOnButton.IsChecked = false;
                                    return;
                                }
                                // 运行时间
                                try
                                {
                                    resTime.Text = Convert.ToInt32(frameContent.Substring(48, 5).Replace(" ", ""), 16).ToString() + "小时";
                                }
                                catch
                                {
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
                                    resData.Text = flFrameData.ToString() + "MPa";
                                }
                                catch
                                {
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

                        }
                        break;
                    default:
                        resProtocol.Text = "未知";
                        resAddress.Foreground = new SolidColorBrush(Colors.Red);
                        break;
                }
            }
            catch
            {
                // 异常时显示提示文字
                statusTextBlock.Text = "参数解析出错！";
                turnOnButton.IsChecked = false;
            }
            //string strConn = @"Data Source=.;Initial Catalog=Test; integrated security=True;";
            //SqlConnection conn = new SqlConnection(strConn);


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
            serialPort.DiscardOutBuffer();
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
                    catch
                    {
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

                serialPort.Write(sendBuffer, 0, sendBuffer.Length);
                // 更新发送数据计数
                sendBytesCount += (uint)sendBuffer.Length;
                statusSendByteTextBlock.Text = sendBytesCount.ToString();
            }
            catch
            {
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

        #region 方法打包
        /// <summary>
        /// 二进制字符串转换为十六进制字符串并格式化
        /// </summary>
        /// <param name="bytes"></param>
        private static string BytestoHexStr(byte[] bytes)
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
        public static string HexStrXor(string HexStr1, string HexStr2)
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
        public static byte[] HexStrToBytes(string HexStr)
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
            catch (Exception e)
            {
                throw new Exception(e.Message);
            }
            return Bytes;
        }
        /// <summary>
        /// 十六进制字符串转换为浮点数
        /// </summary>
        /// <param name="HexStr"></param>
        /// <returns></returns>
        private static float HexStrToFloat(string HexStr)
        {
            //if (HexStr == null)
            //{
            //    throw new ArgumentNullException(nameof(HexStr));
            //}
            //string binData = Convert.ToString(Convert.ToInt32(HexStr, 16), 2).PadLeft(32, '0');
            //int binData_Sign = (1 - Convert.ToInt32(binData.Substring(0, 1), 2) * 2);
            //int binData_Exp = Convert.ToInt32(binData.Substring(1, 8), 2) - 127;
            //string binData_Mant = "1" + binData.Substring(9, 23);
            //if (binData_Exp >= 0)
            //{
            //    binData_Mant = binData_Mant.Insert(binData_Exp + 1, ".");
            //}
            //else binData_Mant = binData_Mant.PadLeft(binData_Mant.Length - binData_Sign, '0').Insert(1, ".");
            //string[] binDataStr = binData_Mant.Split('.');
            //double flData = 0.0;
            //for (int i = 0; i < binDataStr[0].Length; i++)
            //{
            //    double EXP = Math.Pow(2, binDataStr[0].Length - i - 1);
            //    flData += Convert.ToInt32(binDataStr[0].Substring(i, 1)) * EXP;
            //}
            //for (int i = 0; i < binDataStr[1].Length; i++)
            //{
            //    double EXP = Math.Pow(2, -i - 1);
            //    flData += Convert.ToInt32(binDataStr[1].Substring(i, 1)) * EXP;
            //}
            //return flData;
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
        /// 仪表参数解析面板清空
        /// </summary>
        private void ParseParameterClear()
        {
            // 清空解析面板
            resProtocol.Clear(); resAddress.Clear(); resVendor.Clear();
            resType.Clear(); resGroup.Clear(); resFunctionData.Clear();
            resSucRate.Clear(); resBatVol.Clear(); resSleepTime.Clear();
            resStatue.Clear(); resData.Clear(); resCRC.Clear();
            // 将前景色改为黑色
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
            resCRC.Foreground = new SolidColorBrush(Colors.Black);
        }
        #endregion

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
                    // 生成16进制字符串
                    sendTextBox.Text = EstablishBuild_Text();
                    // 标定连接发送
                    // SerialPortSend();
                    // 指示灯变绿
                    if (true)
                    {
                        connectionStatusEllipse.Fill = Brushes.Green;
                    }
                    establishConnectionButton.Content = "关闭连接";
                }
                catch
                {
                    // 异常时显示提示文字
                    statusTextBlock.Text = "建立连接出错！";
                    // 指示灯变灰
                    connectionStatusEllipse.Fill = Brushes.Gray;
                }
            }
            else statusTextBlock.Text = "请先解析仪表参数！";
        }

        private void EstablishConnectionButton_Unchecked(object sender, RoutedEventArgs e)
        {
            connectionStatusEllipse.Fill = Brushes.Gray;
            establishConnectionButton.Content = "建立连接";
        }

        private string EstablishBuild_Text()
        {
            string str = "";
            switch (frameHeader)
            {
                case "FE":
                    {
                        // 获取所需解析数据
                        ParameterAcquisition_FE(out string strHeader, out string strCommand, out string strAddress, out string strProtocolVendor, out string strHandler, out string strGroup, out string strFunctionData);
                        // 写操作数据区
                        string strHandlerContent = "F0";
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
                        // 获取所需解析数据
                        ParameterAcquisition_7E(out string strHeader, out string strCommand, out string strAddress, out string strProtocolVendor, out string strHandler, out string strGroup, out string strFunctionData);
                        // 写操作数据区
                        string strHandlerContent = "F0";
                        // 合成数据域
                        string strContent = strCommand + " " + frameAddress + " FF FE E8 E8 00 11 18 57 01 " + strProtocolVendor + " " + strHandler + " " + strGroup + " " + strFunctionData + " " + strHandlerContent;
                        // 计算长度域
                        int intLength = (strContent.Length + 1) / 3;
                        string strLength = Convert.ToString(intLength, 16).ToUpper().PadLeft(4, '0').Insert(2, " ");
                        string strInner = strLength + " " + strContent;
                        // 计算异或校验码
                        string strCRC = HexCRC(strInner);
                        // 合成返回值
                        str = strHeader + " " + strInner + " " + strCRC;
                    }
                    break;
                default:
                    break;
            }
            return str;

        }



        private void ParameterAcquisition_FE(out string strHeader, out string strCommand, out string strAddress, out string strProtocolVendor, out string strHandler, out string strGroup, out string strFunctionData)
        {
            // 获取所需解析数据
            // 帧头
            strHeader = "FE";
            // 发送命令域
            strCommand = "44 5F";
            // 发送地址
            strAddress = frameAddress;
            // 协议和厂商号为数据内容前四位
            strProtocolVendor = frameContent.Substring(0, 11);
            // 仪表类型：手操器
            strHandler = "1F 10";
            // 组号表号
            strGroup = frameContent.Substring(18, 5);
            // 功能码 / 数据类型
            strFunctionData = "00 80";
        }
        private void ParameterAcquisition_7E(out string strHeader, out string strCommand, out string strAddress, out string strProtocolVendor, out string strHandler, out string strGroup, out string strFunctionData)
        {
            // 帧头
            strHeader = "7E";
            // 发送命令域
            strCommand = "91";
            // 发送地址
            strAddress = frameAddress;
            // 协议和厂商号为数据内容前四位
            strProtocolVendor = frameContent.Substring(0, 11);
            // 仪表类型：手操器
            strHandler = "1F 10";
            // 组号表号
            strGroup = frameContent.Substring(18, 5);
            // 功能码 / 数据类型
            strFunctionData = "00 80";
        }
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
                    if (true)
                    {
                        establishConnectionButton.IsChecked = false;
                    }
                }
                catch
                {
                    // 异常时显示提示文字
                    statusTextBlock.Text = "描述标定出错！";
                }
            }
            else statusTextBlock.Text = "请先建立连接！";
        }

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
                catch
                {
                    // 异常时显示提示文字
                    statusTextBlock.Text = "描述标定出错！";
                }
            }
            else statusTextBlock.Text = "请先建立连接！";
        }

        private string ParameterCalibration_Text()
        {
            string str = "";
            switch (frameHeader)
            {
                case "FE":
                    {
                        // 获取所需解析数据
                        ParameterAcquisition_FE(out string strHeader, out string strCommand, out string strAddress, out string strProtocolVendor, out string strHandler, out string strGroup, out string strFunctionData);
                        // 获取设备描述标定信息
                        // 写操作数据区
                        string strHandlerContent = "F2 " + calibrationCommandNumberTextBox.Text.Trim() + " " + calibrationUnitTextBox.Text.Trim();
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
                        // 获取所需解析数据
                        ParameterAcquisition_7E(out string strHeader, out string strCommand, out string strAddress, out string strProtocolVendor, out string strHandler, out string strGroup, out string strFunctionData);
                        // 获取设备描述标定信息
                        // 写操作数据区
                        string strHandlerContent = "F2 " + calibrationCommandNumberTextBox.Text.Trim() + " " + calibrationUnitTextBox.Text.Trim();
                        // 合成数据域
                        string strContent = strCommand + " " + frameAddress + " FF FE E8 E8 00 11 18 57 01 " + strProtocolVendor + " " + strHandler + " " + strGroup + " " + strFunctionData + " " + strHandlerContent;
                        // 计算长度域
                        int intLength = (strContent.Length + 1) / 3;
                        string strLength = Convert.ToString(intLength, 16).ToUpper().PadLeft(4, '0').Insert(2, " ");
                        string strInner = strLength + " " + strContent;
                        // 计算异或校验码
                        string strCRC = HexCRC(strInner);
                        // 合成返回值
                        str = strHeader + " " + strInner + " " + strCRC;
                    }
                    break;
                default:
                    break;
            }

            return str;
        }


        private string DescribeCalibration_Text()
        {
            string str = "";
            switch (frameHeader)
            {
                case "FE":
                    {
                        // 获取所需解析数据
                        ParameterAcquisition_FE(out string strHeader, out string strCommand, out string strAddress, out string strProtocolVendor, out string strHandler, out string strGroup, out string strFunctionData);
                        // 获取设备描述标定信息
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
                        // 获取所需解析数据
                        ParameterAcquisition_7E(out string strHeader, out string strCommand, out string strAddress, out string strProtocolVendor, out string strHandler, out string strGroup, out string strFunctionData);
                        // 获取设备描述标定信息
                        // 写操作数据区
                        string strHandlerContent = "F1 " + calibrationInstrumentModelTextBox.Text.Trim() + " " + calibrationSerialNumberTextBox.Text.Trim() + " " + calibrationIPRatingTextBox.Text.Trim() + " " + calibrationExplosionProofLevelTextBox.Text.Trim() + " " + calibrationInstructionsTextBox.Text.Trim();
                        // 合成数据域
                        string strContent = strCommand + " " + frameAddress + " FF FE E8 E8 00 11 18 57 01 " + strProtocolVendor + " " + strHandler + " " + strGroup + " " + strFunctionData + " " + strHandlerContent;
                        // 计算长度域
                        int intLength = (strContent.Length + 1) / 3;
                        string strLength = Convert.ToString(intLength, 16).ToUpper().PadLeft(4, '0').Insert(2, " ");
                        string strInner = strLength + " " + strContent;
                        // 计算异或校验码
                        string strCRC = HexCRC(strInner);
                        // 合成返回值
                        str = strHeader + " " + strInner + " " + strCRC;
                    }
                    break;
                default:
                    break;
            }

            return str;
        }

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

        private void CalibrationParametersComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (calibrationParametersComboBox.Text.Length >= 4)
            {
                calibrationCommandNumberTextBox.Text = calibrationParametersComboBox.Text.Substring(2, 2);
            }
        }


    }
}