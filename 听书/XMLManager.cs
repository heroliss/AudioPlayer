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
    public class XMLManager
    {
        XDocument xmlDocument;
        string xmlFileName;
        XElement root;
        public List<KeyValuePair<string, long>> timeDataList;
        public XMLManager(string xmlFileName)
        {
            this.xmlFileName = xmlFileName;
            timeDataList = new List<KeyValuePair<string, long>>();
            try
            {
                FileStream file = new FileStream(xmlFileName, FileMode.Open);
                xmlDocument = XDocument.Load(file);
                root = xmlDocument.Root;
                if (root.Name != "AudioPlayer1.0")
                {
                    throw new XmlException();
                }
                foreach (XElement item in root.Elements("File"))
                {
                    timeDataList.Add(new KeyValuePair<string, long>(
                        item.Attribute("FullFileName").Value, long.Parse(item.Value)));
                }
                file.Close();
            }
            catch { }

            xmlDocument = new XDocument();
            root = new XElement("AudioPlayer1.0");
            xmlDocument.Add(root);//创建一个根节点
        }

        public void addItem(string fullFileName,long position)
        {
            root.Add(new XElement("File"
                ,new XAttribute("FullFileName",fullFileName)
                ,new XText(position.ToString())
                ));
        }

        public void save()
        {
            xmlDocument.Save(xmlFileName);
        }

    }
}
