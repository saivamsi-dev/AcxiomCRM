using AcxiomCRM.Data;
using AcxiomCRM.Dtos.Customers;
using AcxiomCRM.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Controllers.Api;

[ApiController]
[Route("api/customers")]
[Authorize(Policy = "SalesAccess")]
[Produces("application/json")]
public class CustomersController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public CustomersController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<CustomerDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CustomerDto>>> GetAll()
    {
        var customers = await GetScopedCustomers()
            .AsNoTracking()
            .OrderByDescending(customer => customer.CreatedDate)
            .Select(customer => ToDto(customer))
            .ToListAsync();

        return Ok(customers);
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(CustomerDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CustomerDto>> GetById(int id)
    {
        var customer = await GetScopedCustomers()
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.CustomerId == id);
        if (customer is null)
        {
            return NotFound();
        }

        return Ok(ToDto(customer));
    }

    [HttpPost]
    [ProducesResponseType(typeof(CustomerDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CustomerDto>> Create(CustomerUpsertDto request)
    {
        var owner = User.Identity?.Name;
        if (string.IsNullOrWhiteSpace(owner))
        {
            return Forbid();
        }

        var email = request.Email.Trim();
        var phone = request.Phone.Trim();
        if (await EmailExistsAsync(email))
        {
            return DuplicateConflict(nameof(request.Email), "A customer with this email already exists.");
        }

        if (await PhoneExistsAsync(phone))
        {
            return DuplicateConflict(nameof(request.Phone), "A customer with this phone number already exists.");
        }

        var customer = new Customer
        {
            CustomerCode = $"CUS-{Guid.NewGuid():N}"[..12].ToUpperInvariant(),
            CustomerName = request.CustomerName.Trim(),
            Email = email,
            Phone = phone,
            CompanyName = NormalizeOptional(request.CompanyName),
            Address = NormalizeOptional(request.Address),
            City = NormalizeOptional(request.City),
            State = NormalizeOptional(request.State),
            Status = "Active",
            CreatedDate = DateTime.UtcNow,
            CreatedBy = owner
        };

        _context.Customers.Add(customer);
        await _context.SaveChangesAsync();

        var response = ToDto(customer);
        return CreatedAtAction(nameof(GetById), new { id = customer.CustomerId }, response);
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(CustomerDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CustomerDto>> Update(int id, CustomerUpsertDto request)
    {
        var customer = await GetScopedCustomers()
            .FirstOrDefaultAsync(item => item.CustomerId == id);
        if (customer is null)
        {
            return NotFound();
        }

        var email = request.Email.Trim();
        var phone = request.Phone.Trim();
        if (await EmailExistsAsync(email, id))
        {
            return DuplicateConflict(nameof(request.Email), "A customer with this email already exists.");
        }

        if (await PhoneExistsAsync(phone, id))
        {
            return DuplicateConflict(nameof(request.Phone), "A customer with this phone number already exists.");
        }

        customer.CustomerName = request.CustomerName.Trim();
        customer.Email = email;
        customer.Phone = phone;
        customer.CompanyName = NormalizeOptional(request.CompanyName);
        customer.Address = NormalizeOptional(request.Address);
        customer.City = NormalizeOptional(request.City);
        customer.State = NormalizeOptional(request.State);

        await _context.SaveChangesAsync();
        return Ok(ToDto(customer));
    }

    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(int id)
    {
        var customer = await GetScopedCustomers()
            .FirstOrDefaultAsync(item => item.CustomerId == id);
        if (customer is null)
        {
            return NotFound();
        }

        var hasLinkedRecords =
            await _context.Opportunities.AnyAsync(item => item.CustomerId == id) ||
            await _context.FollowUps.AnyAsync(item => item.CustomerId == id) ||
            await _context.Activities.AnyAsync(item => item.CustomerId == id);
        if (hasLinkedRecords)
        {
            return Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Customer has related records",
                Detail = "Reassign or remove related opportunities, follow-ups, and activities before deleting this customer."
            });
        }

        _context.Customers.Remove(customer);
        await _context.SaveChangesAsync();
        return NoContent();
    }

    private IQueryable<Customer> GetScopedCustomers()
    {
        var customers = _context.Customers.AsQueryable();
        if (User.IsInRole("Admin") || User.IsInRole("Manager"))
        {
            return customers;
        }

        var owner = User.Identity?.Name;
        return string.IsNullOrWhiteSpace(owner)
            ? customers.Where(_ => false)
            : customers.Where(customer => customer.CreatedBy == owner);
    }

    private Task<bool> EmailExistsAsync(string email, int? excludingCustomerId = null)
    {
        var normalizedEmail = email.ToUpperInvariant();
        return _context.Customers.AnyAsync(customer =>
            customer.CustomerId != excludingCustomerId && customer.Email.ToUpper() == normalizedEmail);
    }

    private Task<bool> PhoneExistsAsync(string phone, int? excludingCustomerId = null)
    {
        return _context.Customers.AnyAsync(customer =>
            customer.CustomerId != excludingCustomerId && customer.Phone.Trim() == phone);
    }

    private ConflictObjectResult DuplicateConflict(string field, string message)
    {
        return Conflict(new ProblemDetails
        {
            Status = StatusCodes.Status409Conflict,
            Title = "Customer conflict",
            Detail = message,
            Extensions = { ["field"] = field }
        });
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static CustomerDto ToDto(Customer customer)
    {
        return new CustomerDto
        {
            CustomerId = customer.CustomerId,
            CustomerCode = customer.CustomerCode,
            CustomerName = customer.CustomerName,
            Email = customer.Email,
            Phone = customer.Phone,
            CompanyName = customer.CompanyName,
            Address = customer.Address,
            City = customer.City,
            State = customer.State,
            Status = customer.Status,
            CreatedDate = customer.CreatedDate
        };
    }
}