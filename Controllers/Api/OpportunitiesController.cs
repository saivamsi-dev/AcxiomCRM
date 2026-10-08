using AcxiomCRM.Data;
using AcxiomCRM.Dtos.Opportunities;
using AcxiomCRM.Models;
using AcxiomCRM.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Controllers.Api;

[ApiController]
[Route("api/opportunities")]
[Authorize(Policy = "SalesAccess")]
[Produces("application/json")]
public class OpportunitiesController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly AuditService _auditService;

    public OpportunitiesController(ApplicationDbContext context, UserManager<ApplicationUser> userManager, AuditService auditService)
    {
        _context = context;
        _userManager = userManager;
        _auditService = auditService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<OpportunityDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<OpportunityDto>>> GetAll(
        string? status,
        string? stage,
        int? customerId,
        int? leadId,
        string? searchTerm,
        DateOnly? fromDate,
        DateOnly? toDate)
    {
        if (!ValidateDates(fromDate, toDate) ||
            (!string.IsNullOrWhiteSpace(status) && !IsStatus(status)) ||
            (!string.IsNullOrWhiteSpace(stage) && !IsStage(stage)))
        {
            return ValidationProblem(ModelState);
        }

        IQueryable<Opportunity> query = GetScopedOpportunities().AsNoTracking()
            .Include(item => item.Customer)
            .Include(item => item.Lead);
        query = ApplyFilters(query, status, stage, customerId, leadId, searchTerm, fromDate, toDate);

        var rows = await query.OrderBy(item => item.ExpectedCloseDate)
            .Select(item => ToDto(item))
            .ToListAsync();
        return Ok(rows);
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(OpportunityDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OpportunityDto>> GetById(int id)
    {
        var item = await GetScopedOpportunities().AsNoTracking()
            .Include(opportunity => opportunity.Customer)
            .Include(opportunity => opportunity.Lead)
            .FirstOrDefaultAsync(opportunity => opportunity.OpportunityId == id);
        return item is null ? NotFound() : Ok(ToDto(item));
    }

    [HttpPost]
    [ProducesResponseType(typeof(OpportunityDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<OpportunityDto>> Create(OpportunityUpsertDto request)
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
            ModelState.AddModelError(nameof(request.CustomerId), "Select an accessible existing customer.");
        }
        if (request.LeadId.HasValue &&
            !await GetScopedLeads().AnyAsync(item => item.LeadId == request.LeadId.Value))
        {
            ModelState.AddModelError(nameof(request.LeadId), "Select an accessible existing lead.");
        }
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var opportunity = new Opportunity
        {
            OpportunityName = request.OpportunityName.Trim(),
            CustomerId = request.CustomerId,
            LeadId = request.LeadId,
            Amount = request.Amount,
            Stage = request.Stage,
            Probability = request.Probability,
            ExpectedCloseDate = request.ExpectedCloseDate,
            Status = request.Status,
            CreatedDate = DateTime.UtcNow,
            AssignedTo = user.Id
        };

        _context.Opportunities.Add(opportunity);
        await _context.SaveChangesAsync();
        await _auditService.TryLogAsync(
            "Created", "Opportunity", opportunity.OpportunityId.ToString(),
            newValue: $"Status={opportunity.Status};Stage={opportunity.Stage};Amount={opportunity.Amount}");

        var response = await GetDtoQuery().FirstAsync(item => item.OpportunityId == opportunity.OpportunityId);
        return CreatedAtAction(nameof(GetById), new { id = opportunity.OpportunityId }, response);
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(OpportunityDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OpportunityDto>> Update(int id, OpportunityUpsertDto request)
    {
        var opportunity = await GetScopedOpportunities().FirstOrDefaultAsync(item => item.OpportunityId == id);
        if (opportunity is null)
        {
            return NotFound();
        }
        if (!ValidateBusinessRules(request))
        {
            return ValidationProblem(ModelState);
        }
        if (!await GetScopedCustomers().AnyAsync(item => item.CustomerId == request.CustomerId))
        {
            ModelState.AddModelError(nameof(request.CustomerId), "Select an accessible existing customer.");
        }
        if (request.LeadId.HasValue &&
            !await GetScopedLeads().AnyAsync(item => item.LeadId == request.LeadId.Value))
        {
            ModelState.AddModelError(nameof(request.LeadId), "Select an accessible existing lead.");
        }
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var oldValue = $"Status={opportunity.Status};Stage={opportunity.Stage};Amount={opportunity.Amount}";
        opportunity.OpportunityName = request.OpportunityName.Trim();
        opportunity.CustomerId = request.CustomerId;
        opportunity.LeadId = request.LeadId;
        opportunity.Amount = request.Amount;
        opportunity.Stage = request.Stage;
        opportunity.Probability = request.Probability;
        opportunity.ExpectedCloseDate = request.ExpectedCloseDate;
        opportunity.Status = request.Status;
        await _context.SaveChangesAsync();

        await _auditService.TryLogAsync(
            "Updated", "Opportunity", opportunity.OpportunityId.ToString(), oldValue,
            $"Status={opportunity.Status};Stage={opportunity.Stage};Amount={opportunity.Amount}");
        var response = await GetDtoQuery().FirstAsync(item => item.OpportunityId == opportunity.OpportunityId);
        return Ok(response);
    }

    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(int id)
    {
        var opportunity = await GetScopedOpportunities().FirstOrDefaultAsync(item => item.OpportunityId == id);
        if (opportunity is null)
        {
            return NotFound();
        }

        var opportunityId = opportunity.OpportunityId.ToString();
        _context.Opportunities.Remove(opportunity);
        await _context.SaveChangesAsync();
        await _auditService.TryLogAsync("Deleted", "Opportunity", opportunityId);
        return NoContent();
    }

    private IQueryable<Opportunity> GetScopedOpportunities()
    {
        var query = _context.Opportunities.AsQueryable();
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

    private bool ValidateBusinessRules(OpportunityUpsertDto request)
    {
        if (!IsStage(request.Stage))
        {
            ModelState.AddModelError(nameof(request.Stage), "Select a valid opportunity stage.");
        }
        if (!IsStatus(request.Status))
        {
            ModelState.AddModelError(nameof(request.Status), "Select a valid opportunity status.");
        }
        if (request.Status == nameof(OpportunityStatus.Open) && request.Amount <= 0)
        {
            ModelState.AddModelError(nameof(request.Amount), "Amount must be greater than zero for an open opportunity.");
        }
        if (request.Status == nameof(OpportunityStatus.Open) &&
            request.ExpectedCloseDate < DateOnly.FromDateTime(DateTime.UtcNow))
        {
            ModelState.AddModelError(nameof(request.ExpectedCloseDate), "Expected close date cannot be in the past for an open opportunity.");
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

    private static IQueryable<Opportunity> ApplyFilters(
        IQueryable<Opportunity> query,
        string? status,
        string? stage,
        int? customerId,
        int? leadId,
        string? searchTerm,
        DateOnly? fromDate,
        DateOnly? toDate)
    {
        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(item => item.Status == status);
        }
        if (!string.IsNullOrWhiteSpace(stage))
        {
            query = query.Where(item => item.Stage == stage);
        }
        if (customerId.HasValue)
        {
            query = query.Where(item => item.CustomerId == customerId.Value);
        }
        if (leadId.HasValue)
        {
            query = query.Where(item => item.LeadId == leadId.Value);
        }
        if (fromDate.HasValue)
        {
            query = query.Where(item => item.ExpectedCloseDate.HasValue && item.ExpectedCloseDate.Value >= fromDate.Value);
        }
        if (toDate.HasValue)
        {
            query = query.Where(item => item.ExpectedCloseDate.HasValue && item.ExpectedCloseDate.Value <= toDate.Value);
        }
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var pattern = $"%{searchTerm.Trim()}%";
            query = query.Where(item =>
                EF.Functions.ILike(item.OpportunityName, pattern) ||
                (item.Customer != null && EF.Functions.ILike(item.Customer.CustomerName, pattern)) ||
                (item.Lead != null && EF.Functions.ILike(item.Lead.LeadName, pattern)));
        }

        return query;
    }

    private static bool IsStage(string value) =>
        Enum.TryParse<OpportunityStage>(value, false, out var result) && Enum.IsDefined(result);

    private static bool IsStatus(string value) =>
        Enum.TryParse<OpportunityStatus>(value, false, out var result) && Enum.IsDefined(result);

    private IQueryable<OpportunityDto> GetDtoQuery() => _context.Opportunities
        .AsNoTracking()
        .Select(item => new OpportunityDto
        {
            OpportunityId = item.OpportunityId,
            OpportunityName = item.OpportunityName,
            CustomerId = item.CustomerId,
            CustomerName = item.Customer == null ? string.Empty : item.Customer.CustomerName,
            LeadId = item.LeadId,
            LeadName = item.Lead == null ? null : item.Lead.LeadName,
            Amount = item.Amount,
            Stage = item.Stage,
            Probability = item.Probability,
            ExpectedCloseDate = item.ExpectedCloseDate,
            Status = item.Status,
            CreatedDate = item.CreatedDate,
            AssignedTo = item.AssignedTo
        });

    private static OpportunityDto ToDto(Opportunity item) => new()
    {
        OpportunityId = item.OpportunityId,
        OpportunityName = item.OpportunityName,
        CustomerId = item.CustomerId,
        CustomerName = item.Customer?.CustomerName ?? string.Empty,
        LeadId = item.LeadId,
        LeadName = item.Lead?.LeadName,
        Amount = item.Amount,
        Stage = item.Stage,
        Probability = item.Probability,
        ExpectedCloseDate = item.ExpectedCloseDate,
        Status = item.Status,
        CreatedDate = item.CreatedDate,
        AssignedTo = item.AssignedTo
    };
}