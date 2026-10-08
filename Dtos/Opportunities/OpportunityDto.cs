namespace AcxiomCRM.Dtos.Opportunities;

public sealed class OpportunityDto
{
    public int OpportunityId { get; init; }
    public string OpportunityName { get; init; } = string.Empty;
    public int CustomerId { get; init; }
    public string CustomerName { get; init; } = string.Empty;
    public int? LeadId { get; init; }
    public string? LeadName { get; init; }
    public decimal Amount { get; init; }
    public string Stage { get; init; } = string.Empty;
    public int Probability { get; init; }
    public DateOnly? ExpectedCloseDate { get; init; }
    public string Status { get; init; } = string.Empty;
    public DateTime CreatedDate { get; init; }
    public string AssignedTo { get; init; } = string.Empty;
}