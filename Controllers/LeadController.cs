using AcxiomCRM.Data;
using AcxiomCRM.Models;
using AcxiomCRM.Services;
using System.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace AcxiomCRM.Controllers;

[Authorize(Policy = "SalesAccess")]
public class LeadController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<LeadController> _logger;
    private readonly AuditService _auditService;

    public LeadController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        ILogger<LeadController> logger,
        AuditService auditService)
    {
        _context = context;
        _userManager = userManager;
        _logger = logger;
        _auditService = auditService;
    }

    [HttpGet]
    public IActionResult Create()
    {
        return View(new Lead { Status = nameof(LeadStatus.New) });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Lead lead)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser is null)
        {
            return Forbid();
        }

        lead.LeadId = 0;
        lead.LeadCode = $"LEAD-{Guid.NewGuid():N}"[..13].ToUpperInvariant();
        lead.CreatedDate = DateTime.UtcNow;
        lead.Status = string.IsNullOrWhiteSpace(lead.Status)
            ? nameof(LeadStatus.New)
            : lead.Status;
        lead.AssignedTo = currentUser.Id;
        lead.AssignedUser = null;

        ModelState.Remove(nameof(Lead.LeadCode));
        ModelState.Remove(nameof(Lead.CreatedDate));
        ModelState.Remove(nameof(Lead.AssignedTo));

        if (!ModelState.IsValid)
        {
            return View(lead);
        }

        _context.Leads.Add(lead);
        await _context.SaveChangesAsync();
        await _auditService.TryLogAsync("Created", "Lead", lead.LeadId.ToString(), newValue: $"Status={lead.Status}");

        return RedirectToAction("Index", "Lead");
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? searchTerm)
    {
        IQueryable<Lead> leadQuery = _context.Leads;

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var searchPattern = $"%{searchTerm.Trim()}%";
            leadQuery = leadQuery.Where(lead =>
                EF.Functions.ILike(lead.LeadCode, searchPattern) ||
                EF.Functions.ILike(lead.LeadName, searchPattern) ||
                EF.Functions.ILike(lead.Email, searchPattern) ||
                EF.Functions.ILike(lead.Phone, searchPattern) ||
                (lead.CompanyName != null && EF.Functions.ILike(lead.CompanyName, searchPattern)) ||
                EF.Functions.ILike(lead.Source, searchPattern) ||
                EF.Functions.ILike(lead.Status, searchPattern));
        }

        var leads = await leadQuery
            .OrderByDescending(lead => lead.CreatedDate)
            .ToListAsync();

        ViewData["SearchTerm"] = searchTerm;
        ViewData["CurrentUserId"] = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        ViewData["CanConvertAnyLead"] = User.IsInRole("Admin") || User.IsInRole("Manager");
        return View(leads);
    }

    [HttpGet]
    public async Task<IActionResult> Convert(int id)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser is null)
        {
            return Forbid();
        }

        var lead = await GetConvertibleLeads(currentUser.Id)
            .Include(item => item.AssignedUser)
            .FirstOrDefaultAsync(item => item.LeadId == id);
        if (lead is null)
        {
            return NotFound();
        }

        if (lead.Status != nameof(LeadStatus.Qualified))
        {
            TempData["ErrorMessage"] = "Only qualified leads can be converted.";
            return RedirectToAction(nameof(Index));
        }

        ViewData["CurrentUserId"] = currentUser.Id;
        ViewData["CanConvertAnyLead"] = User.IsInRole("Admin") || User.IsInRole("Manager");
        return View(lead);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ConvertConfirmed(int id)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser is null)
        {
            return Forbid();
        }

        await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        try
        {
            var lead = await GetConvertibleLeads(currentUser.Id)
                .FirstOrDefaultAsync(item => item.LeadId == id);
            if (lead is null)
            {
                return NotFound();
            }

            if (lead.Status != nameof(LeadStatus.Qualified))
            {
                TempData["ErrorMessage"] = "Only qualified leads can be converted, and a lead can only be converted once.";
                return RedirectToAction(nameof(Index));
            }

            if (lead.ExpectedValue <= 0)
            {
                ModelState.AddModelError(string.Empty,
                    "Set the lead's expected value to an amount greater than zero before conversion.");
                return View("Convert", lead);
            }

            if (string.IsNullOrWhiteSpace(lead.AssignedTo) ||
                !await _context.Users.AnyAsync(user => user.Id == lead.AssignedTo))
            {
                ModelState.AddModelError(string.Empty,
                    "Assign this lead to an active user before conversion.");
                return View("Convert", lead);
            }

            var email = lead.Email.Trim();
            var phone = lead.Phone.Trim();
            var emailExists = await _context.Customers
                .AnyAsync(customer => customer.Email.ToUpper() == email.ToUpperInvariant());
            var phoneExists = await _context.Customers
                .AnyAsync(customer => customer.Phone.Trim() == phone);

            if (emailExists || phoneExists)
            {
                var duplicateField = emailExists ? "email" : "phone number";
                ModelState.AddModelError(string.Empty,
                    $"A customer with this {duplicateField} already exists. Resolve the duplicate before converting this lead.");
                return View("Convert", lead);
            }

            var customer = new Customer
            {
                CustomerCode = $"CUS-{Guid.NewGuid():N}"[..12].ToUpperInvariant(),
                CustomerName = lead.LeadName,
                Email = email,
                Phone = phone,
                CompanyName = lead.CompanyName,
                Status = "Active",
                CreatedBy = User.Identity?.Name ?? currentUser.UserName ?? currentUser.Id,
                CreatedDate = DateTime.UtcNow
            };

            _context.Customers.Add(customer);
            await _context.SaveChangesAsync();

            var opportunity = new Opportunity
            {
                OpportunityName = string.IsNullOrWhiteSpace(lead.CompanyName)
                    ? lead.LeadName
                    : $"{lead.CompanyName} - {lead.LeadName}",
                CustomerId = customer.CustomerId,
                LeadId = lead.LeadId,
                Amount = lead.ExpectedValue,
                Stage = nameof(OpportunityStage.Qualification),
                Probability = 0,
                ExpectedCloseDate = DateOnly.FromDateTime(DateTime.UtcNow),
                Status = nameof(OpportunityStatus.Open),
                CreatedDate = DateTime.UtcNow,
                AssignedTo = lead.AssignedTo
            };

            _context.Opportunities.Add(opportunity);
            await _context.SaveChangesAsync();

            lead.Status = nameof(LeadStatus.Converted);
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            await _auditService.TryLogAsync(
                "Converted",
                "Lead",
                lead.LeadId.ToString(),
                oldValue: $"Status={nameof(LeadStatus.Qualified)}",
                newValue: $"Status={nameof(LeadStatus.Converted)};CustomerId={customer.CustomerId};OpportunityId={opportunity.OpportunityId}");
            TempData["SuccessMessage"] = "Lead converted successfully into a customer and opportunity.";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception exception) when (exception is DbUpdateException or PostgresException)
        {
            await transaction.RollbackAsync();
            _logger.LogError(exception, "Failed to convert lead {LeadId}.", id);
            TempData["ErrorMessage"] = "Lead conversion could not be completed. No changes were saved.";
            return RedirectToAction(nameof(Index));
        }
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var lead = await _context.Leads
            .Include(item => item.AssignedUser)
            .FirstOrDefaultAsync(item => item.LeadId == id);
        if (lead is null)
        {
            return NotFound();
        }

        return View(lead);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var lead = await _context.Leads.FindAsync(id);
        if (lead is null)
        {
            return NotFound();
        }

        return View(lead);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        int id,
        [Bind(
            nameof(Lead.LeadName),
            nameof(Lead.Email),
            nameof(Lead.Phone),
            nameof(Lead.CompanyName),
            nameof(Lead.Source),
            nameof(Lead.ExpectedValue),
            nameof(Lead.Status))] Lead lead)
    {
        var existingLead = await _context.Leads.FindAsync(id);
        if (existingLead is null)
        {
            return NotFound();
        }

        lead.LeadId = existingLead.LeadId;
        lead.LeadCode = existingLead.LeadCode;
        lead.CreatedDate = existingLead.CreatedDate;
        lead.AssignedTo = existingLead.AssignedTo;

        ModelState.Remove(nameof(Lead.LeadCode));
        ModelState.Remove(nameof(Lead.CreatedDate));

        if (!ModelState.IsValid)
        {
            return View(lead);
        }

        var oldValue = $"Status={existingLead.Status};ExpectedValue={existingLead.ExpectedValue}";
        existingLead.LeadName = lead.LeadName;
        existingLead.Email = lead.Email;
        existingLead.Phone = lead.Phone;
        existingLead.CompanyName = lead.CompanyName;
        existingLead.Source = lead.Source;
        existingLead.ExpectedValue = lead.ExpectedValue;
        existingLead.Status = lead.Status;

        await _context.SaveChangesAsync();
        await _auditService.TryLogAsync(
            "Updated",
            "Lead",
            existingLead.LeadId.ToString(),
            oldValue,
            $"Status={existingLead.Status};ExpectedValue={existingLead.ExpectedValue}");
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Delete(int id)
    {
        var lead = await _context.Leads
            .Include(item => item.AssignedUser)
            .FirstOrDefaultAsync(item => item.LeadId == id);
        if (lead is null)
        {
            return NotFound();
        }

        return View(lead);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var lead = await _context.Leads.FindAsync(id);
        if (lead is null)
        {
            return NotFound();
        }

        var leadId = lead.LeadId.ToString();
        _context.Leads.Remove(lead);
        await _context.SaveChangesAsync();
        await _auditService.TryLogAsync("Deleted", "Lead", leadId);
        return RedirectToAction(nameof(Index));
    }

    private IQueryable<Lead> GetConvertibleLeads(string currentUserId)
    {
        var leads = _context.Leads.AsQueryable();
        if (!User.IsInRole("Admin") && !User.IsInRole("Manager"))
        {
            leads = leads.Where(lead => lead.AssignedTo == currentUserId);
        }

        return leads;
    }
}