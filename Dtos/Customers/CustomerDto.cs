namespace AcxiomCRM.Dtos.Customers;

public sealed class CustomerDto
{
    public int CustomerId { get; init; }
    public string CustomerCode { get; init; } = string.Empty;
    public string CustomerName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string Phone { get; init; } = string.Empty;
    public string? CompanyName { get; init; }
    public string? Address { get; init; }
    public string? City { get; init; }
    public string? State { get; init; }
    public string Status { get; init; } = string.Empty;
    public DateTime CreatedDate { get; init; }
}