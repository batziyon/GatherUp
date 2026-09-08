using System.Threading.Tasks;

namespace GatherUp.Core.Interfaces
{
    public interface IEventNotifier
    {
        Task OnAttendanceConfirmedAsync(int participantId, int eventId);
        Task OnPaymentReceivedAsync(int participantId, int eventId);
        Task OnPollAnsweredAsync(int participantId, int pollId);
        Task OnPollCreatedAsync(int pollId, int eventId);
        Task OnEventDetailsChangedAsync(int eventId);
    }
}
