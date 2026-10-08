using AcxiomCRM.Data;
using AcxiomCRM.Dtos.FollowUps;
using AcxiomCRM.Models;
using AcxiomCRM.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Controllers.Api;

[ApiController]
[Route("api/follow-ups")]
[Authorize(Policy = "SalesAccess")]
[Produces("application/json")]
public class FollowUpsController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly AuditService _auditService;

    public FollowUpsController(ApplicationDbContext context, UserManager<ApplicationUser> userManager, AuditService auditService)
    {
        _context = context;
        _userManager = userManager;
        _auditService = auditService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<FollowUpDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<FollowUpDto>>> GetAll(
        DateOnly? fromDate,
        DateOnly? toDate,
        string? status,
        string? searchTerm)
    {
        if (!ValidateDates(fromDate, toDate) ||
            (!string.IsNullOrWhiteSpace(status) && !IsStatus(status)))
        {
            return ValidationProblem(ModelState);
        }

        IQueryable<FollowUp> query = GetScopedFollowUps().AsNoTracking()
            .Include(item => item.Customer)
            .Include(item => item.Lead);
        query = ApplyFilters(query, fromDate, toDate, status, searchTerm);

        var results = await query.OrderBy(item => item.FollowUpDate)
            .Select(item => ToDto(item))
            .ToListAsync();
        return Ok(results);
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(FollowUpDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FollowUpDto>> GetById(int id)
    {
        var item = await GetScopedFollowUps().AsNoTracking()
            .Include(followUp => followUp.Customer)
            .Include(followUp => followUp.Lead)
            .FirstOrDefaultAsync(followUp => followUp.FollowUpId == id);
        return item is null ? NotFound() : Ok(ToDto(item));
    }

    [HttpPost]
    [ProducesResponseType(typeof(FollowUpDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<FollowUpDto>> Create(FollowUpUpsertDto request)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null)
        {
            return Forbid();
        }
        if (!ValidateBusinessRules(request))
        {
            return ValidationProblem(ModelState);
        }
        if (!await GetScopedCustomers().AnyAsync(item => item.CustomerId == request.CustomerId))
        {
            if (request.CustomerId.HasValue)
            {
                ModelState.AddModelError(nameof(request.CustomerId), "Select an accessible existing customer.");
            }
        }
        if (request.LeadId.HasValue && !await GetScopedLeads().AnyAsync(item => item.LeadId == request.LeadId.Value))
        {
            ModelState.AddModelError(nameof(request.LeadId), "Select an accessible existing lead.");
        }
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var followUp = new FollowUp
        {
            CustomerId = request.CustomerId,
            LeadId = request.LeadId,
            FollowUpDate = request.FollowUpDate,
            FollowUpType = request.FollowUpType.Trim(),
            Remarks = NormalizeOptional(request.Remarks),
            Status = request.Status,
            AssignedTo = user.Id
        };

        _context.FollowUps.Add(followUp);
        await _context.SaveChangesAsync();
        await _auditService.TryLogAsync(
            "Created", "FollowUp", followUp.FollowUpId.ToString(),
            newValue: $"Status={followUp.Status};Type={followUp.FollowUpType}");

        var response = await GetDtoQuery().FirstAsync(item => item.FollowUpId == followUp.FollowUpId);
        return CreatedAtAction(nameof(GetById), new { id = followUp.FollowUpId }, response);
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(FollowUpDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FollowUpDto>> Update(int id, FollowUpUpsertDto request)
    {
        var followUp = await GetScopedFollowUps().FirstOrDefaultAsync(item => item.FollowUpId == id);
        if (followUp is null)
        {
            return NotFound();
        }
        if (!ValidateBusinessRules(request))
        {
            return ValidationProblem(ModelState);
        }
        if (request.CustomerId.HasValue && !await GetScopedCustomers().AnyAsync(item => item.CustomerId == request.CustomerId.Value))
        {
            ModelState.AddModelError(nameof(request.CustomerId), "Select an accessible existing customer.");
        }
        if (request.LeadId.HasValue && !await GetScopedLeads().AnyAsync(item => item.LeadId == request.LeadId.Value))
        {
            ModelState.AddModelError(nameof(request.LeadId), "Select an accessible existing lead.");
        }
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var oldValue = $"Status={followUp.Status};Type={followUp.FollowUpType}";
        followUp.CustomerId = request.CustomerId;
        followUp.LeadId = request.LeadId;
        followUp.FollowUpDate = request.FollowUpDate;
        followUp.FollowUpType = request.FollowUpType.Trim();
        followUp.Remarks = NormalizeOptional(request.Remarks);
        followUp.Status = request.Status;
        await _context.SaveChangesAsync();

        await _auditService.TryLogAsync(
            "Updated", "FollowUp", followUp.FollowUpId.ToString(), oldValue,
            $"Status={followUp.Status};Type={followUp.FollowUpType}");
        var response = await GetDtoQuery().FirstAsync(item => item.FollowUpId == followUp.FollowUpId);
        return Ok(response);
    }

    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(int id)
    {
        var followUp = await GetScopedFollowUps().FirstOrDefaultAsync(item => item.FollowUpId == id);
        if (followUp is null)
        {
            return NotFound();
        }

        var followUpId = followUp.FollowUpId.ToString();
        _context.FollowUps.Remove(followUp);
        await _context.SaveChangesAsync();
        await _auditService.TryLogAsync("Deleted", "FollowUp", followUpId);
        return NoContent();
    }

    private IQueryable<FollowUp> GetScopedFollowUps()
    {
        var query = _context.FollowUps.AsQueryable();
        if (!IsBroadScope())
        {
            var userId = _userManager.GetUserId(User);
            query = userId is null ? query.Where(_ => false) : query.Where(item => item.AssignedTo == userId);
        }
        return query;
    }

    private IQueryable<Customer> GetScopedCustomers()
    {
        var query = _context.Customers.AsQueryable();
        if (!IsBroadScope())
        {
            var owner = User.Identity?.Name;
            query = owner is null ? query.Where(_ => false) : query.Where(item => item.CreatedBy == owner);
        }
        return query;
    }

    private IQueryable<Lead> GetScopedLeads()
    {
        var query = _context.Leads.AsQueryable();
        if (!IsBroadScope())
        {
            var userId = _userManager.GetUserId(User);
            query = userId is null ? query.Where(_ => false) : query.Where(item => item.AssignedTo == userId);
        }
        return query;
    }

    private bool IsBroadScope() => User.IsInRole("Admin") || User.IsInRole("Manager");

    private bool ValidateBusinessRules(FollowUpUpsertDto request)
    {
        if (!IsStatus(request.Status))
        {
            ModelState.AddModelError(nameof(request.Status), "Select a valid follow-up status.");
        }
        if (!request.CustomerId.HasValue && !request.LeadId.HasValue)
        {
            ModelState.AddModelError(string.Empty, "Select at least one Customer or Lead.");
        }
        if (request.Status == nameof(FollowUpStatus.Planned) &&
            request.FollowUpDate < DateOnly.FromDateTime(DateTime.UtcNow))
        {
            ModelState.AddModelError(nameof(request.FollowUpDate), "A planned follow-up date cannot be in the past.");
        }
        return ModelState.IsValid;
    }

    private bool ValidateDates(DateOnly? fromDate, DateOnly? toDate)
    {
        if (fromDate.HasValue && toDate.HasValue && fromDate.Value > toDate.Value)
        {
            ModelState.AddModelError(nameof(fromDate), "From date cannot be after To date.");
        }
        return ModelState.IsValid;
    }

    private static IQueryable<FollowUp> ApplyFilters(
        IQueryable<FollowUp> query,
        DateOnly? fromDate,
        DateOnly? toDate,
        string? status,
        string? searchTerm)
    {
        if (fromDate.HasValue)
        {
            query = query.Where(item => item.FollowUpDate.HasValue && item.FollowUpDate.Value >= fromDate.Value);
        }
        if (toDate.HasValue)
        {
            query = query.Where(item => item.FollowUpDate.HasValue && item.FollowUpDate.Value <= toDate.Value);
        }
        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(item => item.Status == status);
        }
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var pattern = $"%{searchTerm.Trim()}%";
            query = query.Where(item =>
                EF.Functions.ILike(item.FollowUpType, pattern) ||
                (item.Remarks != null && EF.Functions.ILike(item.Remarks, pattern)) ||
                (item.Customer != null && EF.Functions.ILike(item.Customer.CustomerName, pattern)) ||
                (item.Lead != null && EF.Functions.ILike(item.Lead.LeadName, pattern)));
        }

        return query;
    }

    private static bool IsStatus(string value) =>
        Enum.TryParse<FollowUpStatus>(value, false, out var result) && Enum.IsDefined(result);

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private IQueryable<FollowUpDto> GetDtoQuery() => _context.FollowUps
        .AsNoTracking()
        .Select(item => new FollowUpDto
        {
            FollowUpId = item.FollowUpId,
            FollowUpDate = item.FollowUpDate,
            FollowUpType = item.FollowUpType,
            Remarks = item.Remarks,
            CustomerId = item.CustomerId,
            CustomerName = item.Customer == null ? null : item.Customer.CustomerName,
            LeadId = item.LeadId,
            LeadName = item.Lead == null ? null : item.Lead.LeadName,
            Status = item.Status,
            AssignedTo = item.AssignedTo
        });

    private static FollowUpDto ToDto(FollowUp item) => new()
    {
        FollowUpId = item.FollowUpId,
        FollowUpDate = item.FollowUpDate,
        FollowUpType = item.FollowUpType,
        Remarks = item.Remarks,
        CustomerId = item.CustomerId,
        CustomerName = item.Customer?.CustomerName,
        LeadId = item.LeadId,
        LeadName = item.Lead?.LeadName,
        Status = item.Status,
        AssignedTo = item.AssignedTo
    };
}