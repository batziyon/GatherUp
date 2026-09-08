using System;

namespace GatherUp.Core.DO
{
    public class EmailLogEntry
    {
        public DateTime SentAt  { get; set; }
        public string   ToEmail { get; set; } = string.Empty;
        public string   Subject { get; set; } = string.Empty;
        public string   Body    { get; set; } = string.Empty;
        public int?     EventId { get; set; }
    }
}
