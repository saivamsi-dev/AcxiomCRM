using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace AcxiomCRM.Models;

public enum ActivityType
{
    Call,
    Meeting,
    Email,
    Task
}

public enum ActivityStatus
{
    Planned,
    Completed,
    Cancelled
}

public class Activity
{
    public int ActivityId { get; set; }

    [Required]
    [MaxLength(20)]
    [EnumDataType(typeof(ActivityType))]
    public string ActivityType { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string Subject { get; set; } = string.Empty;

    [MaxLength(4000)]
    public string? Description { get; set; }

    [Required]
    [DataType(DataType.Date)]
    public DateOnly? ActivityDate { get; set; }

    [Range(1, int.MaxValue)]
    public int? CustomerId { get; set; }

    [Range(1, int.MaxValue)]
    public int? LeadId { get; set; }

    [Required]
    [MaxLength(20)]
    [EnumDataType(typeof(ActivityStatus))]
    public string Status { get; set; } = nameof(ActivityStatus.Planned);

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
}