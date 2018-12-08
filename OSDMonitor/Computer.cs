using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Text;
using System.Threading.Tasks;

namespace OSDMonitor
{
    class Computer
    {
        public static ManagementObjectSearcher InvokeWmiSearcher(string selectQuery, string nameSpace)
        {
            // Construct a local WMI management scope
            SelectQuery query = new SelectQuery(selectQuery);
            ManagementScope managementScope = new ManagementScope(nameSpace);

            //' Connect to WMI namespace
            managementScope.Connect();

            //' Construct a management searcher object
            ManagementObjectSearcher managementObjectSearcher = new ManagementObjectSearcher(managementScope, query);

            return managementObjectSearcher;
        }

        public static string GetSMBIOSGUID()
        {
            string uuid = string.Empty;

            // Construct management scope with query and namespace params
            ManagementObjectSearcher searcher = InvokeWmiSearcher("SELECT * FROM Win32_ComputerSystemProduct", "root\\cimv2");

            //' Enumerate all instances
            foreach (ManagementObject instance in searcher.Get())
            {
                uuid = (string)instance.GetPropertyValue("UUID");
            }

            return uuid;
        }

        public static string GetMacAddress()
        {
            string macAddress = string.Empty;

            // Construct management scope with query and namespace params
            ManagementObjectSearcher searcher = InvokeWmiSearcher("SELECT * FROM Win32_NetworkAdapterConfiguration", "root\\cimv2");

            //' Enumerate each adapter and check for IPEnabled
            foreach (ManagementObject instance in searcher.Get())
            {
                if ((bool)instance.GetPropertyValue("IPEnabled") == true)
                {
                    macAddress = (string)instance.GetPropertyValue("MACAddress");
                }
            }
            
            return macAddress;
        }
    }
}
