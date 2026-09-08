using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GatherUp.Core.Exceptions;
using GatherUp.Core.DO;
using GatherUp.Core.Interfaces;

namespace GatherUp.BL
{
    public record PollResults(Poll Poll, IEnumerable<QuestionResult> QuestionResults);

    public record QuestionResult(
        PollQuestion Question,
        IEnumerable<(string Option, int Count, double Percentage)> OptionStats
    );

    public class PollService
    {
        private readonly IRepository<Poll>        _pollRepo;
        private readonly IRepository<Event>       _eventRepo;
        private readonly IRepository<Participant> _participantRepo;
        private readonly IEmailService            _emailService;
        private readonly IEventNotifier?          _notifier;

        public PollService(
            IRepository<Poll>        pollRepo,
            IRepository<Event>       eventRepo,
            IRepository<Participant> participantRepo,
            IEmailService            emailService,
            IEventNotifier?          notifier = null)
        {
            _pollRepo        = pollRepo;
            _eventRepo       = eventRepo;
            _participantRepo = participantRepo;
            _emailService    = emailService;
            _notifier        = notifier;
        }

        public async Task<Poll> CreatePollAsync(
            int eventId, string name,
            List<(string QuestionText, List<string> Options)> questions,
            bool isPreliminary = false,
            DateTime? closingDate = null)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new InvalidInputException(nameof(name), "poll name cannot be empty");
            if (questions == null || questions.Count == 0)
                throw new InvalidInputException(nameof(questions), "poll must have at least one question");
            if (closingDate.HasValue && closingDate.Value <= DateTime.UtcNow)
                throw new InvalidInputException(nameof(closingDate), "closing date must be in the future");

            var ev  = await _eventRepo.GetByIdAsync(eventId);
            var all = (await _pollRepo.GetAllAsync()).ToList();
            int newId = all.Any() ? all.Max(p => p.Id) + 1 : 1;

            var validatedQuestions = questions.Select((q, i) =>
            {
                if (string.IsNullOrWhiteSpace(q.QuestionText))
                    throw new InvalidInputException(nameof(q.QuestionText), "question text cannot be empty");
                if (q.Options == null || q.Options.Count < 2)
                    throw new InvalidInputException(nameof(q.Options), "each question must have at least two options");

                return new PollQuestion
                {
                    Id           = i + 1,
                    QuestionText = q.QuestionText,
                    Options      = q.Options
                };
            }).ToList();

            var poll = new Poll
            {
                Id            = newId,
                Code          = Guid.NewGuid().ToString("N")[..8].ToUpper(),
                Name          = name,
                IsPreliminary = isPreliminary,
                ClosingDate   = closingDate,
                Questions     = validatedQuestions
            };

            await _pollRepo.AddAsync(poll);

            if (isPreliminary)
            {
                ev.PreliminaryPollId = poll.Id;
            }

            if (!ev.PollIds.Contains(poll.Id))
                ev.PollIds.Add(poll.Id);

            if (ev.Status == EventStatus.Draft)
                ev.Status = EventStatus.Planning;

            await _eventRepo.UpdateAsync(ev);

            if (_notifier != null)
                await _notifier.OnPollCreatedAsync(poll.Id, eventId);

            return poll;
        }

        public async Task SubmitVoteAsync(int pollId, int questionId, int participantId, string answer)
        {
            await SubmitVoteAllowManagerAsync(pollId, questionId, participantId, answer, isManager: false);
        }

        public async Task SubmitVoteAllowManagerAsync(int pollId, int questionId, int participantId, string answer, bool isManager)
        {
            if (string.IsNullOrWhiteSpace(answer))
                throw new InvalidInputException(nameof(answer), "answer cannot be empty");

            var poll     = await _pollRepo.GetByIdAsync(pollId);
            if (poll.ClosingDate.HasValue && poll.ClosingDate.Value <= DateTime.UtcNow)
                throw new InvalidInputException(nameof(poll.ClosingDate), "poll is closed");

            var question = poll.Questions.FirstOrDefault(q => q.Id == questionId)
                ?? throw new EntityNotFoundException("PollQuestion", questionId);

            if (!question.Options.Contains(answer))
                throw new InvalidInputException(nameof(answer), "answer is not one of the allowed options");

            if (!isManager)
                await _participantRepo.GetByIdAsync(participantId);

            question.SetVote(participantId, answer);
            await _pollRepo.UpdateAsync(poll);

            if (_notifier != null)
                await _notifier.OnPollAnsweredAsync(participantId, pollId);

            await TryFinalizeEventAsync(pollId);
        }

        public async Task<PollResults> GetPollResultsAsync(int pollId)
        {
            var poll = await _pollRepo.GetByIdAsync(pollId);

            var results = poll.Questions.Select(q =>
            {
                int total = q.Votes.Count;
                var stats = q.Options.Select(opt =>
                {
                    int count  = q.Votes.Values.Count(a => a == opt);
                    double pct = total > 0 ? Math.Round((double)count / total * 100, 1) : 0;
                    return (opt, count, pct);
                });
                return new QuestionResult(q, stats);
            });

            return new PollResults(poll, results);
        }

        public async Task<bool> IsPollOpenAsync(int pollId)
        {
            var all = (await _pollRepo.GetAllAsync()).ToList();
            if (!all.Any(p => p.Id == pollId)) return false;
            var poll = all.First(p => p.Id == pollId);
            return poll.ClosingDate == null || poll.ClosingDate > DateTime.UtcNow;
        }

        public async Task<IEnumerable<Poll>> GetPollsByEventAsync(int eventId)
        {
            var ev = await _eventRepo.GetByIdAsync(eventId);
            return (await _pollRepo.GetAllAsync()).Where(p => ev.PollIds.Contains(p.Id));
        }

        private async Task TryFinalizeEventAsync(int pollId)
        {
            var ev = (await _eventRepo.GetAllAsync()).FirstOrDefault(e => e.PollIds.Contains(pollId));
            if (ev == null) return;

            var polls = (await _pollRepo.GetAllAsync()).Where(p => ev.PollIds.Contains(p.Id)).ToList();
            if (!polls.Any()) return;

            if (polls.All(p => p.ClosingDate.HasValue && p.ClosingDate.Value <= DateTime.UtcNow))
            {
                if (ev.Status != EventStatus.Finalized)
                {
                    ev.Status = EventStatus.Finalized;
                    await _eventRepo.UpdateAsync(ev);
                }
            }
        }

        private async Task NotifyPollCreatedAsync(Event ev, string pollName)
        {
            var participants = (await _participantRepo.GetAllAsync())
                .Where(p => ev.ParticipantIds.Contains(p.Id)
                    && p.MailingPreferences.Contains(MailingPreference.PollCreated))
                .ToList();

            await Task.WhenAll(participants.Select(p =>
                _emailService.SendAsync(p.Email,
                    $"סקר חדש: {pollName}",
                    $"שלום {p.Name}, נוצר סקר חדש. אנא מלא/י אותו.",
                    ev.Id)
                    .ContinueWith(t =>
                    {
                        if (t.IsFaulted)
                            System.Diagnostics.Debug.WriteLine(
                                $"Poll notify failed for {p.Email}: {t.Exception?.GetBaseException().Message}");
                    })));
        }
    }
}
