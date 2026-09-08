using System.Linq;
using System.Security.Claims;
using GatherUp.API.DTOs;
using GatherUp.BL;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.JsonWebTokens;

namespace GatherUp.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class EventsController : ControllerBase
    {
        private readonly EventService       _eventService;
        private readonly EmailLogService    _emailLogService;
        private readonly ParticipantService _participantService;

        public EventsController(EventService eventService, EmailLogService emailLogService, ParticipantService participantService)
        {
            _eventService       = eventService;
            _emailLogService    = emailLogService;
            _participantService = participantService;
        }

        [HttpGet]
        [AllowAnonymous]
        [ProducesResponseType(typeof(IEnumerable<EventResponse>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAll() =>
            Ok((await _eventService.GetAllAsync()).Select(EventMapper.ToResponse));

        [HttpGet("{id:int}")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(EventResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetById(int id) =>
            Ok(EventMapper.ToResponse(await _eventService.GetByIdAsync(id)));

        [HttpGet("my/managed")]
        [Authorize(Roles = "Manager")]
        [ProducesResponseType(typeof(IEnumerable<EventResponse>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetMyManagedEvents() =>
            Ok((await _eventService.GetEventsByManagerAsync(GetCallerId())).Select(EventMapper.ToResponse));

        [HttpGet("my/participating")]
        [ProducesResponseType(typeof(IEnumerable<EventResponse>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetMyParticipatingEvents()
        {
            string? email = User.FindFirstValue(ClaimTypes.Email)
                         ?? User.FindFirstValue(JwtRegisteredClaimNames.Email);

            if (string.IsNullOrEmpty(email))
                return Ok(Enumerable.Empty<EventResponse>());

            var events = await _participantService.GetEventsByParticipantEmailAsync(email);
            return Ok(events.Select(EventMapper.ToResponse));
        }

        [HttpGet("host/{hostId:int}")]
        [ProducesResponseType(typeof(IEnumerable<EventResponse>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetByHost(int hostId) =>
            Ok((await _eventService.GetEventsByHostAsync(hostId)).Select(EventMapper.ToResponse));

        [HttpPost]
        [Authorize(Roles = "Manager")]
        [ProducesResponseType(typeof(EventResponse), StatusCodes.Status201Created)]
        public async Task<IActionResult> Create([FromBody] CreateEventRequest request)
        {
            int callerId = GetCallerId();

            var ev = await _eventService.CreateEventAsync(
                request.Name, request.Date, request.Location,
                request.PricePerParticipant, callerId, request.EventHostId ?? 0,
                request.InvitationMessage, request.PaymentDetails, request.PreliminaryPollId);

            return CreatedAtAction(nameof(GetById), new { id = ev.Id }, EventMapper.ToResponse(ev));
        }

        [HttpPut("{id:int}")]
        [Authorize(Roles = "Manager")]
        [ProducesResponseType(typeof(EventResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateEventRequest request)
        {
            await _eventService.EnsureIsEventManagerAsync(id, GetCallerId(), "update event");

            await _eventService.UpdateDetailsAsync(id, request.Name, request.Location,
                request.PricePerParticipant, request.InvitationMessage, request.PaymentDetails,
                request.Date, request.EventHostId);

            return Ok(EventMapper.ToResponse(await _eventService.GetByIdAsync(id)));
        }

        [HttpDelete("{id:int}")]
        [Authorize(Roles = "Manager")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Delete(int id)
        {
            await _eventService.EnsureIsEventManagerAsync(id, GetCallerId(), "delete event");

            await _eventService.DeleteAsync(id);
            return NoContent();
        }

        [HttpPost("{id:int}/invitations")]
        [Authorize(Roles = "Manager")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> SendInvitations(int id, [FromBody] SendInvitationsRequest request)
        {
            await _eventService.EnsureIsEventManagerAsync(id, GetCallerId(), "send invitations");

            await _eventService.SendInvitationsAsync(id, request.RegistrationLink);
            return NoContent();
        }

        [HttpPost("{id:int}/host-invitation")]
        [Authorize(Roles = "Manager")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> SendHostInvitation(int id, [FromBody] SendHostInvitationRequest request)
        {
            await _eventService.EnsureIsEventManagerAsync(id, GetCallerId(), "send host invitation");

            await _eventService.SendHostInvitationAsync(id, request.HostMessageContent);
            return NoContent();
        }

        [HttpPost("{id:int}/host-invitation/schedule")]
        [Authorize(Roles = "Manager")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> ScheduleHostInvitation(int id, [FromBody] ScheduleHostInvitationRequest request)
        {
            await _eventService.EnsureIsEventManagerAsync(id, GetCallerId(), "schedule host invitation");

            await _eventService.ScheduleHostInvitationAsync(id, request.ScheduledAt);
            return NoContent();
        }

        [HttpGet("{id:int}/emails")]
        [Authorize(Roles = "Manager")]
        [ProducesResponseType(typeof(IEnumerable<EmailLogResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetEventEmails(int id)
        {
            await _eventService.EnsureIsEventManagerAsync(id, GetCallerId(), "view event emails");

            var emails = await _emailLogService.GetEmailsForEventAsync(id);
            return Ok(emails.Select(e => new EmailLogResponse(e.SentAt, e.ToEmail, e.Subject, e.Body, e.EventId)));
        }

        [HttpGet("my/managed/emails")]
        [Authorize(Roles = "Manager")]
        [ProducesResponseType(typeof(IEnumerable<EmailLogResponse>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetMyManagedEventsEmails()
        {
            var emails = await _emailLogService.GetEmailsForManagerEventsAsync(GetCallerId());
            return Ok(emails.Select(e => new EmailLogResponse(e.SentAt, e.ToEmail, e.Subject, e.Body, e.EventId)));
        }

        private int GetCallerId() =>
            int.TryParse(User.FindFirstValue("userId"), out int id) ? id : 0;
    }
}
