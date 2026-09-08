using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GatherUp.Core.Exceptions;
using GatherUp.Core.DO;
using GatherUp.Core.Interfaces;

namespace GatherUp.BL
{
    public class EventService
    {
        private readonly IRepository<Event>        _eventRepo;
        private readonly IRepository<EventManager> _managerRepo;
        private readonly IRepository<EventHost>    _hostRepo;
        private readonly IRepository<Participant>  _participantRepo;
        private readonly IEmailService             _emailService;
        private readonly IEventNotifier?           _notifier;

        public EventService(
            IRepository<Event>        eventRepo,
            IRepository<EventManager> managerRepo,
            IRepository<EventHost>    hostRepo,
            IRepository<Participant>  participantRepo,
            IEmailService             emailService,
            IEventNotifier?           notifier = null)
        {
            _eventRepo       = eventRepo;
            _managerRepo     = managerRepo;
            _hostRepo        = hostRepo;
            _participantRepo = participantRepo;
            _emailService    = emailService;
            _notifier        = notifier;
        }

        public async Task<Event> CreateEventAsync(
            string name, DateTime date, string? location,
            decimal pricePerParticipant, int managerId, int hostId,
            string? invitationMessage = null, string? paymentDetails = null,
            int? preliminaryPollId = null)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new InvalidInputException(nameof(name), "event name cannot be empty");
            if (pricePerParticipant < 0)
                throw new InvalidInputException(nameof(pricePerParticipant), "price cannot be negative");

            bool managerExists = false;
            try { await _managerRepo.GetByIdAsync(managerId); managerExists = true; } catch { }
            if (!managerExists)
                throw new InvalidInputException(nameof(managerId),
                    $"EventManager with id {managerId} was not found. " +
                    "Please log out and log in again to refresh your session.");

            if (hostId != 0)
            {
                bool hostFound = false;
                try { await _hostRepo.GetByIdAsync(hostId); hostFound = true; } catch { }
                if (!hostFound)
                    try { await _managerRepo.GetByIdAsync(hostId); hostFound = true; } catch { }
                if (!hostFound)
                    try { await _participantRepo.GetByIdAsync(hostId); hostFound = true; } catch { }
                if (!hostFound)
                    throw new InvalidInputException(nameof(hostId), $"host with id {hostId} was not found in any store");
            }

            var all   = (await _eventRepo.GetAllAsync()).ToList();
            int newId = all.Any() ? all.Max(e => e.Id) + 1 : 1;

            var ev = new Event
            {
                Id                  = newId,
                Name                = name,
                Date                = date,
                Location            = location,
                PricePerParticipant = pricePerParticipant,
                EventManagerId      = managerId,
                EventHostId         = hostId,
                InvitationMessage   = invitationMessage,
                PaymentDetails      = paymentDetails,
                PreliminaryPollId   = preliminaryPollId,
                Status              = EventStatus.Planning
            };
            await _eventRepo.AddAsync(ev);
            return ev;
        }

        public async Task SendInvitationsAsync(int eventId, string registrationBaseLink)
        {
            var ev = await _eventRepo.GetByIdAsync(eventId);
            EnsureNotFinalized(ev);
            var allParticipants = (await _participantRepo.GetAllAsync()).ToList();

            var pending = allParticipants
                .Where(p => ev.ParticipantIds.Contains(p.Id) && p.IsAttending == null)
                .ToList();

            if (!pending.Any()) return;

            await Task.WhenAll(pending.Select(p =>
            {
                string personalizedLink = BuildPersonalizedLink(registrationBaseLink, p.Id, eventId);
                string body = BuildInvitationBody(p.Name, ev, personalizedLink);
                return _emailService.SendAsync(p.Email, $"הזמנה לאירוע: {ev.Name}", body, eventId)
                    .ContinueWith(t =>
                    {
                        if (t.IsFaulted)
                            System.Diagnostics.Debug.WriteLine(
                                $"Failed to send invitation to {p.Email}: {t.Exception?.GetBaseException().Message}");
                    });
            }));

            if (ev.Status != EventStatus.Finalized)
                ev.Status = EventStatus.InvitationsSent;

            await _eventRepo.UpdateAsync(ev);
        }

        public async Task SendHostInvitationAsync(int eventId, string hostMessageContent)
        {
            var ev = await _eventRepo.GetByIdAsync(eventId);
            EnsureNotFinalized(ev);

            if (string.IsNullOrWhiteSpace(hostMessageContent))
                throw new InvalidInputException(nameof(hostMessageContent), "host message cannot be empty");

            string? hostEmail = null;
            try { var host = await _hostRepo.GetByIdAsync(ev.EventHostId); hostEmail = host.Email; }
            catch
            {
                try { var mgr = await _managerRepo.GetByIdAsync(ev.EventHostId); hostEmail = mgr.Email; }
                catch
                {
                    try { var part = await _participantRepo.GetByIdAsync(ev.EventHostId); hostEmail = part.Email; }
                    catch { }
                }
            }

            if (hostEmail == null)
                throw new InvalidInputException("hostId", $"host {ev.EventHostId} not found in any store");

            try { await _emailService.SendAsync(hostEmail, $"הזמנה מיוחדת — {ev.Name}", hostMessageContent, eventId); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Failed to send host invitation for event {eventId}: {ex.Message}"); }

            ev.HostInvitationSent = true;
            await _eventRepo.UpdateAsync(ev);
        }

        public async Task ScheduleHostInvitationAsync(int eventId, DateTime scheduledAt)
        {
            var ev = await _eventRepo.GetByIdAsync(eventId);
            EnsureNotFinalized(ev);
            ev.HostInvitationScheduledAt = scheduledAt;
            await _eventRepo.UpdateAsync(ev);
        }

        public async Task<IEnumerable<Event>> GetEventsByManagerAsync(int managerId) =>
            (await _eventRepo.GetAllAsync()).Where(e => e.EventManagerId == managerId);

        public async Task<IEnumerable<Event>> GetEventsByParticipantAsync(int participantId) =>
            (await _eventRepo.GetAllAsync()).Where(e => e.ParticipantIds.Contains(participantId));

        public async Task<IEnumerable<Event>> GetEventsByParticipantEmailAsync(
            string email,
            IRepository<Participant> participantRepo)
        {
            var participant = (await participantRepo.GetAllAsync())
                .FirstOrDefault(p => p.Email.Equals(email, StringComparison.OrdinalIgnoreCase));
            if (participant == null) return Enumerable.Empty<Event>();
            return (await _eventRepo.GetAllAsync()).Where(e => e.ParticipantIds.Contains(participant.Id));
        }

        public async Task<IEnumerable<Event>> GetEventsByHostAsync(int hostId) =>
            (await _eventRepo.GetAllAsync()).Where(e => e.EventHostId == hostId);

        public async Task<bool> IsEventManagerAsync(int eventId, int userId)
        {
            var ev = await _eventRepo.GetByIdAsync(eventId);
            return ev.EventManagerId == userId;
        }

        public async Task EnsureIsEventManagerAsync(int eventId, int userId, string operation)
        {
            var ev = await _eventRepo.GetByIdAsync(eventId);
            if (ev.EventManagerId != userId)
                throw new UnauthorizedOperationException(userId, operation);
        }

        public Task<Event>              GetByIdAsync(int id)         => _eventRepo.GetByIdAsync(id);
        public Task<IEnumerable<Event>> GetAllAsync()                => _eventRepo.GetAllAsync();
        public Task                     DeleteAsync(int eventId)     => _eventRepo.DeleteAsync(eventId);
        public Task<EventHost>          GetHostByIdAsync(int hostId) => _hostRepo.GetByIdAsync(hostId);
        public Task<IEnumerable<EventHost>> GetAllHostsAsync()       => _hostRepo.GetAllAsync();

        public async Task UpdateDetailsAsync(int eventId, string? newName, string? newLocation, decimal? newPrice,
            string? invitationMessage = null, string? paymentDetails = null, DateTime? newDate = null, int? newHostId = null)
        {
            if (newPrice.HasValue && newPrice.Value < 0)
                throw new InvalidInputException(nameof(newPrice), "price cannot be negative");

            var ev = await _eventRepo.GetByIdAsync(eventId);
            EnsureNotFinalized(ev);
            if (newName           != null) ev.Name                = newName;
            if (newLocation       != null) ev.Location            = newLocation;
            if (newPrice          != null) ev.PricePerParticipant = newPrice.Value;
            if (invitationMessage != null) ev.InvitationMessage   = invitationMessage;
            if (paymentDetails    != null) ev.PaymentDetails      = paymentDetails;
            if (newDate           != null) ev.Date                = newDate.Value;
            if (newHostId         != null) ev.EventHostId         = newHostId.Value;
            await _eventRepo.UpdateAsync(ev);

            if (_notifier != null)
                await _notifier.OnEventDetailsChangedAsync(eventId);
        }

        private static void EnsureNotFinalized(Event ev)
        {
            if (ev.Status == EventStatus.Finalized)
                throw new InvalidInputException(nameof(ev.Status), "event is finalized and cannot be changed");
        }

        private static string BuildPersonalizedLink(string baseLink, int participantId, int eventId)
        {
            string trimmed   = baseLink.TrimEnd('/');
            string separator = trimmed.Contains('?') ? "&" : "?";
            return $"{trimmed}{separator}participantId={participantId}&eventId={eventId}";
        }

        private static string BuildInvitationBody(string participantName, Event ev, string rsvpLink)
        {
            string custom  = ev.InvitationMessage ?? $"אנו שמחים להזמין אותך לאירוע {ev.Name}.";
            string payment = ev.PaymentDetails != null
                ? $"\n💰 סכום לתשלום: {ev.PricePerParticipant}₪\n💳 אפשרויות תשלום: {ev.PaymentDetails}"
                : $"\n💰 סכום לתשלום: {ev.PricePerParticipant}₪";

            string pollSection = string.Empty;
            if (ev.PreliminaryPollId.HasValue)
            {
                string baseOrigin = rsvpLink.Contains("rsvp.html")
                    ? rsvpLink.Substring(0, rsvpLink.IndexOf("rsvp.html"))
                    : rsvpLink.TrimEnd('/') + "/";
                string pollLink = $"{baseOrigin}poll-vote.html?pollId={ev.PreliminaryPollId}";
                pollSection = $"\n\n📝 לפני האישור, אנא מלא/י את השאלון המקדים:\n{pollLink}";
            }

            return $"שלום {participantName},\n\n{custom}\n\n📅 תאריך: {ev.Date:dd/MM/yyyy}\n" +
                   $"📍 מיקום: {ev.Location ?? "טרם נקבע"}{payment}{pollSection}\n\n" +
                   $"✅ לאישור הגעה לחץ/י כאן:\n{rsvpLink}\n\nבברכה,\nמערכת GatherUp";
        }
    }
}
