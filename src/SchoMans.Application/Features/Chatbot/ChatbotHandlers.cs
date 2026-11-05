using FluentValidation;
using Microsoft.EntityFrameworkCore;
using SchoMans.Application.Common.Errors;
using SchoMans.Application.Common.Interfaces;
using SchoMans.Application.Features.Ai;
using SchoMans.Application.Features.Announcements;
using SchoMans.Domain.Communication;

namespace SchoMans.Application.Features.Chatbot;

public sealed record AskChatbotCommand(string Question);

public sealed record ChatbotSourceDto(string Title, string Url);

/// <param name="Known">False when the published sources do not contain the answer: then <paramref name="Answer"/> says so and <paramref name="Contact"/> tells who to ask.</param>
public sealed record ChatbotAnswerDto(bool Known, string Answer, List<ChatbotSourceDto> Sources, string? Contact);

public sealed class AskChatbotValidator : AbstractValidator<AskChatbotCommand>
{
    public const int QuestionMaxLength = 300;

    public AskChatbotValidator() => RuleFor(x => x.Question).NotEmpty().MaximumLength(QuestionMaxLength);
}

/// <summary>
/// Finds the passages in the allowed sources, asks the AI to answer from them only, and gives the answer with links to its sources.
/// When nothing is found, or the AI cannot answer from what was found, the answer is "I don't know" with the staff contact: the
/// AI is not even asked when no passage matches, so a question outside the sources can never be answered from outside knowledge.
/// </summary>
public sealed class ChatbotAnswerer(AiGateway gateway, ISchoolContact school, IHtmlContentSanitizer sanitizer)
{
    public async Task<Result<ChatbotAnswerDto>> AnswerAsync(
        Guid? userId, string question, IQueryable<NewsArticle> news, IQueryable<Announcement> announcements, CancellationToken ct)
    {
        question = question.Trim();
        var keywords = SourceSearch.Keywords(question);
        var passages = keywords.Count == 0 ? [] : await SourceSearch.SearchAsync(news, announcements, keywords, sanitizer, ct);
        if (passages.Count == 0)
        {
            return Unknown();
        }

        var completion = await gateway.RunAsync(userId, ChatbotPrompt.Build(question, passages), ct);
        if (!completion.IsSuccess)
        {
            return completion.Error;
        }

        var parsed = ChatbotPrompt.Parse(completion.Value.Text, passages.Count);
        if (!parsed.IsSuccess)
        {
            return parsed.Error;
        }

        var (answer, sources) = parsed.Value;
        return answer is null
            ? Unknown()
            : new ChatbotAnswerDto(true, answer, sources.Select(n => new ChatbotSourceDto(passages[n - 1].Title, passages[n - 1].Url)).ToList(), null);
    }

    private ChatbotAnswerDto Unknown() =>
        new(false, $"I could not find this in the information the school has published, so I would rather not guess. Please ask the school staff: {school.Contact}", [], school.Contact);
}

/// <summary>The landing-page chatbot for anybody: published news (including admission information) and announcements marked public.</summary>
public sealed class AskPublicChatbotHandler(IAppDbContext db, ChatbotAnswerer answerer)
{
    public Task<Result<ChatbotAnswerDto>> Handle(AskChatbotCommand command, CancellationToken ct) =>
        answerer.AnswerAsync(
            null,
            command.Question,
            db.NewsArticles.AsNoTracking().Where(n => n.Status == PublishStatus.Published),
            db.Announcements.AsNoTracking().Where(a => a.Status == PublishStatus.Published && a.IsPublic),
            ct);
}

/// <summary>
/// The chatbot on the parent dashboard: the same news, and the published announcements meant for this parent (their role or their
/// children's classrooms), not only the public ones. It counts as one of the parent's daily AI calls.
/// </summary>
public sealed class AskParentChatbotHandler(IAppDbContext db, ICurrentUser currentUser, ChatbotAnswerer answerer)
{
    public async Task<Result<ChatbotAnswerDto>> Handle(AskChatbotCommand command, CancellationToken ct)
    {
        var classroomIds = await AnnouncementAudience.ClassroomIdsOfAsync(db, currentUser, ct);
        return await answerer.AnswerAsync(
            currentUser.UserId,
            command.Question,
            db.NewsArticles.AsNoTracking().Where(n => n.Status == PublishStatus.Published),
            db.Announcements.AsNoTracking().Where(a => a.Status == PublishStatus.Published).Where(AnnouncementAudience.VisibleTo(currentUser.Role, classroomIds)),
            ct);
    }
}
