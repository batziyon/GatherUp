using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using GatherUp.Core.DO;
using GatherUp.Core.Interfaces;

namespace GatherUp.Infrastructure.Data
{
    public class FileEmailService : IEmailService
    {
        private readonly string _logFilePath;
        private readonly string _structuredLogPath;

        private static readonly SemaphoreSlim _writeLock = new(1, 1);

        public FileEmailService(string logFolder)
        {
            if (!Directory.Exists(logFolder))
                Directory.CreateDirectory(logFolder);

            _logFilePath       = Path.Combine(logFolder, "emails.log");
            _structuredLogPath = Path.Combine(logFolder, "emails_structured.jsonl");
        }

        public async Task SendAsync(string toEmail, string subject, string body, int? eventId = null)
        {
            if (string.IsNullOrWhiteSpace(toEmail))
                throw new ArgumentException("Email recipient cannot be empty", nameof(toEmail));
            if (string.IsNullOrWhiteSpace(subject))
                throw new ArgumentException("Email subject cannot be empty", nameof(subject));
            if (string.IsNullOrWhiteSpace(body))
                throw new ArgumentException("Email body cannot be empty", nameof(body));

            var now        = DateTime.Now;
            string safeEmail   = toEmail.Replace("\n", " ").Replace("\r", " ");
            string safeSubject = subject.Replace("\n", " ").Replace("\r", " ");
            string safeBody    = body.Replace("\r", " ");

            await _writeLock.WaitAsync();
            try
            {
                string textEntry =
                    $"[{now:yyyy-MM-dd HH:mm:ss}] To: {safeEmail} | Subject: {safeSubject}\n" +
                    $"{safeBody}\n{new string('-', 80)}\n";
                await File.AppendAllTextAsync(_logFilePath, textEntry);

                var entry = new EmailLogEntry
                {
                    SentAt  = now,
                    ToEmail = safeEmail,
                    Subject = safeSubject,
                    Body    = safeBody,
                    EventId = eventId
                };
                string jsonLine = JsonSerializer.Serialize(entry) + "\n";
                await File.AppendAllTextAsync(_structuredLogPath, jsonLine);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to send email to {toEmail}", ex);
            }
            finally
            {
                _writeLock.Release();
            }
        }

        public async Task<IEnumerable<EmailLogEntry>> GetAllLogsAsync()
        {
            var structured = await ReadStructuredLogsAsync();

            var textEntries = await ParseTextLogAsync();

            var mergedByKey = new Dictionary<string, EmailLogEntry>(StringComparer.OrdinalIgnoreCase);

            foreach (var e in structured)
            {
                var key = $"{e.ToEmail}|{e.SentAt:yyyy-MM-dd HH:mm}";
                mergedByKey[key] = e;
            }

            foreach (var e in textEntries)
            {
                var key = $"{e.ToEmail}|{e.SentAt:yyyy-MM-dd HH:mm}";
                if (!mergedByKey.ContainsKey(key))
                    mergedByKey[key] = e;
            }

            return mergedByKey.Values.OrderByDescending(e => e.SentAt);
        }

        private async Task<List<EmailLogEntry>> ReadStructuredLogsAsync()
        {
            var result = new List<EmailLogEntry>();
            if (!File.Exists(_structuredLogPath)) return result;

            await _writeLock.WaitAsync();
            try
            {
                var lines = await File.ReadAllLinesAsync(_structuredLogPath);
                foreach (var line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    try
                    {
                        var entry = JsonSerializer.Deserialize<EmailLogEntry>(line);
                        if (entry != null) result.Add(entry);
                    }
                    catch { }
                }
            }
            finally
            {
                _writeLock.Release();
            }
            return result;
        }

        private async Task<List<EmailLogEntry>> ParseTextLogAsync()
        {
            var result = new List<EmailLogEntry>();
            if (!File.Exists(_logFilePath)) return result;

            string content;
            await _writeLock.WaitAsync();
            try { content = await File.ReadAllTextAsync(_logFilePath); }
            finally { _writeLock.Release(); }

            var blocks = System.Text.RegularExpressions.Regex.Split(
                content, @"-{20,}\r?\n");

            foreach (var block in blocks)
            {
                var trimmed = block.Trim();
                if (string.IsNullOrEmpty(trimmed)) continue;

                var firstNewline = trimmed.IndexOf('\n');
                var headerLine   = firstNewline > 0 ? trimmed[..firstNewline].Trim() : trimmed;
                var body         = firstNewline > 0 ? trimmed[(firstNewline + 1)..].Trim() : string.Empty;

                var tsMatch = System.Text.RegularExpressions.Regex.Match(
                    headerLine, @"\[(\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2})\]");
                if (!tsMatch.Success) continue;
                if (!DateTime.TryParse(tsMatch.Groups[1].Value, out var sentAt)) continue;

                var emailMatch = System.Text.RegularExpressions.Regex.Match(
                    headerLine, @"To:\s*([^\|]+)");
                if (!emailMatch.Success) continue;
                var toEmail = emailMatch.Groups[1].Value.Trim();

                var subjectMatch = System.Text.RegularExpressions.Regex.Match(
                    headerLine, @"Subject:\s*(.+)$");
                var subject = subjectMatch.Success ? subjectMatch.Groups[1].Value.Trim() : string.Empty;

                int? eventId = null;
                var eventIdMatch = System.Text.RegularExpressions.Regex.Match(
                    body, @"eventId=(\d+)");
                if (eventIdMatch.Success && int.TryParse(eventIdMatch.Groups[1].Value, out var eid))
                    eventId = eid;

                result.Add(new EmailLogEntry
                {
                    SentAt  = sentAt,
                    ToEmail = toEmail,
                    Subject = subject,
                    Body    = body,
                    EventId = eventId
                });
            }

            return result;
        }
    }
}
