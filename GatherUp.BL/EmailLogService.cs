using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GatherUp.Core.DO;
using GatherUp.Core.Interfaces;

namespace GatherUp.BL
{
    public class EmailLogService
    {
        private readonly IEmailService             _emailService;
        private readonly IRepository<Event>        _eventRepo;
        private readonly IRepository<EventManager> _managerRepo;

        public EmailLogService(
            IEmailService             emailService,
            IRepository<Event>        eventRepo,
            IRepository<EventManager> managerRepo)
        {
            _emailService = emailService;
            _eventRepo    = eventRepo;
            _managerRepo  = managerRepo;
        }

        public async Task<IEnumerable<EmailLogEntry>> GetEmailsForUserAsync(string email)
        {
            var all = await _emailService.GetAllLogsAsync();
            return all
                .Where(e => e.ToEmail.Equals(email, System.StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(e => e.SentAt);
        }

        public async Task<IEnumerable<EmailLogEntry>> GetEmailsForManagerEventsAsync(int managerId)
        {
            var managedEvents = (await _eventRepo.GetAllAsync())
                .Where(ev => ev.EventManagerId == managerId)
                .Select(ev => ev.Id)
                .ToHashSet();

            if (!managedEvents.Any())
                return Enumerable.Empty<EmailLogEntry>();

            var all = await _emailService.GetAllLogsAsync();
            return all
                .Where(e => e.EventId.HasValue && managedEvents.Contains(e.EventId.Value))
                .OrderByDescending(e => e.SentAt);
        }

        public async Task<IEnumerable<EmailLogEntry>> GetEmailsForEventAsync(int eventId)
        {
            var all = await _emailService.GetAllLogsAsync();
            return all
                .Where(e => e.EventId == eventId)
                .OrderByDescending(e => e.SentAt);
        }
    }
}
