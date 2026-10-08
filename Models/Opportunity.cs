using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace AcxiomCRM.Models;

public enum OpportunityStage
{
    Qualification,
    Proposal,
    Negotiation,
    Won,
    Lost
}

public enum OpportunityStatus
{
    Open,
    Won,
    Lost
}

public class Opportunity : IValidatableObject
{
    public int OpportunityId { get; set; }

    [Required]
    [MaxLength(150)]
    public string OpportunityName { get; set; } = string.Empty;

    [Range(1, int.MaxValue)]
    public int CustomerId { get; set; }

    public int? LeadId { get; set; }

    [Range(typeof(decimal), "0", "9999999999999999.99")]
    [Column(TypeName = "numeric(18,2)")]
    public decimal Amount { get; set; }

    [Required]
    [MaxLength(30)]
    [EnumDataType(typeof(OpportunityStage))]
    public string Stage { get; set; } = nameof(OpportunityStage.Qualification);

    [Range(0, 100)]
    public int Probability { get; set; }

    [Required]
    [DataType(DataType.Date)]
    public DateOnly? ExpectedCloseDate { get; set; }

    [Required]
    [MaxLength(20)]
    [EnumDataType(typeof(OpportunityStatus))]
    public string Status { get; set; } = nameof(OpportunityStatus.Open);

    [BindNever]
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    [Required]
    [MaxLength(450)]
    [BindNever]
    public string AssignedTo { get; set; } = string.Empty;

    [BindNever]
    public Customer? Customer { get; set; }

    [BindNever]
    public Lead? Lead { get; set; }

    [ForeignKey(nameof(AssignedTo))]
    [BindNever]
    public ApplicationUser? AssignedUser { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.Equals(Status, nameof(OpportunityStatus.Open), StringComparison.OrdinalIgnoreCase))
        {
            if (Amount <= 0)
            {
                yield return new ValidationResult(
                    "Amount must be greater than zero for an open opportunity.",
                    [nameof(Amount)]);
            }

            if (ExpectedCloseDate is { } closeDate && closeDate < DateOnly.FromDateTime(DateTime.UtcNow))
            {
                yield return new ValidationResult(
                    "Expected close date cannot be in the past for an open opportunity.",
                    [nameof(ExpectedCloseDate)]);
            }
        }
    }
}