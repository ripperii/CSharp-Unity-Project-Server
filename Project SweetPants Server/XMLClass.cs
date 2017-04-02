using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;

namespace Project_SweetPants_Server
{
    class XMLClass
    {
        
        public static string MakeXML(List<List<string>> str, List<string> fields)
        {
            string XML = "";
            for (int i = 0; i < str.Count; i++)
            {
                XML += "<row ";
                for(int j = 0; j < str[i].Count; j++)
                {
                    XML += fields[j] + " = \"" + str[i][j] + "\"";
                }
                XML += "></row>";
            }
            return XML;
        }
        public static byte[] XMLtoBytes(string xml)
        {
            byte[] bytes = new byte[xml.Length * sizeof(char)];
            Buffer.BlockCopy(xml.ToCharArray(), 0, bytes, 0, bytes.Length);
            return bytes;
        }
        public static string bytesToXML(byte[] bytes)
        {
            char[] chars = new char[bytes.Length / sizeof(char)];
            Buffer.BlockCopy(bytes, 0, chars, 0, bytes.Length);
            return new string(chars);
        }
        public static List<XMLentry> readXML(string xml)
        {
            
            List<XMLentry> list = new List<XMLentry>();
            
            XmlReader reader = XmlReader.Create(new StringReader(xml));

            while(reader.Read())
            {
                
                if (reader.IsStartElement())
                {
                    string n = "";
                    List<XMLAttribute> a = new List<XMLAttribute>();
                    n = reader.Name;
                    if (reader.HasAttributes)
                    {
                        while (reader.MoveToNextAttribute())
                        {
                            a.Add(new XMLAttribute(reader.Name, reader.Value));
                        }
                    }
                    list.Add(new XMLentry(n, a));  
                }
                else if(reader.NodeType == XmlNodeType.Text)
                {
                    list[list.Count - 1].Value = reader.Value;
                }
               
            }

            return list;
        }
    }
    public class XMLentry
    {
        public string Name;
        public string Value;
        public List<XMLAttribute> Attributes;

        public XMLentry(string name, List<XMLAttribute> attributes)
        {
            Name = name;
            Attributes = attributes;
        }
    }
    public class XMLAttribute
    {
        public string Name;
        public string Value;

        public XMLAttribute(string name, string value)
        {
            Name = name;
            Value = value;
        }
    }
}
