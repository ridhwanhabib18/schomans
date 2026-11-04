using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using SchoMans.Application.Common.Interfaces;
using SchoMans.Domain.Communication;

namespace SchoMans.Application.Features.Chatbot;

/// <summary>A piece of a published source, as the AI gets it and as the person gets the link to it.</summary>
public sealed record ChatbotPassage(string Title, string Url, string Text);

/// <summary>
/// Finds the passages that may answer a question (AI-08). The SQL Server image has no full-text component, so the search is a keyword
/// score: one LIKE query per keyword over the allowed sources (a hit in the title counts 3, in the text 1), the best
/// <see cref="MaxPassages"/> win. Only the sources the caller hands in are searched, so what a person may not read is never found.
/// </summary>
internal static partial class SourceSearch
{
    public const int MaxPassages = 5;
    public const int MaxKeywords = 8;
    private const int PassageLength = 700;

    private static readonly HashSet<string> StopWords = new(StringComparer.Ordinal)
    {
        "the", "and", "for", "are", "was", "were", "you", "your", "our", "what", "when", "where", "which", "who", "whom", "how", "why",
        "can", "could", "would", "should", "will", "does", "did", "has", "have", "had", "this", "that", "these", "those", "there", "their",
        "with", "about", "from", "into", "than", "then", "also", "any", "all", "not", "but", "may", "need", "want", "tell", "please", "school",
        "is", "of", "to", "in", "on", "at", "it", "be", "do", "me", "my", "we", "us", "an", "or", "if", "as", "by",
    };

    [GeneratedRegex("[a-z0-9]+")]
    private static partial Regex WordPattern();

    /// <summary>The words of a question that can be looked up: lower case, at least 3 letters, no filler words, no repeats.</summary>
    public static List<string> Keywords(string question) =>
        WordPattern().Matches(question.ToLowerInvariant()).Select(m => m.Value)
            .Where(w => w.Length >= 3 && !StopWords.Contains(w))
            .Distinct(StringComparer.Ordinal).Take(MaxKeywords).ToList();

    public static async Task<List<ChatbotPassage>> SearchAsync(
        IQueryable<NewsArticle> news, IQueryable<Announcement> announcements, IReadOnlyList<string> keywords, IHtmlContentSanitizer sanitizer, CancellationToken ct)
    {
        var newsScores = new Dictionary<Guid, int>();
        var announcementScores = new Dictionary<Guid, int>();
        foreach (var keyword in keywords)
        {
            var k = keyword;
            foreach (var hit in await news.Where(n => n.Title.Contains(k) || n.Body.Contains(k)).Select(n => new { n.Id, Title = n.Title.Contains(k) }).ToListAsync(ct))
            {
                newsScores[hit.Id] = newsScores.GetValueOrDefault(hit.Id) + (hit.Title ? 3 : 1);
            }

            foreach (var hit in await announcements.Where(a => a.Title.Contains(k) || a.Body.Contains(k)).Select(a => new { a.Id, Title = a.Title.Contains(k) }).ToListAsync(ct))
            {
                announcementScores[hit.Id] = announcementScores.GetValueOrDefault(hit.Id) + (hit.Title ? 3 : 1);
            }
        }

        var best = newsScores.Select(s => (Kind: 'n', s.Key, Score: s.Value))
            .Concat(announcementScores.Select(s => (Kind: 'a', s.Key, Score: s.Value)))
            .OrderByDescending(s => s.Score).ThenBy(s => s.Key)
            .Take(MaxPassages).ToList();

        var newsIds = best.Where(b => b.Kind == 'n').Select(b => b.Key).ToList();
        var announcementIds = best.Where(b => b.Kind == 'a').Select(b => b.Key).ToList();
        var newsRows = (await news.Where(n => newsIds.Contains(n.Id)).Select(n => new { n.Id, n.Title, n.Slug, n.Body }).ToListAsync(ct)).ToDictionary(n => n.Id);
        var announcementRows = (await announcements.Where(a => announcementIds.Contains(a.Id)).Select(a => new { a.Id, a.Title, a.Body, a.IsPublic }).ToListAsync(ct)).ToDictionary(a => a.Id);

        var passages = new List<ChatbotPassage>();
        foreach (var (kind, id, _) in best)
        {
            if (kind == 'n' && newsRows.TryGetValue(id, out var article))
            {
                passages.Add(new ChatbotPassage(article.Title, $"/news/{article.Slug}", Window(sanitizer.ToPlainText(article.Body), keywords)));
            }
            else if (kind == 'a' && announcementRows.TryGetValue(id, out var announcement))
            {
                var url = announcement.IsPublic ? $"/announcements/public/{announcement.Id}" : $"/announcements/{announcement.Id}";
                passages.Add(new ChatbotPassage(announcement.Title, url, Window(sanitizer.ToPlainText(announcement.Body), keywords)));
            }
        }

        return passages;
    }

    /// <summary>The stretch of text around the first keyword (or the start): a passage, not the whole article.</summary>
    internal static string Window(string text, IReadOnlyList<string> keywords)
    {
        text = WhitespacePattern().Replace(text, " ").Trim();
        if (text.Length <= PassageLength)
        {
            return text;
        }

        var first = keywords.Select(k => text.IndexOf(k, StringComparison.OrdinalIgnoreCase)).Where(i => i >= 0).DefaultIfEmpty(0).Min();
        var start = Math.Max(0, Math.Min(first - 200, text.Length - PassageLength));
        return text.Substring(start, PassageLength).Trim();
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespacePattern();
}
