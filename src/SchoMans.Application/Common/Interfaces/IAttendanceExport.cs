using SchoMans.Application.Features.Attendance;

namespace SchoMans.Application.Common.Interfaces;

/// <summary>Writes an attendance summary as an Excel workbook (ClosedXML in Infrastructure).</summary>
public interface IAttendanceExport
{
    byte[] Write(ClassroomAttendanceSummaryDto summary);
}
