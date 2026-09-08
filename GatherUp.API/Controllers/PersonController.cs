using System.Linq;
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
    public class PersonController : ControllerBase
    {
        private readonly PersonService   _personService;
        private readonly EmailLogService _emailLogService;

        public PersonController(PersonService personService, EmailLogService emailLogService)
        {
            _personService   = personService;
            _emailLogService = emailLogService;
        }

        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<ParticipantResponse>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAll() =>
            Ok((await _personService.GetAllParticipantsAsync()).Select(ParticipantMapper.ToResponse));

        [HttpGet("{id:int}")]
        [ProducesResponseType(typeof(ParticipantResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetById(int id) =>
            Ok(ParticipantMapper.ToResponse(await _personService.GetParticipantByIdAsync(id)));

        [HttpGet("my/emails")]
        [ProducesResponseType(typeof(IEnumerable<EmailLogResponse>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetMyEmails()
        {
            string? email = User.FindFirstValue(ClaimTypes.Email)
                         ?? User.FindFirstValue(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Email);

            if (string.IsNullOrEmpty(email))
                return BadRequest(new { message = "לא ניתן לזהות את כתובת המייל של המשתמש המחובר." });

            var emails = await _emailLogService.GetEmailsForUserAsync(email);
            return Ok(emails.Select(e => new EmailLogResponse(e.SentAt, e.ToEmail, e.Subject, e.Body, e.EventId)));
        }
    }
}
