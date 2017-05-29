using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;

namespace AudioPlayer
{
    public class XMLDataManager
    {
        public string XmlVersion { get => "AudioPlayerXML_2.0"; }

        private string xmlFileName; //载入和读取用此同一个文件名

        public XMLDataManager(string xmlFileName)
        {
            this.xmlFileName = xmlFileName;
        }

        public void save(Dictionary<string, string> globalData, List<Dictionary<string, string>> xmlData)
        {
            XDocument xmlDoc = new XDocument();
            XElement root = new XElement(XmlVersion);
            xmlDoc.Add(root);

            XElement globalElement = new XElement("Global");

            foreach (KeyValuePair<string, string> attribute in globalData)
            {
                globalElement.Add(new XAttribute(attribute.Key, attribute.Value));
            }
            root.Add(globalElement);

            foreach (Dictionary<string, string> item in xmlData)
            {
                XElement xmlItem = new XElement("Item");
                foreach (KeyValuePair<string, string> oneData in item)
                {
                    xmlItem.Add(new XAttribute(oneData.Key, oneData.Value));
                }
                root.Add(xmlItem);
            }
            xmlDoc.Save(xmlFileName);
        }

        public void loadData(out Dictionary<string, string> globalData, out List<Dictionary<string, string>> itemsData)
        {
            globalData = new Dictionary<string, string>();
            itemsData = new List<Dictionary<string, string>>();
            using (FileStream file = new FileStream(xmlFileName, FileMode.Open))
            {
                XDocument xmlDoc = XDocument.Load(file);
                XElement root = xmlDoc.Root;
                if (root.Name.LocalName != XmlVersion)
                {
                    throw new ApplicationException("XML文件版本不匹配，需要的版本为：" + XmlVersion);
                }

                foreach (XAttribute attribute in root.Element("Global").Attributes())
                {
                    globalData.Add(attribute.Name.LocalName, attribute.Value);
                }
                
                foreach (XElement ele in root.Elements("Item"))
                {
                    Dictionary<string, string> itemData = new Dictionary<string, string>();
                    foreach (XAttribute attribute in ele.Attributes())
                    {
                        itemData.Add(attribute.Name.LocalName, attribute.Value);
                    }
                    itemsData.Add(itemData);
                }
            }
        }
    }
}
