using System.ComponentModel.DataAnnotations;
using AcxiomCRM.Models;

namespace AcxiomCRM.Dtos.Opportunities;

public sealed class OpportunityUpsertDto
{
    [Required]
    [StringLength(150)]
    public string OpportunityName { get; init; } = string.Empty;

    [Range(1, int.MaxValue)]
    public int CustomerId { get; init; }

    [Range(1, int.MaxValue)]
    public int? LeadId { get; init; }

    [Range(typeof(decimal), "0", "9999999999999999.99")]
    public decimal Amount { get; init; }

    [Required]
    [EnumDataType(typeof(OpportunityStage))]
    public string Stage { get; init; } = nameof(OpportunityStage.Qualification);

    [Range(0, 100)]
    public int Probability { get; init; }

    [Required]
    [DataType(DataType.Date)]
    public DateOnly? ExpectedCloseDate { get; init; }

    [Required]
    [EnumDataType(typeof(OpportunityStatus))]
    public string Status { get; init; } = nameof(OpportunityStatus.Open);
}