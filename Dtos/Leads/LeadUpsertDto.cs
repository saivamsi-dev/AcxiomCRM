using System.ComponentModel.DataAnnotations;
using AcxiomCRM.Models;

namespace AcxiomCRM.Dtos.Leads;

public sealed class LeadUpsertDto
{
    [Required]
    [StringLength(150)]
    public string LeadName { get; init; } = string.Empty;

    [Required]
    [EmailAddress]
    [StringLength(255)]
    public string Email { get; init; } = string.Empty;

    [Required]
    [Phone]
    [StringLength(20)]
    public string Phone { get; init; } = string.Empty;

    [StringLength(150)]
    public string? CompanyName { get; init; }

    [Required]
    [StringLength(100)]
    public string Source { get; init; } = string.Empty;

    [Required]
    [EnumDataType(typeof(LeadStatus))]
    public string Status { get; init; } = nameof(LeadStatus.New);

    [Range(typeof(decimal), "0", "9999999999999999.99")]
    public decimal ExpectedValue { get; init; }
}