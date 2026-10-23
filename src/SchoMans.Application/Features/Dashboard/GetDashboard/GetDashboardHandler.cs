using SchoMans.Application.Common.Errors;
using SchoMans.Application.Common.Interfaces;

namespace SchoMans.Application.Features.Dashboard.GetDashboard;

/// <param name="Sections">Role-specific summary sections. Empty in Phase 1; the analytics dashboard (F-09) fills it later.</param>
public sealed record DashboardDto(string Role, string FullName, IReadOnlyList<string> Sections);

public sealed class GetDashboardHandler(ICurrentUser currentUser)
{
    public Task<Result<DashboardDto>> Handle(GetDashboardQuery query, CancellationToken ct)
    {
        Result<DashboardDto> result = new DashboardDto(currentUser.Role.ToString(), currentUser.FullName, []);
        return Task.FromResult(result);
    }
}
