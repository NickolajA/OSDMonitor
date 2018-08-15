using Microsoft.Win32;
using OSDMonitor.ConfigMgrWebService;
using System;
using System.Windows;

namespace OSDMonitor
{
    public partial class MainWindow : Window
    {
        //' Construct SoapClient for ConfigMgr WebService
        public static ConfigMgrWebServiceSoapClient webService = new ConfigMgrWebServiceSoapClient();

        //' Construct timer
        System.Windows.Forms.Timer timer = null;

        //' Construct variable for task sequence type
        public static string sequenceType = string.Empty;

        //' Construct enums
        public enum MonitoringSessionState {
            Ending = 1,
            Running = 2,
            NotStarted = 3
        }

        public MainWindow()
        {
            InitializeComponent();
            try
            {
                //' Ensure that task sequence environment can be loaded
                TSEnvironment.TestTSEnvironment();

                //' Determine if a task sequence for operating system deployment is running
                sequenceType = TSEnvironment.GetTSVariable("_SMSTSType");

                //' Handle session ending event from operating system
                SystemEvents.SessionEnding += new SessionEndingEventHandler(SystemEvents_SessionEnding);

                //' Construct new timer to fire accordingly to application settings
                timer = new System.Windows.Forms.Timer();
                timer.Interval = Properties.Settings.Default.MonitorIntervalMilliseconds;
                timer.Tick += new EventHandler(Timer_Tick);
                timer.Start();

            }
            catch (System.Exception ex)
            {
                MessageBox.Show("Unable to load Task Sequence environment", "Load failure", MessageBoxButton.OK, MessageBoxImage.Error);
                Application.Current.Shutdown();
            }
        }

        public void Timer_Tick(object sender, EventArgs e)
        {
            //' Determine if a task sequence for operating system deployment is running
            MonitoringSessionState monitoringState;
            if (sequenceType == "2")
            {
                monitoringState = MonitoringSessionState.Running;
            }
            else 
            {
                sequenceType = TSEnvironment.GetTSVariable("_SMSTSType");
                monitoringState = MonitoringSessionState.NotStarted;
            }

            //' Add monitoring data
            AddMonitoringData(monitoringState);
        }

        async public static void AddMonitoringData(MonitoringSessionState monitoringState)
        {
            //' Instantiate variables
            string deploymentId = null;
            string stepName = null;
            string currentStep = null;
            string totalSteps = null;
            string details = null;

            //' Determine unique id for deployment
            string uniqueId = TSEnvironment.GetTSVariable(Properties.Settings.Default.UniqueMonitoringTSVariableName);
            if (String.IsNullOrEmpty(uniqueId))
            {
                uniqueId = Guid.NewGuid().ToString();
                TSEnvironment.SetTSVariable(Properties.Settings.Default.UniqueMonitoringTSVariableName, uniqueId);
            }

            // --- something with _SMSTSLastActionSucceeded to set severity and details

            if (!String.IsNullOrEmpty(sequenceType))
            {
                if (sequenceType == "2")
                {
                    //' Read task sequence variable for deployment ID value
                    deploymentId = TSEnvironment.GetTSVariable("_SMSTSAdvertID");
                    if (String.IsNullOrEmpty(deploymentId))
                    {
                        deploymentId = null;
                    }

                    //' Read task sequence variable for current step name value
                    stepName = TSEnvironment.GetTSVariable("_SMSTSCurrentActionName");
                    if (String.IsNullOrEmpty(stepName))
                    {
                        stepName = null;
                    }

                    //' Read task sequence variable for current step value
                    currentStep = TSEnvironment.GetTSVariable("_SMSTSNextInstructionPointer");
                    if (String.IsNullOrEmpty(currentStep))
                    {
                        currentStep = null;
                    }

                    //' Read task sequence variable for total steps value
                    totalSteps = TSEnvironment.GetTSVariable("_SMSTSInstructionTableSize");
                    if (String.IsNullOrEmpty(totalSteps))
                    {
                        totalSteps = null;
                    }
                }
            }

            //' Determine details message parameter
            switch (monitoringState)
            {
                case MonitoringSessionState.Running:
                    details = "Task sequence is running";
                    break;
                case MonitoringSessionState.Ending:
                    details = "Computer is being restarted";
                    break;
                case MonitoringSessionState.NotStarted:
                    details = "Task sequence not started";
                    break;
            }

            //' Read task sequence variable for computer name value
            string computerName = TSEnvironment.GetTSVariable("OSDComputerName");
            if (String.IsNullOrEmpty(computerName))
            {
                computerName = "Unknown";
            }

            //' Get current date time for modified time parameter input
            string modifiedTime = DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss.fff");

            //' Add monitoring data through web service call
            AddCMOSDMonitorDataResponse monitorResponse = await webService.AddCMOSDMonitorDataAsync(Properties.Settings.Default.WebServiceSecretKey, uniqueId, computerName, 1, modifiedTime, deploymentId, stepName, currentStep, totalSteps, null, null, details, null, null, null);
        }

        public void SystemEvents_SessionEnding(object sender, SessionEndingEventArgs e)
        {
            MessageBox.Show("Application is being shutdown", "Restarting computer", MessageBoxButton.OK, MessageBoxImage.Information); //' ----- ONLY FOR DEBUG
            AddMonitoringData(MonitoringSessionState.Ending);
        }

        private void ButtonSuspend_Click(object sender, RoutedEventArgs e)
        {
            //' Handle button states
            ButtonResume.IsEnabled = true;
            ButtonSuspend.IsEnabled = false;

            //' Suspend timer
            timer.Stop();
            LabelStatus.Content = "Monitor suspended";
        }

        private void ButtonResume_Click(object sender, RoutedEventArgs e)
        {
            //' Handle button states
            ButtonResume.IsEnabled = false;
            ButtonSuspend.IsEnabled = true;

            //' Enable timer
            timer.Start();
            LabelStatus.Content = "Monitor running";
        }

        private void ButtonClose_Click(object sender, RoutedEventArgs e)
        {
            //' Disable timer and shutdown application
            timer.Stop();
            Application.Current.Shutdown();
        }
    }
}
