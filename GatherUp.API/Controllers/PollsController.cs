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
    public class PollsController : ControllerBase
    {
        private readonly PollService _pollService;

        public PollsController(PollService pollService)
        {
            _pollService = pollService;
        }

        [HttpPost("event/{eventId:int}")]
        [Authorize(Roles = "Manager")]
        [ProducesResponseType(typeof(PollResponse), StatusCodes.Status201Created)]
        public async Task<IActionResult> Create(int eventId, [FromBody] CreatePollRequest request)
        {
            var questions = request.Questions
                .Select(q => (q.QuestionText, q.Options))
                .ToList();

            var poll   = await _pollService.CreatePollAsync(eventId, request.Name, questions,
                request.IsPreliminary, request.ClosingDate);
            var isOpen = await _pollService.IsPollOpenAsync(poll.Id);
            var result = PollMapper.ToResponse(await _pollService.GetPollResultsAsync(poll.Id), isOpen);

            return CreatedAtAction(nameof(GetResults), new { pollId = poll.Id }, result);
        }

        [HttpGet("event/{eventId:int}")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(IEnumerable<PollResponse>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetByEvent(int eventId)
        {
            var polls = await _pollService.GetPollsByEventAsync(eventId);
            var results = await Task.WhenAll(polls.Select(async poll =>
            {
                var isOpen = await _pollService.IsPollOpenAsync(poll.Id);
                return PollMapper.ToResponse(await _pollService.GetPollResultsAsync(poll.Id), isOpen);
            }));
            return Ok(results);
        }

        [HttpGet("{pollId:int}")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(PollResponse), StatusCodes.Status200OK)]
        public async Task<IActionResult> Get(int pollId)
        {
            var isOpen = await _pollService.IsPollOpenAsync(pollId);
            return Ok(PollMapper.ToResponse(await _pollService.GetPollResultsAsync(pollId), isOpen));
        }

        [HttpGet("{pollId:int}/open")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
        public async Task<IActionResult> IsOpen(int pollId) =>
            Ok(await _pollService.IsPollOpenAsync(pollId));

        [HttpGet("{pollId:int}/results")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(PollResponse), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetResults(int pollId)
        {
            var isOpen = await _pollService.IsPollOpenAsync(pollId);
            return Ok(PollMapper.ToResponse(await _pollService.GetPollResultsAsync(pollId), isOpen));
        }

        [HttpPost("{pollId:int}/vote")]
        [ProducesResponseType(typeof(PollResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> Vote(int pollId, [FromBody] SubmitVoteRequest request)
        {
            if (!User.IsInRole("Manager") && GetCallerId() != request.ParticipantId)
                return Forbid();

            await _pollService.SubmitVoteAllowManagerAsync(
                pollId, request.QuestionId, request.ParticipantId, request.Answer,
                User.IsInRole("Manager"));

            var isOpen = await _pollService.IsPollOpenAsync(pollId);
            return Ok(PollMapper.ToResponse(await _pollService.GetPollResultsAsync(pollId), isOpen));
        }

        private int GetCallerId() =>
            int.TryParse(User.FindFirstValue("userId"), out int id) ? id : 0;
    }
}
