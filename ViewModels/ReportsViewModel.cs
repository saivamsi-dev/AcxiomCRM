namespace AcxiomCRM.ViewModels;

public sealed class ReportsViewModel
{
    public string ReportType { get; init; } = "leads";
    public DateOnly? FromDate { get; init; }
    public DateOnly? ToDate { get; init; }
    public string? Status { get; init; }
    public string? Stage { get; init; }
    public string? Source { get; init; }
    public string? AssignedTo { get; init; }
    public string? SearchText { get; init; }
    public string? DateFilterError { get; set; }
    public bool CanChooseAssignedUser { get; init; }

    public IReadOnlyList<ReportFilterOption> StatusOptions { get; init; } = [];
    public IReadOnlyList<ReportFilterOption> StageOptions { get; init; } = [];
    public IReadOnlyList<ReportFilterOption> SourceOptions { get; init; } = [];
    public IReadOnlyList<ReportFilterOption> AssignedUserOptions { get; init; } = [];

    public CustomerReportSummary CustomerSummary { get; set; } = new();
    public LeadReportSummary LeadSummary { get; set; } = new();
    public OpportunityReportSummary OpportunitySummary { get; set; } = new();
    public FollowUpReportSummary FollowUpSummary { get; set; } = new();

    public IReadOnlyList<string> CustomerStatusesWithCounts { get; init; } = [];
    public IReadOnlyList<CustomerReportRow> Customers { get; set; } = [];
    public IReadOnlyList<LeadReportRow> Leads { get; set; } = [];
    public IReadOnlyList<OpportunityReportRow> Opportunities { get; set; } = [];
    public IReadOnlyList<FollowUpReportRow> FollowUps { get; set; } = [];
}

public sealed class ReportFilterOption
{
    public ReportFilterOption(string value, string text)
    {
        Value = value;
        Text = text;
    }

    public string Value { get; }
    public string Text { get; }
}

public sealed class CustomerReportSummary
{
    public int Total { get; init; }
    public int Active { get; init; }
    public IReadOnlyList<ReportStatusCount> OtherStatuses { get; init; } = [];
}

public sealed class LeadReportSummary
{
    public int Total { get; init; }
    public int New { get; init; }
    public int Contacted { get; init; }
    public int Qualified { get; init; }
    public int Converted { get; init; }
    public int Lost { get; init; }
    public int Unqualified { get; init; }
    public decimal TotalExpectedValue { get; init; }
}

public sealed class OpportunityReportSummary
{
    public int Total { get; init; }
    public int Open { get; init; }
    public int Won { get; init; }
    public int Lost { get; init; }
    public decimal OpenPipelineValue { get; init; }
    public decimal WeightedPipelineValue { get; init; }
    public decimal WonValue { get; init; }
    public decimal LostValue { get; init; }
}

public sealed class FollowUpReportSummary
{
    public int Planned { get; init; }
    public int Completed { get; init; }
    public int Missed { get; init; }
    public int Cancelled { get; init; }
    public int Upcoming { get; init; }
    public int OverdueOrMissed { get; init; }
}

public sealed class ReportStatusCount
{
    public string Status { get; init; } = string.Empty;
    public int Count { get; init; }
}

public sealed class CustomerReportRow
{
    public string CustomerCode { get; init; } = string.Empty;
    public string CustomerName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string Phone { get; init; } = string.Empty;
    public string? CompanyName { get; init; }
    public string? City { get; init; }
    public string? State { get; init; }
    public string Status { get; init; } = string.Empty;
    public DateTime CreatedDate { get; init; }
    public string CreatedBy { get; init; } = string.Empty;
}

public sealed class LeadReportRow
{
    public string LeadCode { get; init; } = string.Empty;
    public string LeadName { get; init; } = string.Empty;
    public string? CompanyName { get; init; }
    public string Source { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public decimal ExpectedValue { get; init; }
    public string AssignedTo { get; init; } = string.Empty;
    public DateTime CreatedDate { get; init; }
}

public sealed class OpportunityReportRow
{
    public string OpportunityName { get; init; } = string.Empty;
    public string CustomerName { get; init; } = string.Empty;
    public string? LeadName { get; init; }
    public decimal Amount { get; init; }
    public string Stage { get; init; } = string.Empty;
    public int Probability { get; init; }
    public decimal WeightedValue { get; init; }
    public string Status { get; init; } = string.Empty;
    public DateOnly? ExpectedCloseDate { get; init; }
    public string AssignedTo { get; init; } = string.Empty;
}

public sealed class FollowUpReportRow
{
    public DateOnly? FollowUpDate { get; init; }
    public string FollowUpType { get; init; } = string.Empty;
    public string? CustomerName { get; init; }
    public string? LeadName { get; init; }
    public string? Remarks { get; init; }
    public string Status { get; init; } = string.Empty;
    public string AssignedTo { get; init; } = string.Empty;
}