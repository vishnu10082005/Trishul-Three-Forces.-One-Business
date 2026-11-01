using CSharpFullStackDemo.Models;

namespace CSharpFullStackDemo.Services;

public interface IDashboardService
{
    Task<DashboardSummary> GetSummaryAsync();
    Task<IReadOnlyList<RecentActivityItem>> GetRecentActivitiesAsync();
}
