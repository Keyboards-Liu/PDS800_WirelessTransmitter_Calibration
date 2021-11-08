using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Research.DynamicDataDisplay;
using Microsoft.Research.DynamicDataDisplay.DataSources;

namespace PDS800_Wireless_Transmitter_Message_Analysis
{
    public partial class DataDisplay : Window
    {
        private ObservableDataSource<Point> dataSource = new ObservableDataSource<Point>();
        private PerformanceCounter dataPerformance = new PerformanceCounter();
        private DispatcherTimer timer = new DispatcherTimer();
        private int i = 0;

        public DataDisplay()
        {
            InitializeComponent();
        }

        private void AnimatedPlot(object sender, EventArgs e)
        {
            dataPerformance.CategoryName = "Processor";
            dataPerformance.CounterName = "% Processor Time";
            dataPerformance.InstanceName = "_Total";

            double x = i;
            double y = dataPerformance.NextValue();

            Point point = new Point(x, y);
            dataSource.AppendAsync(base.Dispatcher, point);

            dataUsageText.Text = String.Format("{0:0}%", y);
            i++;
        }


        internal void Window_Loaded()
        {
            plotter.AxisGrid.Visibility = Visibility.Hidden;
            plotter.AddLineGraph(dataSource, Colors.Red, 2, "Percentage");
            timer.Interval = TimeSpan.FromSeconds(0.1);
            timer.Tick += new EventHandler(AnimatedPlot);
            timer.IsEnabled = true;
            plotter.Viewport.FitToView();
        }
    }
    
}



