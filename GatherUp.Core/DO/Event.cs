using System;
using System.Collections.Generic;
using System.Xml.Serialization;
using GatherUp.Core.Interfaces;

namespace GatherUp.Core.DO
{
    public class Event : IEntity
    {
        [XmlAttribute("id")]
        public int Id { get; set; }

        public string    Name                    { get; set; } = string.Empty;
        public DateTime  Date                    { get; set; }
        public string?   Location                { get; set; }
        public decimal   PricePerParticipant     { get; set; }
        public int       EventManagerId          { get; set; }
        public int       EventHostId             { get; set; }
        public int?      PreliminaryPollId       { get; set; }
        public string?   InvitationMessage       { get; set; }
        public string?   PaymentDetails          { get; set; }
        public DateTime? HostInvitationScheduledAt { get; set; }
        public bool      HostInvitationSent      { get; set; }
        public List<int> ParticipantIds          { get; set; } = new();
        public List<int> VendorIds               { get; set; } = new();
        public List<int> PollIds                 { get; set; } = new();
        public EventStatus Status                { get; set; } = EventStatus.Draft;
    }

    public enum EventStatus
    {
        Draft           = 0,
        Planning        = 1,
        InvitationsSent = 2,
        Finalized       = 3
    }
}
