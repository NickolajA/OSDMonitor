using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace OSDMonitor
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        //' Construct command line object and get base directory of application
        string cmdLine = Environment.CommandLine;
        string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;

        protected override void OnStartup(StartupEventArgs e)
        {
            //' Relaunch application if command line is empty
            if (cmdLine.Contains("WINPE"))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = baseDirectory + "OSDMonitor.exe",
                    Arguments = "OSPE"
                });
                Application.Current.Shutdown();
            }
            else if (cmdLine.Contains("END"))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = baseDirectory + "OSDMonitor.exe",
                    Arguments = "OSCOM"
                });
                Application.Current.Shutdown();
            }
            else if (cmdLine.Contains("FULLOS"))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = baseDirectory + "OSDMonitor.exe",
                    Arguments = "OSFULL"
                });
                Application.Current.Shutdown();
            }
            else
            {
                var mainWindow = new MainWindow
                {
                    ShowActivated = false
                };
                mainWindow.Show();
            }
        }
    }
}
