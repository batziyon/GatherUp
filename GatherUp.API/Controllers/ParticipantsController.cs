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
    public class ParticipantsController : ControllerBase
    {
        private readonly ParticipantService _participantService;
        private readonly PersonService      _personService;

        public ParticipantsController(
            ParticipantService participantService,
            PersonService      personService)
        {
            _participantService = participantService;
            _personService      = personService;
        }

        [HttpGet("{id:int}")]
        [ProducesResponseType(typeof(ParticipantResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetById(int id) =>
            Ok(ParticipantMapper.ToResponse(await _personService.GetParticipantByIdAsync(id)));

        [HttpGet("event/{eventId:int}")]
        [ProducesResponseType(typeof(IEnumerable<ParticipantResponse>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetByEvent(int eventId) =>
            Ok((await _participantService.GetEventParticipantsAsync(eventId)).Select(ParticipantMapper.ToResponse));

        [HttpGet("byEmail")]
        [ProducesResponseType(typeof(ParticipantResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetByEmail([FromQuery] string email)
        {
            var p = await _participantService.GetByEmailAsync(email);
            return p is null
                ? NotFound(new { message = $"לא נמצא משתתף עם מייל '{email}'." })
                : Ok(ParticipantMapper.ToResponse(p));
        }

        [HttpPost("event/{eventId:int}")]
        [Authorize(Roles = "Manager")]
        [ProducesResponseType(typeof(ParticipantResponse), StatusCodes.Status201Created)]
        public async Task<IActionResult> Add(int eventId, [FromBody] CreateParticipantRequest request)
        {
            string baseUrl = $"{Request.Scheme}://{Request.Host}";

            var existing = await _participantService.GetByEmailAsync(request.Email);
            Core.DO.Participant participant;
            if (existing != null)
            {
                participant = existing;
                await _participantService.AddParticipantToEventAsync(eventId, participant, baseUrl);
            }
            else
            {
                var allParticipants = (await _personService.GetAllParticipantsAsync()).ToList();
                int newId = allParticipants.Any() ? allParticipants.Max(p => p.Id) + 1 : 1;
                participant = ParticipantMapper.ToEntity(request, newId);
                await _participantService.AddParticipantToEventAsync(eventId, participant, baseUrl);
            }

            return CreatedAtAction(nameof(GetById), new { id = participant.Id },
                ParticipantMapper.ToResponse(participant));
        }

        [HttpPut("{id:int}")]
        [ProducesResponseType(typeof(ParticipantResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateParticipantRequest request)
        {
            if (!IsManagerRole() && GetCallerId() != id)
                return Forbid();

            await _personService.UpdateParticipantPreferencesAsync(id, request.Name, request.Email, request.MailingPreferences);
            return Ok(ParticipantMapper.ToResponse(await _personService.GetParticipantByIdAsync(id)));
        }

        [HttpDelete("{id:int}")]
        [Authorize(Roles = "Manager")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Delete(int id)
        {
            await _personService.DeleteParticipantAsync(id);
            return NoContent();
        }

        [HttpPut("{participantId:int}/attendance")]
        [ProducesResponseType(typeof(ParticipantResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> ConfirmAttendance(
            int participantId,
            [FromQuery] int eventId,
            [FromBody] ConfirmAttendanceRequest request)
        {
            if (request.IsAttending is null)
                return BadRequest(new { message = "isAttending is required (true/false)" });

            if (!IsManagerRole() && GetCallerId() != participantId)
                return Forbid();

            await _participantService.ConfirmAttendanceAsync(
                participantId, eventId, request.IsAttending.Value, request.SelectedPreferences);

            return Ok(ParticipantMapper.ToResponse(await _personService.GetParticipantByIdAsync(participantId)));
        }

        [HttpPut("{participantId:int}/attendance/public")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(ParticipantResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> ConfirmAttendancePublic(
            int participantId,
            [FromQuery] int eventId,
            [FromBody] ConfirmAttendanceRequest request)
        {
            if (request.IsAttending is null)
                return BadRequest(new { message = "isAttending is required (true/false)" });

            if (participantId <= 0)
                return BadRequest(new { message = "participantId is required and must be greater than zero." });

            if (eventId <= 0)
                return BadRequest(new { message = "eventId is required and must be greater than zero." });

            await _participantService.ConfirmAttendanceAsync(
                participantId, eventId, request.IsAttending.Value, request.SelectedPreferences);

            return Ok(ParticipantMapper.ToResponse(await _personService.GetParticipantByIdAsync(participantId)));
        }

        private int  GetCallerId()   => int.TryParse(User.FindFirstValue("userId"), out int id) ? id : 0;
        private bool IsManagerRole() => User.IsInRole("Manager");
    }
}
