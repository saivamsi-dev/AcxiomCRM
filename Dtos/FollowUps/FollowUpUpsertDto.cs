using System.ComponentModel.DataAnnotations;
using AcxiomCRM.Models;

namespace AcxiomCRM.Dtos.FollowUps;

public sealed class FollowUpUpsertDto
{
    [Range(1, int.MaxValue)]
    public int? CustomerId { get; init; }

    [Range(1, int.MaxValue)]
    public int? LeadId { get; init; }

    [Required]
    [DataType(DataType.Date)]
    public DateOnly? FollowUpDate { get; init; }

    [Required]
    [StringLength(100)]
    public string FollowUpType { get; init; } = string.Empty;

    [StringLength(2000)]
    public string? Remarks { get; init; }

    [Required]
    [EnumDataType(typeof(FollowUpStatus))]
    public string Status { get; init; } = nameof(FollowUpStatus.Planned);
}