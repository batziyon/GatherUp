using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using GatherUp.Core.DO;

namespace GatherUp.API.DTOs
{
    public record EventResponse(
        int           Id,
        string        Name,
        DateTime      Date,
        string?       Location,
        decimal       PricePerParticipant,
        int           EventManagerId,
        int           EventHostId,
        int?          PreliminaryPollId,
        string?       InvitationMessage,
        string?       PaymentDetails,
        DateTime?     HostInvitationScheduledAt,
        bool          HostInvitationSent,
        EventStatus   Status,
        List<int>     ParticipantIds,
        List<int>     VendorIds,
        List<int>     PollIds
    );

    public record CreateEventRequest(
        [Required, MinLength(2)] string  Name,
        [Required]               DateTime Date,
                                 string?  Location,
        [Range(0, double.MaxValue)] decimal PricePerParticipant,
        [Required] int    EventManagerId,
                   int?   EventHostId,
                   string? InvitationMessage = null,
                   string? PaymentDetails    = null,
                   int?    PreliminaryPollId = null
    );

    public record UpdateEventRequest(
        string?   Name                = null,
        string?   Location            = null,
        [Range(0, double.MaxValue)] decimal? PricePerParticipant = null,
        string?   InvitationMessage   = null,
        string?   PaymentDetails      = null,
        DateTime? Date                = null,
        int?      EventHostId         = null
    );

    public record SendInvitationsRequest(
        [Required] string RegistrationLink
    );

    public record SendHostInvitationRequest(
        [Required] string HostMessageContent
    );

    public record ScheduleHostInvitationRequest(
        [Required] DateTime ScheduledAt
    );

    public record EventHostResponse(int Id, string Name, string Email);

    public static class EventMapper
    {
        public static EventResponse ToResponse(Event e) => new(
            e.Id, e.Name, e.Date, e.Location,
            e.PricePerParticipant, e.EventManagerId, e.EventHostId,
            e.PreliminaryPollId, e.InvitationMessage, e.PaymentDetails,
            e.HostInvitationScheduledAt, e.HostInvitationSent,
            e.Status,
            e.ParticipantIds, e.VendorIds, e.PollIds);

        public static EventHostResponse ToHostResponse(EventHost h) => new(h.Id, h.Name, h.Email);
    }
}
