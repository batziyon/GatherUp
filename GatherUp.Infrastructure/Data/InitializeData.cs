using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GatherUp.Core.DO;
using GatherUp.Core.Interfaces;

namespace GatherUp.Infrastructure.Data
{
    public static class InitializeData
    {
        public static async Task InitializeAsync(
            IRepository<Event>            eventRepo,
            IRepository<Participant>      participantRepo,
            IRepository<VendorAllocation> vendorRepo,
            IRepository<Poll>             pollRepo,
            IRepository<EventManager>     managerRepo,
            IRepository<EventHost>        hostRepo)
        {
            var manager1 = new EventManager { Id = 1, Name = "מיכל כהן",  Email = "michal.cohen.dev@gmail.com",   Password = "123456" };
            var manager2 = new EventManager { Id = 2, Name = "שרה לוי",   Email = "sarah.levi.manager@gmail.com", Password = "123456" };
            await managerRepo.AddAsync(manager1);
            await managerRepo.AddAsync(manager2);

            var host1 = new EventHost { Id = 10, Name = "רחל מזרחי",  Email = "rachel.host@gmail.com" };
            var host2 = new EventHost { Id = 11, Name = "יוסי אברהם", Email = "yosi.host@gmail.com" };
            await hostRepo.AddAsync(host1);
            await hostRepo.AddAsync(host2);

            var p1 = new Participant
            {
                Id = 101, Name = "מיכל כהן", Email = "michal.cohen.dev@gmail.com", Password = "123456",
                IsAttending = true, HasPaid = true, AmountContributed = 200,
                MailingPreferences = new List<MailingPreference> { MailingPreference.EventChanges, MailingPreference.PollCreated },
                EventPayments = new List<EventPayment> { new EventPayment { EventId = 500, Amount = 200 } }
            };
            var p2 = new Participant
            {
                Id = 102, Name = "אביגיל לוי", Email = "avigail@gmail.com", Password = "123456",
                IsAttending = null, HasPaid = false, AmountContributed = 0,
                MailingPreferences = new List<MailingPreference> { MailingPreference.PollCreated, MailingPreference.EventChanges }
            };
            var p3 = new Participant
            {
                Id = 103, Name = "דוד ישראלי", Email = "david.israeli@gmail.com", Password = "123456",
                IsAttending = true, HasPaid = true, AmountContributed = 200,
                MailingPreferences = new List<MailingPreference> { MailingPreference.EventChanges },
                EventPayments = new List<EventPayment> { new EventPayment { EventId = 500, Amount = 200 } }
            };
            await participantRepo.AddAsync(p1);
            await participantRepo.AddAsync(p2);
            await participantRepo.AddAsync(p3);

            var vendor1 = new VendorAllocation { Id = 1, Name = "קייטרינג אסאדו", AmountOwed = 4500, HasReceipt = false };
            var vendor2 = new VendorAllocation { Id = 2, Name = "צלם מקצועי",     AmountOwed = 2000, HasReceipt = false };
            await vendorRepo.AddAsync(vendor1);
            await vendorRepo.AddAsync(vendor2);

            var poll1 = new Poll
            {
                Id = 1, Code = "PRE500A", Name = "שאלון מיקום ותאריך",
                Description = "בחר/י מיקום ותאריך", IsPreliminary = true,
                ClosingDate = DateTime.Now.AddDays(7),
                Questions = new List<PollQuestion>
                {
                    new PollQuestion { Id = 1, QuestionText = "באיזה תאריך?", Options = new List<string> { "01/08", "15/08", "01/09" } },
                    new PollQuestion { Id = 2, QuestionText = "איזה מיקום?",  Options = new List<string> { "ירושלים", "תל אביב", "חיפה" } }
                }
            };
            var poll2 = new Poll
            {
                Id = 2, Code = "POLL500B", Name = "סקר כיבוד",
                Description = "בחר/י סגנון כיבוד", IsPreliminary = false,
                Questions = new List<PollQuestion>
                {
                    new PollQuestion { Id = 3, QuestionText = "איזה כיבוד?", Options = new List<string> { "בשרי", "חלבי", "טבעוני" } }
                }
            };
            var poll3 = new Poll
            {
                Id = 3, Code = "PRE501A", Name = "שאלון תחילתי - מסיבת יום הולדת",
                Description = "פרטי המסיבה", IsPreliminary = true,
                ClosingDate = DateTime.Now.AddDays(5),
                Questions = new List<PollQuestion>
                {
                    new PollQuestion { Id = 4, QuestionText = "נושא המסיבה?",   Options = new List<string> { "ספייס", "ים", "פרחים" } },
                    new PollQuestion { Id = 5, QuestionText = "מספר מוזמנים?", Options = new List<string> { "עד 20", "20-50", "50+" } }
                }
            };
            await pollRepo.AddAsync(poll1);
            await pollRepo.AddAsync(poll2);
            await pollRepo.AddAsync(poll3);

            await eventRepo.AddAsync(new Event
            {
                Id = 500, Name = "שבת גיבוש משפחתית 2025",
                Date = DateTime.Now.AddDays(30), Location = "ירושלים",
                PricePerParticipant = 200, EventManagerId = manager1.Id, EventHostId = host1.Id,
                PreliminaryPollId = poll1.Id,
                InvitationMessage = "אנו שמחים להזמין אותך לשבת גיבוש משפחתית מיוחדת!",
                PaymentDetails    = "העברה בנקאית: בנק הפועלים, סניף 12, חשבון 345678",
                ParticipantIds = new List<int> { p1.Id, p2.Id, p3.Id },
                VendorIds      = new List<int> { vendor1.Id },
                PollIds        = new List<int> { poll1.Id, poll2.Id }
            });

            await eventRepo.AddAsync(new Event
            {
                Id = 501, Name = "מסיבת יום הולדת לרחל",
                Date = DateTime.Now.AddDays(60), Location = "תל אביב",
                PricePerParticipant = 150, EventManagerId = manager2.Id, EventHostId = host2.Id,
                PreliminaryPollId = poll3.Id,
                InvitationMessage = "מוזמן/ת למסיבת יום הולדת מפתיעה!",
                PaymentDetails    = "מזומן אצל שרה לוי",
                ParticipantIds = new List<int> { p1.Id, p2.Id },
                VendorIds      = new List<int> { vendor2.Id },
                PollIds        = new List<int> { poll3.Id }
            });
        }
    }
}
