using System.Collections.Generic;
using System.Xml.Serialization;

namespace GatherUp.Core.DO
{
    public class PollQuestion
    {
        [XmlAttribute("id")]
        public int Id { get; set; }

        public string       QuestionText       { get; set; } = string.Empty;
        public List<string> Options            { get; set; } = new();
        public List<PollResponse> ParticipantAnswers { get; set; } = new();

        [XmlIgnore]
        public Dictionary<int, string> Votes
        {
            get
            {
                var d = new Dictionary<int, string>();
                foreach (var r in ParticipantAnswers)
                    d[r.ParticipantId] = r.Answer;
                return d;
            }
        }

        public void SetVote(int participantId, string answer)
        {
            var existing = ParticipantAnswers.Find(r => r.ParticipantId == participantId);
            if (existing != null)
                existing.Answer = answer;
            else
                ParticipantAnswers.Add(new PollResponse { ParticipantId = participantId, Answer = answer });
        }
    }
}
