using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace OSDMonitor
{
    class DataLoader
    {
        public static XmlDocument LoadXMLFile(string path)
        {
            //' Construct XML document and load file from path
            XmlDocument xmlDocument = new XmlDocument();
            xmlDocument.Load(path);

            return xmlDocument;
        }

        public static string GetXmlNodeAttribute(XmlDocument xmlDocument, string pattern, string name)
        {
            //' Construct variable for return value
            string attribute = string.Empty;

            //' Select xml node and read attribute value
            XmlNode xmlNode = xmlDocument.SelectSingleNode(pattern);

            if (xmlNode != null)
            {
                attribute = xmlNode.Attributes[name].Value;
            }

            return attribute;
        }

        public static List<string> GetXmlNodeAttributes(XmlDocument xmlDocument, string pattern, string name)
        {
            //' Construct variable for return value
            List<string> attributeList = new List<string>();

            //' Select xml nodes and read attribute values
            XmlNodeList xmlNodes = xmlDocument.SelectNodes(pattern);

            if (xmlNodes != null)
            {
                if (xmlNodes.Count >= 1)
                {
                    foreach (XmlNode xmlNode in xmlNodes)
                    {
                        attributeList.Add(xmlNode.Attributes[name].Value);
                    }
                }
            }

            return attributeList;
        }

        public static IEnumerable<string> SearchFile(string root, string searchTerm)
        {
            //' Construct list of files
            List<string> fileList = new List<string>();

            //' Search current root files for file matching search term
            foreach (string file in Directory.EnumerateFiles(root).Where(f => f.Contains(searchTerm)))
            {
                fileList.Add(file);
            }

            foreach (var subFolder in Directory.EnumerateDirectories(root))
            {
                try
                {
                    if (fileList.Count == 0)
                    {
                        fileList.AddRange(SearchFile(subFolder, searchTerm));
                    }
                }
                catch (System.Exception ex)
                {
                    throw new Exception(ex.Message);
                }
            }

            return fileList;
        }
    }
}
