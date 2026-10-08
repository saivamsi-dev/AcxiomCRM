using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace AcxiomCRM.Models;

public enum LeadStatus
{
    New,
    Contacted,
    Qualified,
    Unqualified,
    Converted,
    Lost
}

public class Lead
{
    public int LeadId { get; set; }

    [Required]
    [MaxLength(20)]
    [BindNever]
    public string LeadCode { get; set; } = string.Empty;

    [Required]
    [MaxLength(150)]
    public string LeadName { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    [MaxLength(255)]
    public string Email { get; set; } = string.Empty;

    [Required]
    [Phone]
    [MaxLength(20)]
    public string Phone { get; set; } = string.Empty;

    [MaxLength(150)]
    public string? CompanyName { get; set; }

    [Required]
    [MaxLength(100)]
    public string Source { get; set; } = string.Empty;

    [Required]
    [MaxLength(30)]
    [EnumDataType(typeof(LeadStatus))]
    public string Status { get; set; } = nameof(LeadStatus.New);

    [Required]
    [Range(typeof(decimal), "0", "9999999999999999.99")]
    [Column(TypeName = "numeric(18,2)")]
    [DataType(DataType.Currency)]
    public decimal ExpectedValue { get; set; }

    [BindNever]
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    [MaxLength(450)]
    public string? AssignedTo { get; set; }

    [ForeignKey(nameof(AssignedTo))]
    public ApplicationUser? AssignedUser { get; set; }
}