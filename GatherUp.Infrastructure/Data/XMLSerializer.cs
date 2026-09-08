using System.IO;
using System.Xml.Serialization;

namespace GatherUp.Infrastructure.Data
{
    public static class XMLSerializer
    {
        public static string SerializeToString<T>(T data) where T : class, new()
        {
            using var writer = new StringWriter();
            new XmlSerializer(typeof(T)).Serialize(writer, data);
            return writer.ToString();
        }

        public static T? DeserializeFromString<T>(string xml) where T : class, new()
        {
            if (string.IsNullOrWhiteSpace(xml)) return null;
            using var reader = new StringReader(xml);
            return new XmlSerializer(typeof(T)).Deserialize(reader) as T;
        }
    }
}
