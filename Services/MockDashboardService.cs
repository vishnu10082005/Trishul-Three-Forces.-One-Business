using CSharpFullStackDemo.Models;

namespace CSharpFullStackDemo.Services;

public class MockDashboardService : IDashboardService
{
    public Task<DashboardSummary> GetSummaryAsync()
    {
        var summary = new DashboardSummary
        {
            TotalQuotationsCount = 24,
            TotalQuotationsAmount = 485000m,
            TotalInvoicesCount = 38,
            TotalInvoicesAmount = 762400m,
            PaidAmount = 542000m,
            PendingAmount = 220400m,
            IsMockData = true
        };

        return Task.FromResult(summary);
    }

    public Task<IReadOnlyList<RecentActivityItem>> GetRecentActivitiesAsync()
    {
        var activities = new List<RecentActivityItem>
        {
            new RecentActivityItem
            {
                Id = "act-1",
                ActivityType = "Invoice",
                ReferenceNumber = "INV-2026-038",
                CustomerName = "Sri Balaji Enterprises",
                Amount = 84500m,
                Date = DateTime.Today.AddDays(-1),
                Status = "Paid"
            },
            new RecentActivityItem
            {
                Id = "act-2",
                ActivityType = "Quotation",
                ReferenceNumber = "QUO-2026-024",
                CustomerName = "Kiran Tech Solutions",
                Amount = 142000m,
                Date = DateTime.Today.AddDays(-2),
                Status = "Sent"
            },
            new RecentActivityItem
            {
                Id = "act-3",
                ActivityType = "Invoice",
                ReferenceNumber = "INV-2026-037",
                CustomerName = "Apex Industrial Hardware",
                Amount = 56200m,
                Date = DateTime.Today.AddDays(-4),
                Status = "Pending"
            },
            new RecentActivityItem
            {
                Id = "act-4",
                ActivityType = "Invoice",
                ReferenceNumber = "INV-2026-036",
                CustomerName = "Sharma & Sons Logistics",
                Amount = 79700m,
                Date = DateTime.Today.AddDays(-5),
                Status = "Paid"
            },
            new RecentActivityItem
            {
                Id = "act-5",
                ActivityType = "Quotation",
                ReferenceNumber = "QUO-2026-023",
                CustomerName = "Zenith Retail Mart",
                Amount = 38500m,
                Date = DateTime.Today.AddDays(-6),
                Status = "Draft"
            }
        };

        return Task.FromResult<IReadOnlyList<RecentActivityItem>>(activities);
    }
}
