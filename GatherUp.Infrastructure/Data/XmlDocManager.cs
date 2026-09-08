using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace GatherUp.Infrastructure.Data
{
    public static class XmlDocManager
    {
        public static async Task<XDocument> LoadAsync(string filePath, string rootElementName = "ArrayOfReceipt")
        {
            if (!File.Exists(filePath))
                return new XDocument(new XElement(rootElementName));

            string xml = await File.ReadAllTextAsync(filePath);
            return XDocument.Parse(xml);
        }

        public static async Task SaveAsync(string filePath, XDocument doc)
        {
            await File.WriteAllTextAsync(filePath, doc.ToString());
        }

        public static XElement? FindById(XDocument doc, int id)
        {
            return doc.Root?.Elements()
                .FirstOrDefault(el =>
                {
                    var attr = el.Attribute("id");
                    return attr != null && int.TryParse(attr.Value, out int v) && v == id;
                });
        }

        public static XElement? FindByChildId(XDocument doc, int id)
        {
            return doc.Root?.Elements()
                .FirstOrDefault(el =>
                {
                    var child = el.Element("Id");
                    return child != null && int.TryParse(child.Value, out int v) && v == id;
                });
        }

        public static void AddElement(XDocument doc, XElement element)
        {
            doc.Root!.Add(element);
        }

        public static bool ReplaceById(XDocument doc, int id, XElement newElement)
        {
            var existing = FindById(doc, id);
            if (existing == null) return false;
            existing.ReplaceWith(newElement);
            return true;
        }

        public static bool DeleteById(XDocument doc, int id)
        {
            var existing = FindById(doc, id);
            if (existing == null) return false;
            existing.Remove();
            return true;
        }

        public static string GetString(XElement el, string childName)
            => el.Element(childName)?.Value ?? string.Empty;

        public static int GetInt(XElement el, string childName)
            => int.TryParse(el.Element(childName)?.Value, out int v) ? v : 0;

        public static decimal GetDecimal(XElement el, string childName)
            => decimal.TryParse(el.Element(childName)?.Value,
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out decimal v) ? v : 0m;

        public static System.DateTime GetDateTime(XElement el, string childName)
            => System.DateTime.TryParse(el.Element(childName)?.Value, out System.DateTime v) ? v : System.DateTime.MinValue;
    }
}
