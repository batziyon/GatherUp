using GatherUp.API.DTOs;
using GatherUp.BL;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GatherUp.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class EventHostController : ControllerBase
    {
        private readonly EventService  _eventService;
        private readonly PersonService _personService;

        public EventHostController(EventService eventService, PersonService personService)
        {
            _eventService  = eventService;
            _personService = personService;
        }

        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<EventHostResponse>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAll() =>
            Ok((await _eventService.GetAllHostsAsync()).Select(EventMapper.ToHostResponse));

        [HttpGet("{id:int}")]
        [ProducesResponseType(typeof(EventHostResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetById(int id)
        {
            try
            {
                var host = await _eventService.GetHostByIdAsync(id);
                return Ok(EventMapper.ToHostResponse(host));
            }
            catch { }

            var manager = await _personService.GetManagerByIdAsync(id);
            if (manager != null)
                return Ok(new EventHostResponse(manager.Id, manager.Name, manager.Email));

            var participant = await _personService.TryGetParticipantByIdAsync(id);
            if (participant != null)
                return Ok(new EventHostResponse(participant.Id, participant.Name, participant.Email));

            return NotFound(new { message = $"לא נמצא בעל אירוע עם מזהה {id}" });
        }

        [HttpGet("byEmail")]
        [ProducesResponseType(typeof(EventHostResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetByEmail([FromQuery] string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return BadRequest(new { message = "נדרש מייל." });

            var host = await _personService.GetHostByEmailAsync(email);
            if (host != null)
                return Ok(EventMapper.ToHostResponse(host));

            var manager = await _personService.GetManagerByEmailAsync(email);
            if (manager != null)
                return Ok(new EventHostResponse(manager.Id, manager.Name, manager.Email));

            var participant = await _personService.GetParticipantByEmailAsync(email);
            if (participant != null)
                return Ok(new EventHostResponse(participant.Id, participant.Name, participant.Email));

            return NotFound(new { message = $"לא נמצא משתמש עם מייל '{email}'." });
        }

        [HttpGet("byName")]
        [ProducesResponseType(typeof(IEnumerable<EventHostResponse>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetByName([FromQuery] string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return BadRequest(new { message = "נדרש שם לחיפוש." });

            var results = new List<EventHostResponse>();

            var hosts = await _personService.SearchHostsByNameAsync(name);
            results.AddRange(hosts.Select(EventMapper.ToHostResponse));

            var managers = await _personService.SearchManagersByNameAsync(name);
            foreach (var m in managers)
            {
                if (!results.Any(r => r.Id == m.Id))
                    results.Add(new EventHostResponse(m.Id, m.Name, m.Email));
            }

            var participants = await _personService.SearchParticipantsByNameAsync(name);
            foreach (var p in participants)
            {
                if (!results.Any(r => r.Id == p.Id))
                    results.Add(new EventHostResponse(p.Id, p.Name, p.Email));
            }

            return Ok(results);
        }
    }
}
