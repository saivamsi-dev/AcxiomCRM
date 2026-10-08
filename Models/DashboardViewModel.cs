namespace AcxiomCRM.Models;

public class DashboardViewModel
{
    public string Period { get; init; } = "ThisMonth";
    public DateOnly? StartDate { get; init; }
    public DateOnly? EndDate { get; init; }
    public string? DateFilterError { get; init; }
    public bool HasValidDateRange { get; init; } = true;

    public int? TotalCustomers { get; init; }
    public int? TotalLeads { get; init; }
    public int? OpenLeads { get; init; }

    public int? TotalOpportunities { get; init; }
    public int? OpenOpportunities { get; init; }
    public int? WonOpportunities { get; init; }
    public int? LostOpportunities { get; init; }
    public decimal? TotalPipelineValue { get; init; }

    public List<DashboardChartPoint> LeadStatusData { get; init; } = [];
    public List<DashboardChartPoint> OpportunityPipelineData { get; init; } = [];
    public List<MonthlyOutcomePoint> MonthlyOutcomesData { get; init; } = [];
}

public class DashboardChartPoint
{
    public string Label { get; init; } = string.Empty;
    public decimal Value { get; init; }
}

public class MonthlyOutcomePoint
{
    public string Month { get; init; } = string.Empty;
    public decimal WonSales { get; init; }
    public decimal LostValue { get; init; }
    public decimal OpenValue { get; init; }
}
