using System.ComponentModel.DataAnnotations;

namespace AcxiomCRM.Dtos.Customers;

public sealed class CustomerUpsertDto
{
    [Required]
    [StringLength(150)]
    public string CustomerName { get; init; } = string.Empty;

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

    [StringLength(500)]
    public string? Address { get; init; }

    [StringLength(100)]
    public string? City { get; init; }

    [StringLength(100)]
    public string? State { get; init; }
}