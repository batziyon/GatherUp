using System.Linq;
using System.Threading.Tasks;
using GatherUp.Core.DO;
using GatherUp.Core.Interfaces;

namespace GatherUp.BL
{
    public class EventNotifierService : IEventNotifier
    {
        public static event Func<int, int, Task>? AttendanceConfirmed;
        public static event Func<int, int, Task>? PaymentReceived;
        public static event Func<int, int, Task>? PollAnswered;
        public static event Func<int, int, Task>? PollCreated;
        public static event Func<int, Task>?      EventDetailsChanged;

        private readonly IRepository<Participant>  _participantRepo;
        private readonly IRepository<EventManager> _managerRepo;
        private readonly IRepository<Event>        _eventRepo;
        private readonly IEmailService             _emailService;

        public EventNotifierService(
            IRepository<Participant>  participantRepo,
            IRepository<EventManager> managerRepo,
            IRepository<Event>        eventRepo,
            IEmailService             emailService)
        {
            _participantRepo = participantRepo;
            _managerRepo     = managerRepo;
            _eventRepo       = eventRepo;
            _emailService    = emailService;
        }

        public async Task OnAttendanceConfirmedAsync(int participantId, int eventId)
        {
            if (AttendanceConfirmed != null)
                await AttendanceConfirmed.Invoke(participantId, eventId);

            var ev          = await _eventRepo.GetByIdAsync(eventId);
            var manager     = (await _managerRepo.GetAllAsync()).FirstOrDefault(m => m.Id == ev.EventManagerId);
            var participant = (await _participantRepo.GetAllAsync()).FirstOrDefault(p => p.Id == participantId);

            if (manager == null || participant == null) return;

            string statusText = participant.IsAttending == true ? "✅ מגיע/ה" : "❌ לא מגיע/ה";

            try
            {
                await _emailService.SendAsync(manager.Email, "עדכון הגעה התקבל",
                    $"שלום {manager.Name},\n\n" +
                    $"המשתתף {participant.Name} עדכן את סטטוס הגעתו לאירוע \"{ev.Name}\".\n\n" +
                    $"סטטוס: {statusText}\n\n" +
                    $"בברכה,\nמערכת GatherUp",
                    eventId);

                string participantSubject = participant.IsAttending == true
                    ? "אישור הגעה נרשם ✅"
                    : "דיווח אי-הגעה נרשם ❌";
                string participantBody = participant.IsAttending == true
                    ? $"שלום {participant.Name},\n\n" +
                      $"אישור הגעתך לאירוע \"{ev.Name}\" נרשם בהצלחה.\n\n" +
                      $"📅 תאריך: {ev.Date:dd/MM/yyyy}\n" +
                      $"📍 מיקום: {ev.Location ?? "טרם נקבע"}\n" +
                      $"💰 סכום לתשלום: {ev.PricePerParticipant}₪\n\n" +
                      $"בברכה,\nמערכת GatherUp"
                    : $"שלום {participant.Name},\n\n" +
                      $"קיבלנו את הודעתך שלא תוכל/י להגיע לאירוע \"{ev.Name}\".\n\n" +
                      $"בברכה,\nמערכת GatherUp";

                await _emailService.SendAsync(participant.Email, participantSubject, participantBody, eventId);
            }
            catch { }
        }

        public async Task OnPaymentReceivedAsync(int participantId, int eventId)
        {
            if (PaymentReceived != null)
                await PaymentReceived.Invoke(participantId, eventId);

            var ev          = await _eventRepo.GetByIdAsync(eventId);
            var manager     = (await _managerRepo.GetAllAsync()).FirstOrDefault(m => m.Id == ev.EventManagerId);
            var participant = (await _participantRepo.GetAllAsync()).FirstOrDefault(p => p.Id == participantId);

            if (manager == null || participant == null) return;

            decimal amountForEvent = participant.GetAmountForEvent(eventId);

            try
            {
                await _emailService.SendAsync(manager.Email, "תשלום התקבל",
                    $"שלום {manager.Name},\n\n" +
                    $"המשתתף {participant.Name} שילם עבור אירוע \"{ev.Name}\".\n\n" +
                    $"סכום: {amountForEvent}₪\n\n" +
                    $"בברכה,\nמערכת GatherUp",
                    eventId);

                await _emailService.SendAsync(participant.Email, "אישור תשלום",
                    $"שלום {participant.Name},\n\n" +
                    $"תשלומך עבור אירוע \"{ev.Name}\" נרשם בהצלחה.\n\n" +
                    $"סכום: {amountForEvent}₪\n\n" +
                    $"בברכה,\nמערכת GatherUp",
                    eventId);
            }
            catch { }
        }

        public async Task OnPollAnsweredAsync(int participantId, int pollId)
        {
            if (PollAnswered != null)
                await PollAnswered.Invoke(participantId, pollId);

            try
            {
                var ev = (await _eventRepo.GetAllAsync()).FirstOrDefault(e => e.PollIds.Contains(pollId));
                if (ev == null) return;
                var manager = (await _managerRepo.GetAllAsync()).FirstOrDefault(m => m.Id == ev.EventManagerId);
                if (manager == null) return;
                var who = (await _participantRepo.GetAllAsync()).FirstOrDefault(p => p.Id == participantId);
                if (who == null) return;

                await _emailService.SendAsync(manager.Email, "תשובה חדשה לסקר",
                    $"שלום {manager.Name},\n\nהמשתתף {who.Name} ענה על סקר {pollId} באירוע {ev.Name}.\n\nבברכה,\nמערכת GatherUp",
                    ev.Id);
            }
            catch { }
        }

        public async Task OnPollCreatedAsync(int pollId, int eventId)
        {
            if (PollCreated != null)
                await PollCreated.Invoke(pollId, eventId);

            try
            {
                var ev = await _eventRepo.GetByIdAsync(eventId);
                var participants = (await _participantRepo.GetAllAsync())
                    .Where(p => ev.ParticipantIds.Contains(p.Id)
                        && p.MailingPreferences.Contains(MailingPreference.PollCreated))
                    .ToList();

                await Task.WhenAll(participants.Select(p =>
                    _emailService.SendAsync(p.Email, $"סקר חדש באירוע: {ev.Name}",
                        $"שלום {p.Name},\n\n" +
                        $"נוצר סקר חדש באירוע \"{ev.Name}\".\n\n" +
                        $"להשתתפות בסקר: /poll-vote.html?pollId={pollId}\n\n" +
                        $"בברכה,\nמערכת GatherUp",
                        eventId)
                        .ContinueWith(t => {
                            if (t.IsFaulted)
                                System.Diagnostics.Debug.WriteLine($"Poll notify failed for {p.Email}: {t.Exception?.GetBaseException().Message}");
                        })));
            }
            catch { }
        }

        public async Task OnEventDetailsChangedAsync(int eventId)
        {
            if (EventDetailsChanged != null)
                await EventDetailsChanged.Invoke(eventId);

            try
            {
                var ev = await _eventRepo.GetByIdAsync(eventId);
                var participants = (await _participantRepo.GetAllAsync())
                    .Where(p => ev.ParticipantIds.Contains(p.Id)
                        && p.MailingPreferences.Contains(MailingPreference.EventChanges))
                    .ToList();

                await Task.WhenAll(participants.Select(p =>
                    _emailService.SendAsync(p.Email, "פרטי האירוע עודכנו",
                        $"שלום {p.Name},\n\nחל עדכון בפרטי האירוע {ev.Name}.\n\nתאריך: {ev.Date:dd/MM/yyyy}\n" +
                        $"מיקום: {ev.Location ?? "טרם נקבע"}\nמחיר: {ev.PricePerParticipant}₪\n\nבברכה,\nמערכת GatherUp",
                        eventId)
                        .ContinueWith(t => {
                            if (t.IsFaulted)
                                System.Diagnostics.Debug.WriteLine($"Details notify failed for {p.Email}: {t.Exception?.GetBaseException().Message}");
                        })));
            }
            catch { }
        }
    }
}
