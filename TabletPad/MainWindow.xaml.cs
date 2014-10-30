using RegawMOD.Android;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Forms;
using System.Drawing;
using System.Globalization;

namespace TabletPad
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {

        #region variables&Init

        System.Drawing.Size resolution;
        NumberFormatInfo provider = new NumberFormatInfo();
        BackgroundWorker adbFilter = null;
        AndroidController android;
        Device device;


        public MainWindow()
        {
            InitializeComponent();
            KillProcess("adb.exe");
            android = AndroidController.Instance;
            resolution = Screen.PrimaryScreen.Bounds.Size;
            provider.NumberDecimalSeparator = ".";
            provider.NumberGroupSeparator = " ";
            provider.NumberGroupSizes = new int[] { 3 };
        }

        #endregion variables&Init

        #region buttonHandlers
        private void Connect_Click(object sender, RoutedEventArgs e)
        {

            Disonnect.IsEnabled = true;
            UseAsMouse.IsEnabled = true;
            Connect.IsEnabled = false;

            if (adbFilter != null) return;

            string serial;

            //Always call UpdateDeviceList() before using AndroidController on devices to get the most updated list
            android.UpdateDeviceList();

            if (android.HasConnectedDevices)
            {
                serial = android.ConnectedDevices[0];
                device = android.GetConnectedDevice(serial);
            }

            adbFilter = new BackgroundWorker();
            adbFilter.WorkerReportsProgress = true;
            adbFilter.DoWork += worker_DoWork;
            adbFilter.ProgressChanged += worker_ProgressChanged;
            adbFilter.WorkerSupportsCancellation = true;

            adbFilter.RunWorkerAsync(device);
        }

        private void Disonnect_Click(object sender, RoutedEventArgs e)
        {
            Disonnect.IsEnabled = false;
            UseAsMouse.IsEnabled = false;
            StopUsingAsMouse.IsEnabled = false;
            Connect.IsEnabled = true;

            if (adbFilter != null) adbFilter.ProgressChanged -= mouseMoverProgressInspector;

            if (adbFilter != null)
            {
                adbFilter.CancelAsync();
                adbFilter.Dispose();
                adbFilter = null;
            }

        }

        private void UseAsMouse_Click(object sender, RoutedEventArgs e)
        {
            UseAsMouse.IsEnabled = false;
            StopUsingAsMouse.IsEnabled = true;
            if (adbFilter != null) adbFilter.ProgressChanged += mouseMoverProgressInspector;
        }

        private void StopUsingAsMouse_Click(object sender, RoutedEventArgs e)
        {
            UseAsMouse.IsEnabled = true;
            StopUsingAsMouse.IsEnabled = false;

            if (adbFilter != null) adbFilter.ProgressChanged -= mouseMoverProgressInspector;
        }

        #endregion buttonHandlers

        #region worker&workerHandlers
        private void mouseMoverProgressInspector(object sender, ProgressChangedEventArgs e)
        {
            //converting - filtering dummy rows
            string data = (string)e.UserState;
            if (data.StartsWith("--")) return;
            double x = Convert.ToDouble(data.Split(',')[0].Split('(')[1].Trim(), provider);
            double y = Convert.ToDouble(data.Split(',')[1].Split(')')[0].Trim(), provider);
            //scaling
            x *= resolution.Width;
            y *= resolution.Height;
            //move cursor
            SetCursorPos((int)x, (int)y);
        }
        private void worker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            LogCatBox.Items.Add((string)e.UserState);
        }

        private void worker_DoWork(object sender, DoWorkEventArgs e)
        {
            BackgroundWorker me = adbFilter;
            Device device = (Device)e.Argument;
            //clear the logcat
            AdbCommand adbCmd = Adb.FormAdbCommand("logcat", "-c");
            Adb.ExecuteAdbCommand(adbCmd);

            while (true)
            {
                if (me.CancellationPending)
                {
                    e.Cancel = true;
                    return;
                }
                //dump and clear as fast as it can
                adbCmd = Adb.FormAdbCommand("logcat", "-d -s Sensors:I");
                string logcatOutput = Adb.ExecuteAdbCommand(adbCmd);
                adbCmd = Adb.FormAdbCommand("logcat", "-c");
                Adb.ExecuteAdbCommand(adbCmd);

                foreach (var line in logcatOutput.Split(new char[] { '\r', '\n' }))
                {
                    var linet = line.Trim();
                    if (linet.Length > 0)
                        (sender as BackgroundWorker).ReportProgress(0, linet);
                }

            }
        }

        #endregion worker&workerHandlers

        #region ApiNeed
        private void KillProcess(string processName)
        {
            foreach (Process p in Process.GetProcesses())
            {
                try
                {
                    if (processName.StartsWith(p.ProcessName))
                    {
                        p.Kill();
                    }
                }
                catch { }
            }
        }
        [DllImport("user32")]
        private static extern int SetCursorPos(int x, int y);

        #endregion ApiNeed
    }
}
