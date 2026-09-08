using System;
using System.Collections.Generic;
using System.Xml.Serialization;
using GatherUp.Core.Interfaces;

namespace GatherUp.Core.DO
{
    public class Poll : IEntity
    {
        [XmlAttribute("id")]
        public int Id { get; set; }

        public string    Code         { get; set; } = string.Empty;
        public string    Name         { get; set; } = string.Empty;
        public string?   Description  { get; set; }
        public DateTime? ClosingDate  { get; set; }
        public bool      IsPreliminary { get; set; }

        public List<PollQuestion> Questions { get; set; } = new();
    }
}
