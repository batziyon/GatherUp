using System;
using System.IO;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using GatherUp.BL;
using GatherUp.Core.DO;
using GatherUp.Core.Interfaces;
using GatherUp.Infrastructure.Data;

namespace GatherUp.Tests
{
    class Program
    {
        private static List<string> TestResults = new();
        private static int PassedTests  = 0;
        private static int FailedTests  = 0;

        record Repos(
            IRepository<Event>            Events,
            IRepository<Participant>      Participants,
            IRepository<VendorAllocation> Vendors,
            IRepository<Poll>             Polls,
            IRepository<EventManager>     Managers,
            IRepository<EventHost>        Hosts,
            IRepository<Receipt>          Receipts
        );

        static Repos CreateXmlRepositories(string xmlFolder, string receiptsFolder)
        {
            return new Repos(
                Events:       new XmlRepository<Event>(xmlFolder),
                Participants: new XmlRepository<Participant>(xmlFolder),
                Vendors:      new XmlRepository<VendorAllocation>(xmlFolder),
                Polls:        new XmlRepository<Poll>(xmlFolder),
                Managers:     new XmlRepository<EventManager>(xmlFolder),
                Hosts:        new XmlRepository<EventHost>(xmlFolder),
                Receipts:     new ReceiptRepository(xmlFolder, receiptsFolder)
            );
        }

        static Repos CreateMemoryRepositories(string receiptsFolder)
        {
            return new Repos(
                Events:       new MemoryRepository<Event>(),
                Participants: new MemoryRepository<Participant>(),
                Vendors:      new MemoryRepository<VendorAllocation>(),
                Polls:        new MemoryRepository<Poll>(),
                Managers:     new MemoryRepository<EventManager>(),
                Hosts:        new MemoryRepository<EventHost>(),
                Receipts:     new MemoryRepository<Receipt>()
            );
        }

        static async Task InitWithXml(Repos repos)
        {
            await InitializeData.InitializeAsync(
                repos.Events, repos.Participants, repos.Vendors,
                repos.Polls,  repos.Managers,     repos.Hosts);
        }

        static async Task InitWithMemory(Repos repos)
        {
            await InitializeData.InitializeAsync(
                repos.Events, repos.Participants, repos.Vendors,
                repos.Polls,  repos.Managers,     repos.Hosts);
        }

        static async Task Main(string[] args)
        {
            string baseDir        = AppDomain.CurrentDomain.BaseDirectory;
            string xmlFolder      = Path.Combine(baseDir, "XMLData");
            string receiptsFolder = Path.Combine(baseDir, "ReceiptsStorage");
            string emailsFolder   = Path.Combine(baseDir, "EmailsLog");

            var repos = CreateXmlRepositories(xmlFolder, receiptsFolder);

            if (!File.Exists(Path.Combine(xmlFolder, "Events.xml")))
                await InitWithXml(repos);

            var emailService = new FileEmailService(emailsFolder);

            var notifier           = new EventNotifierService(repos.Participants, repos.Managers, repos.Events, emailService);
            var participantService = new ParticipantService(repos.Participants, repos.Events, emailService, notifier);
            var financeService     = new FinanceService(repos.Participants, repos.Vendors, repos.Receipts, repos.Events, emailService, notifier);
            var pollService        = new PollService(repos.Polls, repos.Events, repos.Participants, emailService, notifier);
            var eventService       = new EventService(repos.Events, repos.Managers, repos.Hosts, repos.Participants, emailService);
            var personService      = new PersonService(repos.Participants, repos.Managers, repos.Hosts);

            try
            {
                Print("STEP 1: Create Event");
                var newEvent = new Event
                {
                    Id                  = 999,
                    Name                = "E2E Test Event",
                    Date                = DateTime.Now.AddDays(30),
                    Location            = "Test Location",
                    PricePerParticipant = 150,
                    EventManagerId      = 100,
                    EventHostId         = 10,
                    Status              = EventStatus.Planning
                };
                await repos.Events.AddAsync(newEvent);
                var createdEvent = await repos.Events.GetByIdAsync(999);
                Assert(createdEvent != null,                              "Event created successfully",      "Event creation failed");
                Assert(createdEvent?.Name   == "E2E Test Event",         "Event name matches",              "Event name mismatch");
                Assert(createdEvent?.Status == EventStatus.Planning,     "Event status is Planning",        "Event status incorrect");
                Console.WriteLine();

                Print("STEP 2: Create Poll");
                var poll = await pollService.CreatePollAsync(999, "Test Poll",
                    new List<(string, List<string>)>
                    {
                        ("Location?", new List<string> { "Option A", "Option B" }),
                        ("Time?",     new List<string> { "10:00",    "15:00"    })
                    });
                Assert(poll != null,                       "Poll created successfully",   "Poll creation failed");
                Assert((poll?.Questions.Count ?? 0) == 2, "Poll has 2 questions",        "Poll question count incorrect");
                var eventWithPoll = await repos.Events.GetByIdAsync(999);
                Assert(eventWithPoll?.PollIds.Contains(poll!.Id) ?? false, "Poll linked to event", "Poll event linkage failed");
                Console.WriteLine();

                Print("STEP 3: Add Participants");
                var p1 = new Participant { Id = 2001, Name = "Test User 1", Email = "test1@example.com",
                    MailingPreferences = new List<MailingPreference> { MailingPreference.EventChanges } };
                var p2 = new Participant { Id = 2002, Name = "Test User 2", Email = "test2@example.com",
                    MailingPreferences = new List<MailingPreference> { MailingPreference.PollCreated  } };
                var p3extra = new Participant { Id = 2004, Name = "Extra User 3", Email = "extra3@example.com",
                    MailingPreferences = new List<MailingPreference> { MailingPreference.EventChanges } };

                await participantService.AddParticipantToEventAsync(999, p1);
                await participantService.AddParticipantToEventAsync(999, p2);
                await participantService.AddParticipantToEventAsync(999, p3extra);

                var evt = await repos.Events.GetByIdAsync(999);
                Assert(evt?.ParticipantIds.Contains(2001) ?? false, "Participant 1 added to event", "Participant 1 not in event");
                Assert(evt?.ParticipantIds.Contains(2002) ?? false, "Participant 2 added to event", "Participant 2 not in event");
                Assert(evt?.ParticipantIds.Contains(2004) ?? false, "Participant 3 added to event", "Participant 3 not in event");
                Console.WriteLine();

                Print("STEP 4: Submit & Change Poll Responses");
                await pollService.SubmitVoteAsync(poll!.Id, 1, 2001, "Option A");
                await pollService.SubmitVoteAsync(poll!.Id, 2, 2001, "10:00");
                await pollService.SubmitVoteAsync(poll!.Id, 1, 2002, "Option B");
                await pollService.SubmitVoteAsync(poll!.Id, 2, 2002, "15:00");
                await pollService.SubmitVoteAsync(poll!.Id, 1, 2001, "Option B");
                var pollResults = await pollService.GetPollResultsAsync(poll!.Id);
                Assert(pollResults != null, "Poll results retrieved", "Poll results retrieval failed");
                Assert((pollResults?.QuestionResults.Count() ?? 0) == 2, "Poll has results for 2 questions", "Poll results count incorrect");
                var q1Result   = pollResults!.QuestionResults.First();
                var optBCount  = q1Result.OptionStats.FirstOrDefault(s => s.Option == "Option B").Count;
                Assert(optBCount == 2, "Vote change recorded (Option B has 2 votes)", "Vote change not recorded");
                Console.WriteLine();

                Print("STEP 5: Finalize Event");
                var finalEvent = await repos.Events.GetByIdAsync(999);
                finalEvent.Status = EventStatus.Finalized;
                await repos.Events.UpdateAsync(finalEvent);
                var finalizedEvent = await repos.Events.GetByIdAsync(999);
                Assert(finalizedEvent?.Status == EventStatus.Finalized, "Event finalized successfully", "Event finalization failed");
                Console.WriteLine();

                Print("STEP 6: Send Invitations");
                var inviteEvent = new Event
                {
                    Id = 998, Name = "Invite Test Event",
                    Date = DateTime.Now.AddDays(30), Location = "Test",
                    PricePerParticipant = 100, EventManagerId = 100, EventHostId = 10,
                    Status = EventStatus.Planning,
                    ParticipantIds = new List<int> { 2001, 2002 }
                };
                await repos.Events.AddAsync(inviteEvent);
                var p3pending = new Participant
                {
                    Id = 2003, Name = "Pending User", Email = "pending@example.com",
                    IsAttending = null, MailingPreferences = new List<MailingPreference>()
                };
                await repos.Participants.AddAsync(p3pending);
                var eventWithPending = await repos.Events.GetByIdAsync(998);
                eventWithPending.ParticipantIds.Add(2003);
                await repos.Events.UpdateAsync(eventWithPending);

                await eventService.SendInvitationsAsync(998, "https://gatherup.app/invite/998");
                var inviteSentEvent = await repos.Events.GetByIdAsync(998);
                Assert(inviteSentEvent.Status == EventStatus.InvitationsSent, "Invitations sent status updated", "Invitation status not updated");
                Console.WriteLine();

                Print("STEP 7: Register Participants (RSVP)");
                await participantService.ConfirmAttendanceAsync(2001, 999, isAttending: true,
                    new List<MailingPreference> { MailingPreference.EventChanges, MailingPreference.AttendanceConfirmed });
                await participantService.ConfirmAttendanceAsync(2002, 999, isAttending: true,
                    new List<MailingPreference> { MailingPreference.PaymentReceived });
                var p1Check = await repos.Participants.GetByIdAsync(2001);
                var p2Check = await repos.Participants.GetByIdAsync(2002);
                Assert(p1Check.IsAttending == true, "Participant 1 confirmed attendance", "Participant 1 attendance not confirmed");
                Assert(p2Check.IsAttending == true, "Participant 2 confirmed attendance", "Participant 2 attendance not confirmed");
                Assert(p1Check.MailingPreferences.Contains(MailingPreference.AttendanceConfirmed),
                    "Participant 1 has AttendanceConfirmed pref", "AttendanceConfirmed preference not set");
                Console.WriteLine();

                Print("STEP 8: Record Payments");
                await financeService.RegisterPaymentAsync(2001, 999, 150);
                await financeService.RegisterPaymentAsync(2002, 999, 150);
                var p1Payment = await repos.Participants.GetByIdAsync(2001);
                var p2Payment = await repos.Participants.GetByIdAsync(2002);
                Assert(p1Payment.HasPaid == true,             "Participant 1 payment recorded", "Participant 1 payment not recorded");
                Assert(p2Payment.HasPaid == true,             "Participant 2 payment recorded", "Participant 2 payment not recorded");
                Assert(p1Payment.AmountContributed == 150,    "Participant 1 amount correct",   "Participant 1 amount incorrect");
                Assert(p2Payment.AmountContributed == 150,    "Participant 2 amount correct",   "Participant 2 amount incorrect");
                Console.WriteLine();

                Print("STEP 9: Add Vendors");
                var vendor1 = await financeService.AddVendorToEventAsync(999, "Catering",    800);
                var vendor2 = await financeService.AddVendorToEventAsync(999, "Decorations", 500);
                Assert(vendor1 != null,                   "Vendor 1 created",      "Vendor 1 creation failed");
                Assert(vendor2 != null,                   "Vendor 2 created",      "Vendor 2 creation failed");
                Assert(vendor1?.Name == "Catering",       "Vendor 1 name correct", "Vendor 1 name incorrect");
                Assert(vendor2?.AmountOwed == 500,        "Vendor 2 debt correct", "Vendor 2 debt incorrect");
                Console.WriteLine();

                Print("STEP 10: Upload Receipts");
                string dummyFile = Path.Combine(baseDir, "receipt_e2e.txt");
                File.WriteAllText(dummyFile, "Receipt #RCP-E2E-001\nCatering services: 800");

                var receipt = new Receipt
                {
                    Id            = 5001,
                    VendorId      = vendor1!.Id,
                    ReceiptNumber = "RCP-E2E-001",
                    FilePath      = dummyFile,
                    Amount        = 800,
                    Date          = DateTime.Now
                };
                await financeService.AddReceiptAsync(receipt, vendor1!.Id);

                var vendor1Check = await repos.Vendors.GetByIdAsync(vendor1!.Id);
                Assert(vendor1Check?.AmountOwed == 0,    "Vendor 1 debt cleared by receipt", "Vendor 1 debt not cleared");
                Assert(vendor1Check?.IsPaid     == true, "Vendor 1 marked as paid",          "Vendor 1 not marked paid");

                var copiedFiles = Directory.GetFiles(receiptsFolder);
                Assert(copiedFiles.Length > 0, "Receipt file copied to ReceiptsStorage", "Receipt file not copied");
                Console.WriteLine();

                Print("STEP 11: Send Notifications (Integrated)");
                string emailLog = Path.Combine(emailsFolder, "emails.log");
                Assert(File.Exists(emailLog), "Email log exists", "Email log not created");
                var logContent = File.ReadAllText(emailLog);
                Assert(logContent.Length > 0,                           "Email log has entries",              "Email log is empty");
                Assert(logContent.Contains("test1@example.com"),        "Notification sent to participant 1", "No notification for participant 1");
                Assert(logContent.Contains("test2@example.com"),        "Notification sent to participant 2", "No notification for participant 2");
                Console.WriteLine();

                Print("Financial Summary Verification");
                var finSummary = await financeService.GetFinancialSummaryAsync(999);
                Assert(finSummary != null, "Financial summary retrieved", "Financial summary retrieval failed");
                Assert((finSummary?.TotalIncome ?? -1) == 300,
                    $"Total income correct (got {finSummary?.TotalIncome})", "Total income incorrect");
                Console.WriteLine($"  Total Income:   {finSummary?.TotalIncome}");
                Console.WriteLine($"  Total Expenses: {finSummary?.TotalExpenses}");
                Console.WriteLine($"  Balance:        {finSummary?.Balance}");
                Console.WriteLine();

                Print("Data Persistence Verification (XML on Disk)");
                var persistedEvent = await repos.Events.GetByIdAsync(999);
                Assert(persistedEvent != null, "Event persisted to XML", "Event not persisted");

                var allParticipants = (await repos.Participants.GetAllAsync()).ToList();
                Assert(allParticipants.Count > 0, "Participants persisted to XML", "Participants not persisted");

                var allVendors = (await repos.Vendors.GetAllAsync()).ToList();
                Assert(allVendors.Count > 0, "Vendors persisted to XML", "Vendors not persisted");

                Assert(File.Exists(Path.Combine(xmlFolder, "Events.xml")),       "Events.xml exists on disk",       "Events.xml missing");
                Assert(File.Exists(Path.Combine(xmlFolder, "Participants.xml")), "Participants.xml exists on disk", "Participants.xml missing");
                Assert(File.Exists(Path.Combine(xmlFolder, "Polls.xml")),        "Polls.xml exists on disk",        "Polls.xml missing");
                Assert(File.Exists(Path.Combine(xmlFolder, "Receipts.xml")),     "Receipts.xml exists on disk",     "Receipts.xml missing");
                Console.WriteLine();

                PrintSummary();
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"\n CRITICAL ERROR: {ex.Message}");
                Console.WriteLine($"Stack Trace: {ex.StackTrace}");
                Console.ResetColor();
                FailedTests++;
                PrintSummary();
            }

            Console.WriteLine();
            Print("Test Artifacts");
            Console.WriteLine($"  Emails:   {Path.Combine(emailsFolder, "emails.log")}");
            Console.WriteLine($"  XML Data: {xmlFolder}");
            Console.WriteLine($"  Receipts: {receiptsFolder}");
        }

        static void Assert(bool condition, string successMsg, string failureMsg)
        {
            if (condition)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"  {successMsg}");
                Console.ResetColor();
                PassedTests++;
                TestResults.Add($"PASS: {successMsg}");
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"  {failureMsg}");
                Console.ResetColor();
                FailedTests++;
                TestResults.Add($"FAIL: {failureMsg}");
            }
        }

        static void Print(string title)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"\n=== {title} ===");
            Console.ResetColor();
        }

        static void PrintSummary()
        {
            Console.WriteLine("\n" + new string('=', 60));
            Print("INTEGRATION TEST SUMMARY");
            Console.WriteLine($"  Passed: {PassedTests}");
            Console.WriteLine($"  Failed: {FailedTests}");
            Console.WriteLine($"  Total:  {PassedTests + FailedTests}");

            if (FailedTests == 0)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("\n  ALL TESTS PASSED");
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"\n  {FailedTests} TEST(S) FAILED");
            }
            Console.ResetColor();
            Console.WriteLine(new string('=', 60));
        }
    }
}
