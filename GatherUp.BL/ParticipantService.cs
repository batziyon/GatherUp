using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GatherUp.Core.Exceptions;
using GatherUp.Core.DO;
using GatherUp.Core.Interfaces;

namespace GatherUp.BL
{
    public class ParticipantService
    {
        private readonly IRepository<Participant> _participantRepo;
        private readonly IRepository<Event>       _eventRepo;
        private readonly IEmailService            _emailService;
        private readonly IEventNotifier?          _notifier;

        public ParticipantService(
            IRepository<Participant> participantRepo,
            IRepository<Event>       eventRepo,
            IEmailService            emailService,
            IEventNotifier?          notifier = null)
        {
            _participantRepo = participantRepo;
            _eventRepo       = eventRepo;
            _emailService    = emailService;
            _notifier        = notifier;
        }

        public async Task AddParticipantToEventAsync(int eventId, Participant participant, string? baseUrl = null)
        {
            if (string.IsNullOrWhiteSpace(participant.Name))
                throw new InvalidInputException(nameof(participant.Name), "participant name cannot be empty");
            if (string.IsNullOrWhiteSpace(participant.Email))
                throw new InvalidInputException(nameof(participant.Email), "participant email cannot be empty");

            var ev              = await _eventRepo.GetByIdAsync(eventId);
            var allParticipants = (await _participantRepo.GetAllAsync()).ToList();
            var existingById    = allParticipants.FirstOrDefault(p => p.Id == participant.Id);
            var existingByEmail = allParticipants.FirstOrDefault(p => p.Email.Equals(participant.Email, StringComparison.OrdinalIgnoreCase));

            if (existingByEmail != null && existingByEmail.Id != participant.Id)
                throw new InvalidInputException(nameof(participant.Email), "email already registered for another participant");

            if (existingById != null)
            {
                if (!string.Equals(existingById.Email, participant.Email, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidInputException(nameof(participant.Id), "participant id conflicts with an existing record");

                if (ev.ParticipantIds.Contains(participant.Id))
                    return;

                participant = existingById;
            }
            else
            {
                if (existingByEmail != null)
                    throw new InvalidInputException(nameof(participant.Email), "email already registered for another participant");

                await _participantRepo.AddAsync(participant);
            }

            if (!ev.ParticipantIds.Contains(participant.Id))
            {
                ev.ParticipantIds.Add(participant.Id);
                await _eventRepo.UpdateAsync(ev);

                try
                {
                    string rsvpLink = !string.IsNullOrWhiteSpace(baseUrl)
                        ? $"{baseUrl.TrimEnd('/')}/rsvp.html?participantId={participant.Id}&eventId={ev.Id}"
                        : $"rsvp.html?participantId={participant.Id}&eventId={ev.Id}";

                    string joinBody =
                        $"שלום {participant.Name},\n\n" +
                        $"הוזמנת להשתתף באירוע \"{ev.Name}\".\n\n" +
                        $"📅 תאריך: {ev.Date:dd/MM/yyyy}\n" +
                        $"📍 מיקום: {ev.Location ?? "טרם נקבע"}\n" +
                        $"💰 מחיר: {ev.PricePerParticipant}₪\n\n" +
                        $"✅ לאישור הגעה לחץ/י על הלינק:\n{rsvpLink}\n\n" +
                        $"בברכה,\nמערכת GatherUp";
                    await _emailService.SendAsync(participant.Email, $"הוזמנת לאירוע: {ev.Name}", joinBody, ev.Id);
                }
                catch { }
            }
        }

        public async Task ConfirmAttendanceAsync(
            int participantId, int eventId, bool isAttending,
            List<MailingPreference>? selectedPreferences = null)
        {
            var ev          = await _eventRepo.GetByIdAsync(eventId);
            var participant = await _participantRepo.GetByIdAsync(participantId);
            participant.IsAttending = isAttending;

            if (selectedPreferences != null)
                participant.MailingPreferences = selectedPreferences;
            else if (isAttending)
                participant.MailingPreferences = participant.MailingPreferences.Any()
                    ? participant.MailingPreferences
                    : new List<MailingPreference> { MailingPreference.EventChanges };
            else
                participant.MailingPreferences = new List<MailingPreference>();

            await _participantRepo.UpdateAsync(participant);

            if (!ev.ParticipantIds.Contains(participantId))
            {
                ev.ParticipantIds.Add(participantId);
                await _eventRepo.UpdateAsync(ev);
            }

            if (_notifier != null)
                await _notifier.OnAttendanceConfirmedAsync(participantId, eventId);
        }

        public async Task<IEnumerable<Participant>> GetEventParticipantsAsync(int eventId)
        {
            var ev = await _eventRepo.GetByIdAsync(eventId);
            return (await _participantRepo.GetAllAsync()).Where(p => ev.ParticipantIds.Contains(p.Id));
        }

        public async Task<Participant?> GetByEmailAsync(string email) =>
            (await _participantRepo.GetAllAsync())
                .FirstOrDefault(p => p.Email.Equals(email, StringComparison.OrdinalIgnoreCase));

        public async Task<IEnumerable<Event>> GetEventsByParticipantEmailAsync(string email)
        {
            var participant = await GetByEmailAsync(email);
            if (participant == null) return Enumerable.Empty<Event>();
            return (await _eventRepo.GetAllAsync()).Where(e => e.ParticipantIds.Contains(participant.Id));
        }
    }
}
