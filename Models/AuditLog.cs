using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AcxiomCRM.Models;

public class AuditLog
{
    [Key]
    public int AuditLogId { get; set; }

    [MaxLength(450)]
    public string? UserId { get; set; }

    [Required]
    [MaxLength(100)]
    public string Action { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string EntityName { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? RecordId { get; set; }

    [MaxLength(16000)]
    public string? OldValue { get; set; }

    [MaxLength(16000)]
    public string? NewValue { get; set; }

    [Required]
    public DateTime CreatedDate { get; set; }

    [MaxLength(45)]
    public string? IpAddress { get; set; }

    [ForeignKey(nameof(UserId))]
    public ApplicationUser? User { get; set; }
}