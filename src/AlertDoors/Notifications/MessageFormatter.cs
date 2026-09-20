using System.Text.RegularExpressions;
using AlertDoors.Jobs;
using AlertDoors.Sources;
using AlertDoors.State;
namespace AlertDoors.Notifications;
public static class MessageFormatter
{
    public static IReadOnlyList<NotificationBatch> Prepare(Channel channel, IReadOnlyList<JobPosting> jobs)
    {
        var batches = new List<NotificationBatch>();
        var current = new List<JobPosting>();
        var body = "";
        var limit = channel == Channel.Discord ? 1900 : 30000;
        foreach (var job in jobs.DistinctBy(j => j.Id))
        {
            if (!LinkedInPageParser.IsJobUri(job.Url) || job.Url.AbsoluteUri.Length > 1024)
                throw new InvalidOperationException("Invalid job URL for notification.");
            var title = Clip(job.Title, 180);
            var company = Clip(job.Company, 100);
            var location = Clip(job.Location, 120);
            if (channel == Channel.Discord)
            {
                title = EscapeMarkdown(title); company = EscapeMarkdown(company); location = EscapeMarkdown(location);
            }
            var mode = job.Mode == WorkMode.Unknown ? "Modalidade a confirmar" : job.Mode.ToString();
            var entry = $"{title}\n{company} | {location} | {mode}\n{job.Url.AbsoluteUri}\n";
            if (!Regex.IsMatch(LinkedInDetailParser.Normalize(job.Title + " " + job.SeniorityText), @"\b(junior|jr|pleno|pl|mid[ -]level)\b"))
                entry += "Senioridade a confirmar\n";
            if (job.PublishedAt is null) entry += "Publicação não informada\n";
            if (entry.Length > limit) throw new InvalidOperationException("Notification entry exceeds channel limit.");
            if (body.Length + entry.Length + 1 > limit || current.Count == 20) Flush();
            current.Add(job); body += entry + "\n";
        }
        Flush();
        return batches;
        void Flush()
        {
            if (current.Count == 0) return;
            var id = FirestoreStateStore.Hash(channel + ":" + string.Join(",", current.Select(j => j.Id).Order()));
            batches.Add(new(id, channel, current.ToArray(), $"AlertDoors — {current.Count} vagas .NET", body.TrimEnd()));
            current.Clear(); body = "";
        }
    }
    private static string Clip(string value, int max)
    {
        value = Regex.Replace(value, @"[\r\n\t]+", " ");
        if (value.Length <= max) return value;
        var length = char.IsHighSurrogate(value[max - 1]) ? max - 1 : max;
        return value[..length] + "…";
    }
    private static string EscapeMarkdown(string value) => Regex.Replace(value, @"([\\`*_~\[\]()<>])", @"\$1");
}
