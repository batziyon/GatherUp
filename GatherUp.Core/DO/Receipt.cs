using System;
using System.Xml.Serialization;
using GatherUp.Core.Interfaces;

namespace GatherUp.Core.DO
{
    public sealed record Receipt : IEntity
    {
        [XmlAttribute("id")]
        public int      Id            { get; set; }
        public int      VendorId      { get; set; }
        public string   ReceiptNumber { get; set; } = string.Empty;
        public string   FilePath      { get; set; } = string.Empty;
        public decimal  Amount        { get; set; }
        public DateTime Date          { get; set; }
    }
}
