using System;
using System.IO;
using System.Xml.Serialization;

namespace WattleScript.HardwireGen
{
    [XmlRoot("WattleScript")]
    public class ExtraClassListXml
    {
        [XmlElement]
        public string[] ExtraType = Array.Empty<string>();
        private static XmlSerializer cl = new XmlSerializer(typeof(ExtraClassListXml));
        public static ExtraClassListXml? Get(string file)
        {
            string? text;
            if ((text = ReadAllText(file)) != null)
            {
                try
                {
                    return (ExtraClassListXml) cl.Deserialize(new StringReader(text));
                }
                catch (Exception)
                {
                    return null;
                }
            }
            return null;
        }

        static string? ReadAllText(string file)
        {
            try
            {
                using (var reader = new StreamReader(file))
                {
                    char[] buffer = new char[256];
                    var c = reader.ReadBlock(buffer, 0, buffer.Length);
                    if (c == -1) return null;
                    var str = new string(buffer, 0, c);
                    if (str.TrimStart().StartsWith("<"))
                    {
                        return str + reader.ReadToEnd();
                    }
                }
                return null;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}