using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using SchoMans.Api.IntegrationTests.Infrastructure;
using SchoMans.Application.Common;
using SchoMans.Application.Features.QuestionBank;
using SchoMans.Domain.Assignments;
using SchoMans.Infrastructure.Persistence;

namespace SchoMans.Api.IntegrationTests.Endpoints;

/// <summary>
/// The test set of AI-06, first half (question bank): a teacher uploads a PDF, the AI drafts questions from its text, the teacher edits and
/// saves them to the bank of a subject they teach. Nothing is saved before the teacher saves it. The AI provider is the deterministic mock.
/// </summary>
[Collection(ApiCollection.Name)]
public class QuestionBankEndpointTests(ApiFixture fixture)
{
    private const string Url = "/api/v1/question-bank";

    private static readonly string[] Material =
    [
        "Photosynthesis is the process by which plants make food from light.",
        "Chlorophyll in the leaves absorbs the sunlight that the plant needs.",
        "Plants take in carbon dioxide and release oxygen during photosynthesis.",
        "Water is carried from the roots to the leaves through the stem.",
    ];

    /// <summary>A real PDF with the given text lines on each page.</summary>
    private static byte[] Pdf(int pages = 1, IEnumerable<string>? lines = null)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        var text = (lines ?? Material).ToList();
        return Document.Create(container =>
        {
            for (var page = 0; page < pages; page++)
            {
                container.Page(p => p.Content().Column(column =>
                {
                    foreach (var line in text)
                    {
                        column.Item().Text(line);
                    }
                }));
            }
        }).GeneratePdf();
    }

    private static MultipartFormDataContent Form(Guid subjectId, byte[] file, string fileName = "material.pdf", int count = 3, string difficulty = "Easy", string type = "MultipleChoice")
    {
        var content = new MultipartFormDataContent
        {
            { new StringContent(subjectId.ToString()), "subjectId" },
            { new StringContent(count.ToString()), "count" },
            { new StringContent(difficulty), "difficulty" },
            { new StringContent(type), "type" },
        };
        var part = new ByteArrayContent(file);
        part.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        content.Add(part, "file", fileName);
        return content;
    }

    private static QuestionInput Choice(string body = "What is 2 + 2?", int key = 1) =>
        new(QuestionType.MultipleChoice, QuestionDifficulty.Easy, body, ["3", "4", "5"], key.ToString(), "Because.");

    private static async Task<List<QuestionDto>> SaveAsync(HttpClient teacher, Guid subjectId, params QuestionInput[] questions)
    {
        var response = await teacher.PostJsonAsync(Url, new SaveQuestionsCommand(subjectId, [.. questions]));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await response.ReadAsync<List<QuestionDto>>();
    }

    private async Task<int> QuestionCountAsync()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Questions.CountAsync();
    }

    // ---- generating ----

    [Fact]
    public async Task A_teacher_gets_drafts_from_the_text_of_a_pdf_and_nothing_is_saved()
    {
        var scenario = await ClassroomScenario.CreateAsync(fixture.Factory, 1);
        using var teacher = scenario.TeacherClient();
        var before = await QuestionCountAsync();

        var response = await teacher.PostAsync($"{Url}/generate", Form(scenario.Subject.Id, Pdf(), count: 3));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var drafts = await response.ReadAsync<GeneratedQuestionsDto>();
        Assert.Equal((1, false), (drafts.Pages, drafts.Truncated));
        Assert.Equal(3, drafts.Questions.Count);
        Assert.All(drafts.Questions, q =>
        {
            Assert.Equal((QuestionType.MultipleChoice, QuestionDifficulty.Easy), (q.Type, q.Difficulty));
            Assert.Equal(4, q.Options.Count);
            Assert.InRange(int.Parse(q.AnswerKey), 0, 3);
        });
        Assert.Contains(drafts.Questions, q => q.Options.Any(o => o.Contains("Photosynthesis", StringComparison.Ordinal)));
        Assert.Equal(before, await QuestionCountAsync());
    }

    [Fact]
    public async Task Short_answer_drafts_have_a_model_answer_and_no_options()
    {
        var scenario = await ClassroomScenario.CreateAsync(fixture.Factory, 1);
        using var teacher = scenario.TeacherClient();

        var response = await teacher.PostAsync($"{Url}/generate", Form(scenario.Subject.Id, Pdf(), count: 2, difficulty: "Hard", type: "ShortAnswer"));

        var drafts = await response.ReadAsync<GeneratedQuestionsDto>();
        Assert.Equal(2, drafts.Questions.Count);
        Assert.All(drafts.Questions, q =>
        {
            Assert.Equal((QuestionType.ShortAnswer, QuestionDifficulty.Hard), (q.Type, q.Difficulty));
            Assert.Empty(q.Options);
            Assert.False(string.IsNullOrWhiteSpace(q.AnswerKey));
        });
    }

    [Fact]
    public async Task A_pdf_of_more_than_thirty_pages_is_refused()
    {
        var scenario = await ClassroomScenario.CreateAsync(fixture.Factory, 1);
        using var teacher = scenario.TeacherClient();

        var response = await teacher.PostAsync($"{Url}/generate", Form(scenario.Subject.Id, Pdf(pages: 31)));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("questions.pdf_too_long", await response.ProblemCodeAsync());
    }

    [Fact]
    public async Task A_pdf_of_exactly_thirty_pages_is_accepted()
    {
        var scenario = await ClassroomScenario.CreateAsync(fixture.Factory, 1);
        using var teacher = scenario.TeacherClient();

        var response = await teacher.PostAsync($"{Url}/generate", Form(scenario.Subject.Id, Pdf(pages: 30)));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(30, (await response.ReadAsync<GeneratedQuestionsDto>()).Pages);
    }

    [Fact]
    public async Task A_pdf_without_text_such_as_a_scan_is_refused_without_asking_the_ai()
    {
        var scenario = await ClassroomScenario.CreateAsync(fixture.Factory, 1);
        using var teacher = scenario.TeacherClient();
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var before = await db.AiUsageLogs.CountAsync(l => l.Feature == "AI-06");

        var response = await teacher.PostAsync($"{Url}/generate", Form(scenario.Subject.Id, Pdf(lines: [])));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("questions.pdf_no_text", await response.ProblemCodeAsync());
        Assert.Equal(before, await db.AiUsageLogs.CountAsync(l => l.Feature == "AI-06"));
    }

    [Fact]
    public async Task A_file_that_is_not_a_pdf_is_refused()
    {
        var scenario = await ClassroomScenario.CreateAsync(fixture.Factory, 1);
        using var teacher = scenario.TeacherClient();

        var notPdf = await teacher.PostAsync($"{Url}/generate", Form(scenario.Subject.Id, Encoding.UTF8.GetBytes("just some text, not a pdf")));
        var wrongName = await teacher.PostAsync($"{Url}/generate", Form(scenario.Subject.Id, Pdf(), fileName: "material.docx"));

        Assert.Equal(HttpStatusCode.BadRequest, notPdf.StatusCode);
        Assert.Equal("questions.pdf_invalid", await notPdf.ProblemCodeAsync());
        Assert.Equal(HttpStatusCode.BadRequest, wrongName.StatusCode);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(16)]
    public async Task The_number_of_questions_is_between_one_and_fifteen(int count)
    {
        var scenario = await ClassroomScenario.CreateAsync(fixture.Factory, 1);
        using var teacher = scenario.TeacherClient();

        var response = await teacher.PostAsync($"{Url}/generate", Form(scenario.Subject.Id, Pdf(), count: count));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task A_teacher_cannot_generate_for_a_subject_they_do_not_teach()
    {
        var scenario = await ClassroomScenario.CreateAsync(fixture.Factory, 1);
        var other = await ClassroomScenario.CreateAsync(fixture.Factory, 1, subjectName: "English");
        using var teacher = scenario.TeacherClient();

        var response = await teacher.PostAsync($"{Url}/generate", Form(other.Subject.Id, Pdf()));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("questions.not_your_subject", await response.ProblemCodeAsync());
    }

    [Fact]
    public async Task A_draft_uses_one_daily_ai_call_of_the_teacher()
    {
        var scenario = await ClassroomScenario.CreateAsync(fixture.Factory, 1);
        using var teacher = scenario.TeacherClient();
        (await teacher.PostAsync($"{Url}/generate", Form(scenario.Subject.Id, Pdf()))).EnsureSuccessStatusCode();
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var userId = await db.Users.Where(u => u.Email == scenario.Teacher.Email).Select(u => u.Id).SingleAsync();

        Assert.Equal(1, await db.AiUsageLogs.CountAsync(l => l.UserId == userId && l.Feature == "AI-06" && !l.IsBulk));
    }

    // ---- the bank ----

    [Fact]
    public async Task A_teacher_saves_questions_and_lists_them_with_the_filters()
    {
        var scenario = await ClassroomScenario.CreateAsync(fixture.Factory, 1);
        using var teacher = scenario.TeacherClient();
        var hard = new QuestionInput(QuestionType.ShortAnswer, QuestionDifficulty.Hard, "Why is the sky blue?", [], "Scattering", "Light scatters.");

        var saved = await SaveAsync(teacher, scenario.Subject.Id, Choice("Easy sum?"), hard);

        Assert.Equal(2, saved.Count);
        Assert.All(saved, q => Assert.True(q.IsMine));
        var all = await (await teacher.GetAsync($"{Url}?subjectId={scenario.Subject.Id}&pageSize=100")).ReadAsync<PagedResult<QuestionDto>>();
        Assert.Contains(all.Items, q => q.Body == "Easy sum?" && q.Options.SequenceEqual(["3", "4", "5"]) && q.AnswerKey == "1");
        var onlyHard = await (await teacher.GetAsync($"{Url}?subjectId={scenario.Subject.Id}&difficulty=Hard")).ReadAsync<PagedResult<QuestionDto>>();
        Assert.Equal(["Why is the sky blue?"], onlyHard.Items.Select(q => q.Body));
        var shortOnes = await (await teacher.GetAsync($"{Url}?subjectId={scenario.Subject.Id}&type=ShortAnswer&search=sky")).ReadAsync<PagedResult<QuestionDto>>();
        Assert.Single(shortOnes.Items);
        var none = await (await teacher.GetAsync($"{Url}?subjectId={scenario.Subject.Id}&search=nothing-like-this")).ReadAsync<PagedResult<QuestionDto>>();
        Assert.Empty(none.Items);
    }

    [Fact]
    public async Task Invalid_questions_are_a_validation_error_and_nothing_is_saved()
    {
        var scenario = await ClassroomScenario.CreateAsync(fixture.Factory, 1);
        using var teacher = scenario.TeacherClient();
        var before = await QuestionCountAsync();

        QuestionInput[][] cases =
        [
            [],
            [Choice(body: " ")],
            [Choice(key: 3)],
            [new QuestionInput(QuestionType.MultipleChoice, QuestionDifficulty.Easy, "Q?", ["only"], "0", "")],
            [new QuestionInput(QuestionType.MultipleChoice, QuestionDifficulty.Easy, "Q?", ["a", "A"], "0", "")],
            [new QuestionInput(QuestionType.ShortAnswer, QuestionDifficulty.Easy, "Q?", ["a", "b"], "x", "")],
            [new QuestionInput(QuestionType.ShortAnswer, QuestionDifficulty.Easy, "Q?", [], " ", "")],
            [Choice(), Choice(body: new string('q', 1001))],
            [.. Enumerable.Range(0, 31).Select(i => Choice($"Question {i}?"))],
        ];

        foreach (var questions in cases)
        {
            Assert.Equal(HttpStatusCode.BadRequest, (await teacher.PostJsonAsync(Url, new SaveQuestionsCommand(scenario.Subject.Id, [.. questions]))).StatusCode);
        }

        Assert.Equal(before, await QuestionCountAsync());
    }

    [Fact]
    public async Task Teachers_of_a_subject_share_its_bank_but_only_the_author_changes_or_deletes()
    {
        var scenario = await ClassroomScenario.CreateAsync(fixture.Factory, 1);
        using var author = scenario.TeacherClient();
        var saved = (await SaveAsync(author, scenario.Subject.Id, Choice("Shared?"))).Single();

        // A second teacher of the same subject (scenarios share the subject Mathematics) sees the question, marked as not theirs.
        var colleague = await ClassroomScenario.CreateAsync(fixture.Factory, 1);
        using var colleagueClient = colleague.TeacherClient();
        var asColleague = await (await colleagueClient.GetAsync($"{Url}?subjectId={colleague.Subject.Id}&pageSize=100")).ReadAsync<PagedResult<QuestionDto>>();
        var seen = Assert.Single(asColleague.Items, q => q.Id == saved.Id);
        Assert.False(seen.IsMine);
        Assert.Equal(scenario.Teacher.FullName, seen.CreatedByName);

        var edit = new UpdateQuestionCommand(saved.Id, Choice("Changed?", 0));
        var byAuthor = await author.PutJsonAsync($"{Url}/{saved.Id}", edit);
        var byOther = await colleagueClient.PutJsonAsync($"{Url}/{saved.Id}", edit);
        var deleteByOther = await colleagueClient.DeleteAsync($"{Url}/{saved.Id}");

        Assert.Equal(HttpStatusCode.OK, byAuthor.StatusCode);
        Assert.Equal("Changed?", (await byAuthor.ReadAsync<QuestionDto>()).Body);
        Assert.Equal(HttpStatusCode.Forbidden, byOther.StatusCode);
        Assert.Equal("questions.not_yours", await byOther.ProblemCodeAsync());
        Assert.Equal(HttpStatusCode.Forbidden, deleteByOther.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await author.DeleteAsync($"{Url}/{saved.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await author.DeleteAsync($"{Url}/{saved.Id}")).StatusCode);
    }

    [Fact]
    public async Task A_teacher_cannot_save_or_read_the_bank_of_a_subject_they_do_not_teach()
    {
        var scenario = await ClassroomScenario.CreateAsync(fixture.Factory, 1);
        var other = await ClassroomScenario.CreateAsync(fixture.Factory, 1, subjectName: "English");
        using var teacher = scenario.TeacherClient();

        var save = await teacher.PostJsonAsync(Url, new SaveQuestionsCommand(other.Subject.Id, [Choice()]));
        var list = await teacher.GetAsync($"{Url}?subjectId={other.Subject.Id}");

        Assert.Equal(HttpStatusCode.Forbidden, save.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, list.StatusCode);
    }

    [Fact]
    public async Task Only_teachers_use_the_question_bank()
    {
        var scenario = await ClassroomScenario.CreateAsync(fixture.Factory, 1);
        using var admin = fixture.AsAdmin();
        using var parent = fixture.AsParent();
        using var student = scenario.StudentClient(0);
        using var guest = fixture.Factory.CreateClient();

        foreach (var client in new[] { admin, parent, student })
        {
            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"{Url}?subjectId={scenario.Subject.Id}")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.PostJsonAsync(Url, new SaveQuestionsCommand(scenario.Subject.Id, [Choice()]))).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync($"{Url}/generate", Form(scenario.Subject.Id, Pdf()))).StatusCode);
        }

        Assert.Equal(HttpStatusCode.Unauthorized, (await guest.GetAsync($"{Url}?subjectId={scenario.Subject.Id}")).StatusCode);
    }
}
