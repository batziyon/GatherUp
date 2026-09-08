using System.Collections.Generic;
using System.Threading.Tasks;
using GatherUp.Core.DO;

namespace GatherUp.Core.Interfaces
{
    public interface IEmailService
    {
        Task SendAsync(string toEmail, string subject, string body, int? eventId = null);
        Task<IEnumerable<EmailLogEntry>> GetAllLogsAsync();
    }
}
