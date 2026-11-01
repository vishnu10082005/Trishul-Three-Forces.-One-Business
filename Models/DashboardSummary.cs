namespace CSharpFullStackDemo.Models;

public class DashboardSummary
{
    public int TotalQuotationsCount { get; set; }
    public decimal TotalQuotationsAmount { get; set; }

    public int TotalInvoicesCount { get; set; }
    public decimal TotalInvoicesAmount { get; set; }

    public decimal PaidAmount { get; set; }
    public decimal PendingAmount { get; set; }

    public bool IsMockData { get; set; } = true;
}

public class RecentActivityItem
{
    public string Id { get; set; } = string.Empty;
    public string ActivityType { get; set; } = "Invoice"; // "Invoice" or "Quotation"
    public string ReferenceNumber { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime Date { get; set; }
    public string Status { get; set; } = "Pending"; // "Paid", "Pending", "Draft", "Sent"
}
