using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace AcxiomCRM.Models;

public enum FollowUpStatus
{
    Planned,
    Completed,
    Missed,
    Cancelled
}

public class FollowUp : IValidatableObject
{
    public int FollowUpId { get; set; }

    [Range(1, int.MaxValue)]
    public int? CustomerId { get; set; }

    [Range(1, int.MaxValue)]
    public int? LeadId { get; set; }

    [Required]
    [DataType(DataType.Date)]
    public DateOnly? FollowUpDate { get; set; }

    [Required]
    [MaxLength(100)]
    public string FollowUpType { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Remarks { get; set; }

    [Required]
    [MaxLength(20)]
    [EnumDataType(typeof(FollowUpStatus))]
    public string Status { get; set; } = nameof(FollowUpStatus.Planned);

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
        if (!CustomerId.HasValue && !LeadId.HasValue)
        {
            yield return new ValidationResult(
                "Select a customer or a lead.",
                [nameof(CustomerId), nameof(LeadId)]);
        }

        if (string.Equals(Status, nameof(FollowUpStatus.Planned), StringComparison.Ordinal) &&
            FollowUpDate is { } followUpDate &&
            followUpDate < DateOnly.FromDateTime(DateTime.UtcNow))
        {
            yield return new ValidationResult(
                "A planned follow-up date cannot be earlier than today.",
                [nameof(FollowUpDate)]);
        }
    }
}