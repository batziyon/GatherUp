using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using GatherUp.BL;
using GatherUp.Core.DO;

namespace GatherUp.API.DTOs
{
    public record PollQuestionRequest(
        [Required, MinLength(3)] string       QuestionText,
        [Required, MinLength(2)] List<string> Options
    );

    public record CreatePollRequest(
        [Required, MinLength(2)] string Name,
        [Required] List<PollQuestionRequest>  Questions,
        bool      IsPreliminary,
        DateTime? ClosingDate
    );

    public record SubmitVoteRequest(
        [Required] int    QuestionId,
        [Required] int    ParticipantId,
        [Required] string Answer
    );

    public record PollOptionStatResponse(string Option, int Count, double Percentage);

    public record PollQuestionResultResponse(
        int                          QuestionId,
        string                       QuestionText,
        List<PollOptionStatResponse> Stats
    );

    public record PollResponse(
        int                              Id,
        string                           Code,
        string                           Name,
        string?                          Description,
        bool                             IsPreliminary,
        DateTime?                        ClosingDate,
        bool                             IsOpen,
        List<PollQuestionResultResponse> QuestionResults
    );

    public static class PollMapper
    {
        public static PollResponse ToResponse(PollResults r, bool isOpen = true) => new(
            r.Poll.Id,
            r.Poll.Code,
            r.Poll.Name,
            r.Poll.Description,
            r.Poll.IsPreliminary,
            r.Poll.ClosingDate,
            isOpen,
            r.QuestionResults.Select(qr => new PollQuestionResultResponse(
                qr.Question.Id,
                qr.Question.QuestionText,
                qr.OptionStats.Select(s => new PollOptionStatResponse(s.Option, s.Count, s.Percentage)).ToList()
            )).ToList()
        );
    }
}
