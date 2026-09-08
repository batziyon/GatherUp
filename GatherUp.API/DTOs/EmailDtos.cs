using System;
using System.Collections.Generic;

namespace GatherUp.API.DTOs
{
    public record EmailLogResponse(
        DateTime SentAt,
        string   ToEmail,
        string   Subject,
        string   Body,
        int?     EventId
    );

    public record EmailLogLinesResponse(List<string> Lines);
}
