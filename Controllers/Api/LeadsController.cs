using AcxiomCRM.Data;
using AcxiomCRM.Dtos.Leads;
using AcxiomCRM.Models;
using AcxiomCRM.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Controllers.Api;

[ApiController]
[Route("api/leads")]
[Authorize(Policy = "SalesAccess")]
[Produces("application/json")]
public class LeadsController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly AuditService _auditService;

    public LeadsController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        AuditService auditService)
    {
        _context = context;
        _userManager = userManager;
        _auditService = auditService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<LeadDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<LeadDto>>> GetAll(
        string? searchTerm,
        string? status,
        string? source)
    {
        if (!string.IsNullOrWhiteSpace(status) && !IsStatus(status))
        {
            ModelState.AddModelError(nameof(status), "Select a valid lead status.");
            return ValidationProblem(ModelState);
        }

        var query = GetScopedLeads().AsNoTracking();
        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(lead => lead.Status == status);
        }
        if (!string.IsNullOrWhiteSpace(source))
        {
            query = query.Where(lead => lead.Source == source);
        }
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var pattern = $"%{searchTerm.Trim()}%";
            query = query.Where(lead =>
                EF.Functions.ILike(lead.LeadCode, pattern) ||
                EF.Functions.ILike(lead.LeadName, pattern) ||
                EF.Functions.ILike(lead.Email, pattern) ||
                EF.Functions.ILike(lead.Phone, pattern) ||
                (lead.CompanyName != null && EF.Functions.ILike(lead.CompanyName, pattern)) ||
                EF.Functions.ILike(lead.Source, pattern));
        }

        var results = await query
            .OrderByDescending(lead => lead.CreatedDate)
            .Select(lead => ToDto(lead))
            .ToListAsync();
        return Ok(results);
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(LeadDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LeadDto>> GetById(int id)
    {
        var lead = await GetScopedLeads().AsNoTracking()
            .FirstOrDefaultAsync(item => item.LeadId == id);
        return lead is null ? NotFound() : Ok(ToDto(lead));
    }

    [HttpPost]
    [ProducesResponseType(typeof(LeadDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<LeadDto>> Create(LeadUpsertDto request)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser is null)
        {
            return Forbid();
        }
        if (!IsStatus(request.Status))
        {
            ModelState.AddModelError(nameof(request.Status), "Select a valid lead status.");
        }
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var lead = new Lead
        {
            LeadCode = $"LEAD-{Guid.NewGuid():N}"[..13].ToUpperInvariant(),
            LeadName = request.LeadName.Trim(),
            Email = request.Email.Trim(),
            Phone = request.Phone.Trim(),
            CompanyName = NormalizeOptional(request.CompanyName),
            Source = request.Source.Trim(),
            Status = request.Status,
            ExpectedValue = request.ExpectedValue,
            CreatedDate = DateTime.UtcNow,
            AssignedTo = currentUser.Id
        };

        _context.Leads.Add(lead);
        await _context.SaveChangesAsync();
        await _auditService.TryLogAsync("Created", "Lead", lead.LeadId.ToString(), newValue: $"Status={lead.Status}");
        return CreatedAtAction(nameof(GetById), new { id = lead.LeadId }, ToDto(lead));
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(LeadDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LeadDto>> Update(int id, LeadUpsertDto request)
    {
        var lead = await GetScopedLeads().FirstOrDefaultAsync(item => item.LeadId == id);
        if (lead is null)
        {
            return NotFound();
        }
        if (!IsStatus(request.Status))
        {
            ModelState.AddModelError(nameof(request.Status), "Select a valid lead status.");
        }
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var oldValue = $"Status={lead.Status};ExpectedValue={lead.ExpectedValue}";
        lead.LeadName = request.LeadName.Trim();
        lead.Email = request.Email.Trim();
        lead.Phone = request.Phone.Trim();
        lead.CompanyName = NormalizeOptional(request.CompanyName);
        lead.Source = request.Source.Trim();
        lead.Status = request.Status;
        lead.ExpectedValue = request.ExpectedValue;

        await _context.SaveChangesAsync();
        await _auditService.TryLogAsync(
            "Updated", "Lead", lead.LeadId.ToString(), oldValue,
            $"Status={lead.Status};ExpectedValue={lead.ExpectedValue}");
        return Ok(ToDto(lead));
    }

    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(int id)
    {
        var lead = await GetScopedLeads().FirstOrDefaultAsync(item => item.LeadId == id);
        if (lead is null)
        {
            return NotFound();
        }

        var hasLinkedRecords =
            await _context.Opportunities.AnyAsync(item => item.LeadId == id) ||
            await _context.FollowUps.AnyAsync(item => item.LeadId == id) ||
            await _context.Activities.AnyAsync(item => item.LeadId == id);
        if (hasLinkedRecords)
        {
            return Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Lead has related records",
                Detail = "Remove or reassign related opportunities, follow-ups, and activities before deleting this lead."
            });
        }

        var leadId = lead.LeadId.ToString();
        _context.Leads.Remove(lead);
        await _context.SaveChangesAsync();
        await _auditService.TryLogAsync("Deleted", "Lead", leadId);
        return NoContent();
    }

    private IQueryable<Lead> GetScopedLeads()
    {
        var query = _context.Leads.AsQueryable();
        if (!User.IsInRole("Admin") && !User.IsInRole("Manager"))
        {
            var userId = _userManager.GetUserId(User);
            query = userId is null ? query.Where(_ => false) : query.Where(item => item.AssignedTo == userId);
        }

        return query;
    }

    private static bool IsStatus(string status)
    {
        return Enum.TryParse<LeadStatus>(status, ignoreCase: false, out var parsed) && Enum.IsDefined(parsed);
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static LeadDto ToDto(Lead lead) => new()
    {
        LeadId = lead.LeadId,
        LeadCode = lead.LeadCode,
        LeadName = lead.LeadName,
        Email = lead.Email,
        Phone = lead.Phone,
        CompanyName = lead.CompanyName,
        Source = lead.Source,
        Status = lead.Status,
        ExpectedValue = lead.ExpectedValue,
        CreatedDate = lead.CreatedDate,
        AssignedTo = lead.AssignedTo
    };
}