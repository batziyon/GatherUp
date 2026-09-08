using System.Xml.Serialization;

namespace GatherUp.Core.DO
{
    public class EventPayment
    {
        [XmlAttribute("eventId")]
        public int     EventId { get; set; }

        [XmlAttribute("amount")]
        public decimal Amount  { get; set; }
    }
}
