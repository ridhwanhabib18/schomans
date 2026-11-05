using SchoMans.Api.Endpoints;
using SchoMans.Api.Hubs;

namespace SchoMans.Api.Extensions;

public static class EndpointRegistration
{
    /// <summary>
    /// Every endpoint and hub of the API, in one place. <c>Program.cs</c> maps them with this, and <c>tools/SchoMans.DocGen</c> reads
    /// the same list to write the API reference and the authorization matrix, so the documents cannot drift from the code.
    /// </summary>
    public static WebApplication MapSchoMansEndpoints(this WebApplication app)
    {
        app.MapDashboardEndpoints();
        app.MapUserEndpoints();
        app.MapStudentEndpoints();
        app.MapTeacherEndpoints();
        app.MapClassroomEndpoints();
        app.MapSubjectEndpoints();
        app.MapAcademicYearEndpoints();
        app.MapScheduleEndpoints();
        app.MapAttendanceEndpoints();
        app.MapAssignmentEndpoints();
        app.MapGradeEndpoints();
        app.MapReportCardEndpoints();
        app.MapAdmissionEndpoints();
        app.MapRequestEndpoints();
        app.MapApprovalChainEndpoints();
        app.MapLetterEndpoints();
        app.MapGradeBandEndpoints();
        app.MapGradeWeightEndpoints();
        app.MapNewsEndpoints();
        app.MapAnnouncementEndpoints();
        app.MapNotificationEndpoints();
        app.MapProgressSummaryEndpoints();
        app.MapTeacherNoteEndpoints();
        app.MapLearningObjectiveEndpoints();
        app.MapAssistantEndpoints();
        app.MapAuditEndpoints();
        app.MapRubricEndpoints();
        app.MapEssayFeedbackEndpoints();
        app.MapDraftEndpoints();
        app.MapChatbotEndpoints();
        app.MapQuestionBankEndpoints();
        app.MapQuizEndpoints();
        app.MapGeneralLetterEndpoints();
        app.MapFileEndpoints();
        app.MapSitemapEndpoints();
        app.MapHealthEndpoints();
        app.MapHub<NotificationHub>(NotificationHub.Path);
        app.MapHub<AttendanceHub>(AttendanceHub.Path);

        return app;
    }
}
