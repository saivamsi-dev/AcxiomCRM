namespace AcxiomCRM.Dtos.Leads;

public sealed class LeadDto
{
    public int LeadId { get; init; }
    public string LeadCode { get; init; } = string.Empty;
    public string LeadName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string Phone { get; init; } = string.Empty;
    public string? CompanyName { get; init; }
    public string Source { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public decimal ExpectedValue { get; init; }
    public DateTime CreatedDate { get; init; }
    public string? AssignedTo { get; init; }
}