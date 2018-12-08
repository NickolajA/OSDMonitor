using Microsoft.Win32;
using OSDMonitor.ConfigMgrWebService;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Xml;

namespace OSDMonitor
{
    public partial class MainWindow : Window
    {
        //' Construct SoapClient for ConfigMgr WebService
        public static ConfigMgrWebServiceSoapClient webService = new ConfigMgrWebServiceSoapClient();

        //' Construct timer
        System.Windows.Forms.Timer timer = null;

        //' Construct variable for task sequence type
        public static string sequenceType = null;

        //' Construct object for the monitoring state
        MonitoringSessionState monitoringState;

        //' Instantiate parameter variables for web service call
        public static string uuid = null;
        public static string computerName = null;
        public static string macAddress = null;
        public static string deploymentId = null;
        public static string modifiedTime = null;
        public static string startTime = null;
        public static string endTime = null;
        public static int severity = 1;
        public static string stepName = null;
        public static string currentStep = null;
        public static string totalSteps = null;
        public static string details = null;
        public static string dartTicket = null;
        public static string dartIp = null;
        public static string dartPort = null;

        //' Construct enum for monitoring state
        public enum MonitoringSessionState {
            Reboot = 1,
            Running = 2,
            NotStarted = 3,
            Failed = 4,
            Completed = 5
        }

        public MainWindow()
        {
            try
            {
                //' Initial logging statement
                WriteLogFile("ConfigMgr OSD Monitor started");

                //' Read command line arguments and determine application run mode
                string[] cmdLineArgs = Environment.GetCommandLineArgs();
                string convertedParameter = string.Empty;
                switch (cmdLineArgs[1])
                {
                    case "OSCOM":
                        convertedParameter = "END";
                        break;
                    case "OSFULL":
                        convertedParameter = "FULLOS";
                        break;
                    case "OSPE":
                        convertedParameter = "WINPE";
                        break;
                }

                WriteLogFile(String.Format("Passed command line argument: {0}", convertedParameter));

                if (cmdLineArgs.Length == 1)
                {
                    WriteLogFile("No command line argument was given, please use either WINPE, FULLOS or END");
                    Application.Current.Shutdown();
                }
                else if (cmdLineArgs.Length == 2)
                {
                    List<string> approvedArguments = new List<string> { "OSFULL", "OSPE", "OSCOM" };
                    string cmdLine = cmdLineArgs[1];
                    if (approvedArguments.Contains(cmdLine))
                    {
                        //' Ensure that task sequence environment can be loaded
                        TSEnvironment.TestTSEnvironment();
                        WriteLogFile("Successfully loaded the Microsoft.SMS.TSEnvironment object");

                        //' Get computer macaddress information
                        macAddress = Computer.GetMacAddress();
                        if (String.IsNullOrEmpty(macAddress))
                        {
                            macAddress = null;
                        }

                        //' Get computer smbios guid information
                        uuid = Computer.GetSMBIOSGUID();
                        if (String.IsNullOrEmpty(uuid))
                        {
                            uuid = null;
                        }

                        try
                        {
                            //' Determine if a task sequence for operating system deployment is running
                            sequenceType = TSEnvironment.GetTSVariable("_SMSTSType");
                            if (sequenceType == "2")
                            {
                                WriteLogFile("Detected that a task sequence is currently running at application startup");
                            }
                            else
                            {
                                WriteLogFile("Detected that no task sequence is currently running at application startup");
                            }

                            WriteLogFile("Successfully determined task sequence operational state");
                        }
                        catch (System.Exception ex)
                        {
                            WriteLogFile(String.Format("An error occurred while reading TSEnvironment variable _SMSTSType at application startup. Error: {0}", ex.Message));
                            Application.Current.Shutdown();
                        }

                        //' Initialize application and show window
                        InitializeComponent();

                        if (convertedParameter == "WINPE" || convertedParameter == "FULLOS")
                        {
                            if (convertedParameter == "WINPE")
                            {
                                try
                                {
                                    //' Gather dart data
                                    if (Properties.Settings.Default.GatherDaRTData == true)
                                    {
                                        bool isWinpe;
                                        Boolean.TryParse(TSEnvironment.GetTSVariable("_SMSTSInWinPE"), out isWinpe);
                                        if (isWinpe == true)
                                        {
                                            string systemDrive = String.Format("{0}\\", Environment.GetEnvironmentVariable("SystemDrive"));
                                            WriteLogFile("Gather DaRT data application setting was enabled, attempting to locate and read DaRT information");
                                            LoadDartData(systemDrive);
                                        }
                                    }
                                }
                                catch (System.Exception ex)
                                {
                                    WriteLogFile(String.Format("An error occurred while reading DaRT information from inv32.xml. Error: {0}", ex.Message));
                                }
                            }

                            //' Handle session ending event from operating system
                            SystemEvents.SessionEnding += new SessionEndingEventHandler(SystemEvents_SessionEnding);
                            WriteLogFile("Successfully configured event handler for restarts of computer");

                            //' Construct new timer to fire accordingly to application settings
                            WriteLogFile(String.Format("Creating a new timer to send monitoring data to web service every '{0}' ms", Properties.Settings.Default.MonitorIntervalMilliseconds));
                            timer = new System.Windows.Forms.Timer
                            {
                                Interval = Properties.Settings.Default.MonitorIntervalMilliseconds
                            };
                            timer.Tick += new EventHandler(Timer_Tick);
                            timer.Start();
                        }
                        else
                        {
                            AddMonitoringDataEnd();
                            WriteLogFile("Successfully posted deployment completed monitoring data to web service, application will now shutdown");
                            Application.Current.Shutdown();
                        }
                    }
                    else
                    {
                        WriteLogFile("Unsupported command line argument detected, please see documentation for supported command line arguments");
                        Application.Current.Shutdown();
                    }
                }
                else
                {
                    WriteLogFile("Unhandled amount of command line arguments passed to application, please see documentation for supported command line arguments");
                    Application.Current.Shutdown();
                }
            }
            catch (System.Exception ex)
            {
                MessageBox.Show("Unable to load Task Sequence environment", "Load failure", MessageBoxButton.OK, MessageBoxImage.Error);
                WriteLogFile(String.Format("Unable to load Microsoft.SMS.TSEnvironment, this application should be executed from within a task sequence environment", ex.Message));
                Application.Current.Shutdown();
            }
        }

        public static void WriteLogFile(string value)
        {
            //' Construct streamwriter for reading and appending to log file
            StreamWriter logFile = new StreamWriter(Path.Combine(Environment.GetEnvironmentVariable("SystemRoot"), "Temp", "OSDMonitor.log"), true);

            //' Write content and close log file
            logFile.WriteLine(value);
            logFile.Close();
        }

        public static string GetTimeNow()
        {
            return DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
        }

        public static void LoadDartData(string root)
        {
            try
            {
                //' Search system root location recursively for dart data file
                WriteLogFile(String.Format("Searching for inv32.xml file recursively in {0}", root));
                string xmlFile = DataLoader.SearchFile(root, "inv32").First();
                WriteLogFile(String.Format("Found XML file located in: {0}", xmlFile));

                try
                {
                    //' Load data file as xml document
                    if (!String.IsNullOrEmpty(xmlFile))
                    {
                        WriteLogFile(String.Format("Attempting to load XML file: {0}", xmlFile));
                        XmlDocument xmlDocument = DataLoader.LoadXMLFile(xmlFile);

                        try
                        {
                            //' Get dart ticket from xml
                            dartTicket = DataLoader.GetXmlNodeAttribute(xmlDocument, "//A", "ID");
                            WriteLogFile(String.Format("Found DaRT ticket number in XML file: {0}", dartTicket));

                            try
                            {
                                //' Get dart ipv4 address from xml
                                Regex regEx = new Regex(@"\b(25[0-5]|2[0-4]\d|1\d\d|[1-9]?\d)(\.(25[0-5]|2[0-4]\d|1\d\d|[1-9]?\d)){3}\b");
                                dartIp = DataLoader.GetXmlNodeAttributes(xmlDocument, "//L", "N").Where(ip => regEx.IsMatch(ip)).First();
                                WriteLogFile(String.Format("Found DaRT IP number in XML file: {0}", dartIp));

                                try
                                {
                                    //' Get dart port from xml
                                    dartPort = DataLoader.GetXmlNodeAttributes(xmlDocument, "//L", "P").Last();
                                    WriteLogFile(String.Format("Found DaRT port number in XML file: {0}", dartPort));
                                }
                                catch (System.Exception ex)
                                {
                                    WriteLogFile(String.Format("An error occurred while searching for DaRT port number in XML file. Error: {0}", ex.Message));
                                }
                            }
                            catch (System.Exception ex)
                            {
                                WriteLogFile(String.Format("An error occurred while searching for DaRT IP number in XML file. Error: {0}", ex.Message));
                            }
                        }
                        catch (System.Exception ex)
                        {
                            WriteLogFile(String.Format("An error occurred while searching for DaRT ticket number in XML file. Error: {0}", ex.Message));
                        }
                    }
                }
                catch (System.Exception ex)
                {
                    WriteLogFile(String.Format("An error occurred while loading '{1}'. Error: {0}", ex.Message, xmlFile));
                }
            }
            catch (System.Exception ex)
            {
                WriteLogFile(String.Format("An error occurred while searching for inv32.xml recursively. Error: {0}", ex.Message));
            }
        }

        public void Timer_Tick(object sender, EventArgs e)
        {
            try
            {
                //' Determine if a task sequence for operating system deployment is running
                if (sequenceType == "2")
                {
                    //' Set monitoring session state
                    monitoringState = MonitoringSessionState.Running;

                    try
                    {
                        //' When deployment has started, set start time variable if not already set
                        if (String.IsNullOrEmpty(TSEnvironment.DeploymentStartTime))
                        {
                            startTime = GetTimeNow();
                            TSEnvironment.DeploymentStartTime = startTime;
                        }
                        else
                        {
                            startTime = TSEnvironment.DeploymentStartTime;
                        }
                    }
                    catch (System.Exception ex)
                    {
                        WriteLogFile(String.Format("An error occurred while reading task sequence environment, this exception happened while reading deployment started variable and was unexpected. Error: {0}", ex.Message));
                    }

                    //' Determine if the last task sequence step succeeded
                    bool lastSuccess;
                    Boolean.TryParse(TSEnvironment.GetTSVariable("_SMSTSLastActionSucceeded"), out lastSuccess);
                    if (lastSuccess == false)
                    {
                        WriteLogFile("Determined that the task sequence has failed, changing monitoring session state");
                        monitoringState = MonitoringSessionState.Failed;
                    }
                }
                else
                {
                    try
                    {
                        sequenceType = TSEnvironment.GetTSVariable("_SMSTSType");
                    }
                    catch (System.Exception ex)
                    {
                        WriteLogFile(String.Format("An warning occurred while reading task sequence environment, this was expected and should not happen again. Error: {0}", ex.Message));
                    }

                    //' Set monitoring session state
                    monitoringState = MonitoringSessionState.NotStarted;
                }

                //' Read task sequence variable for computer name value
                computerName = TSEnvironment.GetTSVariable("OSDComputerName");
                if (String.IsNullOrEmpty(computerName))
                {
                    computerName = "Unknown";
                }

                //' Get current date time for modified time parameter input
                modifiedTime = GetTimeNow();

                //' Add monitoring data
                AddMonitoringData(monitoringState, modifiedTime, computerName, false);
            }
            catch (System.Exception ex)
            {
                WriteLogFile(String.Format("An warning occurred while reading task sequence environment, this exception happened within the timer tick and was unexpected. Error: {0}", ex.Message));
            }
        }

        async public static void AddMonitoringData(MonitoringSessionState monitoringState, string modifiedTime, string computerName, bool monitorEnd)
        {
            WriteLogFile("Monitoring timer was triggered, reading task sequence environment data");

            try
            {
                //' Determine unique id for deployment
                string uniqueId = TSEnvironment.GetTSVariable(Properties.Settings.Default.UniqueMonitoringTSVariableName);
                if (String.IsNullOrEmpty(uniqueId))
                {
                    uniqueId = Guid.NewGuid().ToString();
                    TSEnvironment.SetTSVariable(Properties.Settings.Default.UniqueMonitoringTSVariableName, uniqueId);
                }

                try
                {
                    //' Read additional task sequence information if the environment is reachable
                    bool tsEnvironment = TSEnvironment.TestTSEnvironment();
                    if (tsEnvironment == true)
                    {
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
                    }
                }
                catch (System.Exception ex)
                {
                    WriteLogFile(String.Format("An warning occurred while reading additional task sequence data, this exception happened within the add monitoring method and was unexpected. Error: {0}", ex.Message));
                }

                //' Determine details message parameter
                switch (monitoringState)
                {
                    case MonitoringSessionState.Running:
                        details = "Task sequence is running";
                        severity = 1;
                        break;
                    case MonitoringSessionState.Reboot:
                        details = "Computer is being restarted";
                        severity = 1;
                        break;
                    case MonitoringSessionState.NotStarted:
                        details = "Task sequence has not started";
                        severity = 1;
                        break;
                    case MonitoringSessionState.Failed:
                        details = "Deployment failed";
                        severity = 2;
                        break;
                    case MonitoringSessionState.Completed:
                        details = "Deployment completed";
                        severity = 1;
                        break;
                    default:
                        severity = 1;
                        break;
                }

                //' Determine if end time should be set
                if (monitorEnd == true)
                {
                    endTime = GetTimeNow();
                }

                try
                {
                    //' Add monitoring data through web service call
                    AddCMOSDMonitorDataResponse monitorResponse = await webService.AddCMOSDMonitorDataAsync(Properties.Settings.Default.WebServiceSecretKey, uniqueId, computerName, uuid, macAddress, severity, modifiedTime, deploymentId, stepName, currentStep, totalSteps, startTime, endTime, details, dartIp, dartPort, dartTicket);
                    WriteLogFile("Successfully posted monitoring data to web service");
                }
                catch (System.Exception ex)
                {
                    WriteLogFile(String.Format("An error occurred while attempting to post monitoring data to web service. Error: {0}", ex.Message));
                }
            }
            catch (System.Exception ex)
            {
                WriteLogFile(String.Format("An error occurred while collecting data for web service post. Will be retried in the next cycle. Error: {0}", ex.Message));
            }
        }

        public void AddMonitoringDataEnd()
        {
            //' Stop the timer currently running
            timer.Stop();

            //' Get current date time for modified time parameter input
            modifiedTime = GetTimeNow();

            //' Post a task sequence restarting monitor update
            AddMonitoringData(MonitoringSessionState.Completed, modifiedTime, computerName, true);
        }

        public void SystemEvents_SessionEnding(object sender, SessionEndingEventArgs e)
        {
            //' Stop the timer currently running
            timer.Stop();

            //' Get current date time for modified time parameter input
            modifiedTime = GetTimeNow();

            //' Post a task sequence restarting monitor update
            AddMonitoringData(MonitoringSessionState.Reboot, modifiedTime, computerName, false);
        }

        private void ButtonSuspend_Click(object sender, RoutedEventArgs e)
        {
            //' Handle button states
            ButtonResume.IsEnabled = true;
            ButtonSuspend.IsEnabled = false;

            //' Suspend timer
            timer.Stop();

            //' Handle UI elements
            ProgressBar.IsIndeterminate = false;
            LabelStatus.Content = "Monitor suspended";

            WriteLogFile("Successfully suspended monitoring data process");
        }

        private void ButtonResume_Click(object sender, RoutedEventArgs e)
        {
            //' Handle button states
            ButtonResume.IsEnabled = false;
            ButtonSuspend.IsEnabled = true;

            //' Enable timer
            timer.Start();

            //' Handle UI elements
            ProgressBar.IsIndeterminate = true;
            LabelStatus.Content = "Monitor running";

            WriteLogFile("Successfully resumed monitoring data process");
        }

        private void ButtonClose_Click(object sender, RoutedEventArgs e)
        {
            //' Disable timer and shutdown application
            timer.Stop();
            WriteLogFile("Successfully ended monitoring data process caused by application shutdown");
            Application.Current.Shutdown();
        }
    }
}
