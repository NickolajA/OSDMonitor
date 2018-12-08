using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OSDMonitor
{
    class TSEnvironment
    {
        //' Construct property to get or set the start time of the deployment
        public static string DeploymentStartTime
        {
            get
            {
                return GetTSVariable("OSDStartTime");
            }
            set
            {
                SetTSVariable("OSDStartTime", value);
            }
        }

        public static string GetTSVariable(string varName)
        {
            //' Construct return value object
            string returnValue = string.Empty;

            try
            {
                //' Initiate variable for COM object
                dynamic comObject;

                //' Load TS environment
                Type tsEnvironment = Type.GetTypeFromProgID("Microsoft.SMS.TSEnvironment");
                comObject = Activator.CreateInstance(tsEnvironment);

                //' Read task sequence variable value
                returnValue = comObject.Value[varName];

                //' Cleanup COM object
                if (System.Runtime.InteropServices.Marshal.IsComObject(comObject) == true)
                    System.Runtime.InteropServices.Marshal.ReleaseComObject(comObject);
            }
            catch (System.Exception ex)
            {
                throw new Exception(String.Format("Failed to retrieve TSEnvironment variable: {0}", ex.Message));
            }

            return returnValue;
        }

        public static bool SetTSVariable(string varName, string value)
        {
            //' Construct return value object
            bool returnValue = false;

            try
            {
                //' Initiate variable for COM object
                dynamic comObject;

                //' Load TS environment
                Type tsEnvironment = Type.GetTypeFromProgID("Microsoft.SMS.TSEnvironment");
                comObject = Activator.CreateInstance(tsEnvironment);

                //' Read task sequence variable value
                returnValue = comObject.Value[varName] = value;

                //' Cleanup COM object
                if (System.Runtime.InteropServices.Marshal.IsComObject(comObject) == true)
                    System.Runtime.InteropServices.Marshal.ReleaseComObject(comObject);

                returnValue = true;            
            }
            catch (System.Exception ex)
            {
                returnValue = false;
            }

            return returnValue;
        }

        public static bool TestTSEnvironment()
        {
            //' Construct variable for return value
            bool returnValue = false;

            try
            {
                //' Initiate variable for COM object
                dynamic comObject;

                //' Load TS environment
                Type tsEnvironment = Type.GetTypeFromProgID("Microsoft.SMS.TSEnvironment");
                comObject = Activator.CreateInstance(tsEnvironment);

                //' Check if COM object is present
                if (System.Runtime.InteropServices.Marshal.IsComObject(comObject) == true)
                    returnValue = true;

                //' Cleanup COM object
                if (System.Runtime.InteropServices.Marshal.IsComObject(comObject) == true)
                    System.Runtime.InteropServices.Marshal.ReleaseComObject(comObject);
            }
            catch (System.Exception ex)
            {
                throw new Exception(String.Format("{0}", ex.Message));
            }

            return returnValue;
        }
    }
}
