namespace AcxiomCRM.Dtos.FollowUps;

public sealed class FollowUpDto
{
    public int FollowUpId { get; init; }
    public DateOnly? FollowUpDate { get; init; }
    public string FollowUpType { get; init; } = string.Empty;
    public string? Remarks { get; init; }
    public int? CustomerId { get; init; }
    public string? CustomerName { get; init; }
    public int? LeadId { get; init; }
    public string? LeadName { get; init; }
    public string Status { get; init; } = string.Empty;
    public string AssignedTo { get; init; } = string.Empty;
}