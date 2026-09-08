using System.Security.Claims;
using GatherUp.API.DTOs;
using GatherUp.BL;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GatherUp.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class NotificationsController : ControllerBase
    {
        private readonly EmailLogService _emailLogService;
        private readonly EventService    _eventService;

        public NotificationsController(EmailLogService emailLogService, EventService eventService)
        {
            _emailLogService = emailLogService;
            _eventService    = eventService;
        }

        [HttpGet("log")]
        [ProducesResponseType(typeof(EmailLogLinesResponse), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetLog()
        {
            int callerId = GetCallerId();
            bool isManager = User.IsInRole("Manager");

            IEnumerable<EmailLogResponse> emails;

            if (isManager)
            {
                var raw = await _emailLogService.GetEmailsForManagerEventsAsync(callerId);
                emails = raw.Select(e => new EmailLogResponse(e.SentAt, e.ToEmail, e.Subject, e.Body, e.EventId));
            }
            else
            {
                string? email = User.FindFirstValue(ClaimTypes.Email)
                             ?? User.FindFirstValue(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Email);
                if (string.IsNullOrEmpty(email))
                    return BadRequest(new { message = "לא ניתן לזהות את כתובת המייל של המשתמש המחובר." });

                var raw = await _emailLogService.GetEmailsForUserAsync(email);
                emails = raw.Select(e => new EmailLogResponse(e.SentAt, e.ToEmail, e.Subject, e.Body, e.EventId));
            }

            var lines = FormatEmailsAsLines(emails.Take(60));
            return Ok(new EmailLogLinesResponse(lines));
        }

        [HttpGet("log/event/{eventId:int}")]
        [Authorize(Roles = "Manager")]
        [ProducesResponseType(typeof(EmailLogLinesResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetEventLog(int eventId)
        {
            int callerId = GetCallerId();
            await _eventService.EnsureIsEventManagerAsync(eventId, callerId, "view event email log");

            var raw = await _emailLogService.GetEmailsForEventAsync(eventId);
            var emails = raw.Select(e => new EmailLogResponse(e.SentAt, e.ToEmail, e.Subject, e.Body, e.EventId));

            var lines = FormatEmailsAsLines(emails);
            return Ok(new EmailLogLinesResponse(lines));
        }

        private static List<string> FormatEmailsAsLines(IEnumerable<EmailLogResponse> emails)
        {
            var lines = new List<string>();
            foreach (var e in emails)
            {
                lines.Add($"[{e.SentAt:yyyy-MM-dd HH:mm:ss}] To: {e.ToEmail} | Subject: {e.Subject}");
                foreach (var bodyLine in e.Body.Split('\n'))
                    lines.Add(bodyLine.TrimEnd('\r'));
                lines.Add(new string('-', 60));
                lines.Add(string.Empty);
            }
            return lines;
        }

        private int GetCallerId() =>
            int.TryParse(User.FindFirstValue("userId"), out int id) ? id : 0;
    }
}
