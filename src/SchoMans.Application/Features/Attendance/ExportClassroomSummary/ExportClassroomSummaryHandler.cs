using SchoMans.Application.Common.Errors;
using SchoMans.Application.Common.Interfaces;
using SchoMans.Application.Features.Attendance.GetClassroomSummary;

namespace SchoMans.Application.Features.Attendance.ExportClassroomSummary;

public sealed record AttendanceExportFile(string FileName, byte[] Content);

/// <summary>The classroom summary of a month as an Excel file, with the same access rules as the summary on screen.</summary>
public sealed class ExportClassroomSummaryHandler(GetClassroomSummaryHandler summaries, IAttendanceExport export)
{
    public async Task<Result<AttendanceExportFile>> Handle(GetClassroomSummaryQuery query, CancellationToken ct)
    {
        var summary = await summaries.Handle(query, ct);
        if (summary.IsFailure)
        {
            return summary.Error;
        }

        var safeName = string.Concat(summary.Value.ClassroomName.Select(c => char.IsLetterOrDigit(c) ? c : '-'));
        return new AttendanceExportFile($"attendance-{safeName}-{summary.Value.Month}.xlsx", export.Write(summary.Value));
    }
}
