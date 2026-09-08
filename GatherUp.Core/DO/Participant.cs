using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Xml.Serialization;

namespace GatherUp.Core.DO
{
    public class Participant : Person
    {
        [SetsRequiredMembers]
        public Participant() { }

        public bool?   IsAttending        { get; set; }
        public bool    HasPaid            { get; set; }
        public decimal AmountContributed  { get; set; }

        public List<MailingPreference> MailingPreferences { get; set; } = new();
        public List<EventPayment>      EventPayments      { get; set; } = new();

        public bool    HasPaidForEvent(int eventId)   => EventPayments.Any(p => p.EventId == eventId && p.Amount > 0);
        public decimal GetAmountForEvent(int eventId) => EventPayments.Where(p => p.EventId == eventId).Sum(p => p.Amount);
    }
}
