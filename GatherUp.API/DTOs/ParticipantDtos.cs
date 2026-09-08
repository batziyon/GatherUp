using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using GatherUp.Core.DO;

namespace GatherUp.API.DTOs
{
    public record ParticipantResponse(
        int                     Id,
        string                  Name,
        string                  Email,
        bool?                   IsAttending,
        bool                    HasPaid,
        decimal                 AmountContributed,
        List<MailingPreference> MailingPreferences,
        List<EventPaymentDto>   EventPayments
    );

    public record EventPaymentDto(int EventId, decimal Amount);

    public record CreateParticipantRequest(
        [Required, MinLength(2)] string  Name,
        [Required, EmailAddress] string  Email,
        List<MailingPreference>?         MailingPreferences
    );

    public record UpdateParticipantRequest
    {
        public string?                  Name               { get; init; }
        public string?                  Email              { get; init; }
        public List<MailingPreference>? MailingPreferences { get; init; }
    }

    public record ConfirmAttendanceRequest(
        bool?                    IsAttending,
        List<MailingPreference>? SelectedPreferences
    );

    public static class ParticipantMapper
    {
        public static ParticipantResponse ToResponse(Participant p) => new(
            p.Id, p.Name, p.Email, p.IsAttending,
            p.HasPaid, p.AmountContributed, p.MailingPreferences,
            p.EventPayments.Select(ep => new EventPaymentDto(ep.EventId, ep.Amount)).ToList());

        public static Participant ToEntity(CreateParticipantRequest r, int newId) => new()
        {
            Id                 = newId,
            Name               = r.Name,
            Email              = r.Email,
            MailingPreferences = r.MailingPreferences ?? new List<MailingPreference>()
        };
    }
}
