using AcxiomCRM.Data;
using AcxiomCRM.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Controllers;

[Authorize(Policy = "SalesAccess")]
public class OpportunityController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public OpportunityController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var opportunities = await _context.Opportunities
            .AsNoTracking()
            .Include(opportunity => opportunity.Customer)
            .Include(opportunity => opportunity.Lead)
            .Include(opportunity => opportunity.AssignedUser)
            .OrderByDescending(opportunity => opportunity.CreatedDate)
            .ToListAsync();

        return View(opportunities);
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        await PopulateSelectionsAsync();
        return View(new Opportunity());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        [Bind(
            nameof(Opportunity.OpportunityName),
            nameof(Opportunity.CustomerId),
            nameof(Opportunity.LeadId),
            nameof(Opportunity.Amount),
            nameof(Opportunity.Stage),
            nameof(Opportunity.Probability),
            nameof(Opportunity.ExpectedCloseDate),
            nameof(Opportunity.Status))] Opportunity opportunity)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser is null)
        {
            return Forbid();
        }

        opportunity.OpportunityId = 0;
        opportunity.CreatedDate = DateTime.UtcNow;
        opportunity.AssignedTo = currentUser.Id;
        opportunity.AssignedUser = currentUser;

        ModelState.Remove(nameof(Opportunity.OpportunityId));
        ModelState.Remove(nameof(Opportunity.CreatedDate));
        ModelState.Remove(nameof(Opportunity.AssignedTo));

        if (!await _context.Customers.AnyAsync(customer => customer.CustomerId == opportunity.CustomerId))
        {
            ModelState.AddModelError(nameof(Opportunity.CustomerId), "Select an existing customer.");
        }

        if (opportunity.LeadId.HasValue &&
            !await _context.Leads.AnyAsync(lead => lead.LeadId == opportunity.LeadId.Value))
        {
            ModelState.AddModelError(nameof(Opportunity.LeadId), "Select an existing lead or leave this field empty.");
        }

        if (!ModelState.IsValid)
        {
            await PopulateSelectionsAsync(opportunity.CustomerId, opportunity.LeadId);
            return View(opportunity);
        }

        _context.Opportunities.Add(opportunity);
        await _context.SaveChangesAsync();

        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var opportunity = await _context.Opportunities.FindAsync(id);
        if (opportunity is null)
        {
            return NotFound();
        }

        await PopulateSelectionsAsync(opportunity.CustomerId, opportunity.LeadId);
        return View(opportunity);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        int id,
        [Bind(
            nameof(Opportunity.OpportunityName),
            nameof(Opportunity.CustomerId),
            nameof(Opportunity.LeadId),
            nameof(Opportunity.Amount),
            nameof(Opportunity.Stage),
            nameof(Opportunity.Probability),
            nameof(Opportunity.ExpectedCloseDate),
            nameof(Opportunity.Status))] Opportunity opportunity)
    {
        var existingOpportunity = await _context.Opportunities.FindAsync(id);
        if (existingOpportunity is null)
        {
            return NotFound();
        }

        opportunity.OpportunityId = existingOpportunity.OpportunityId;
        opportunity.CreatedDate = existingOpportunity.CreatedDate;
        opportunity.AssignedTo = existingOpportunity.AssignedTo;

        ModelState.Remove(nameof(Opportunity.OpportunityId));
        ModelState.Remove(nameof(Opportunity.CreatedDate));
        ModelState.Remove(nameof(Opportunity.AssignedTo));

        if (opportunity.Amount <= 0)
        {
            ModelState.AddModelError(nameof(Opportunity.Amount), "Amount must be greater than zero.");
        }

        if (!Enum.TryParse<OpportunityStage>(opportunity.Stage, ignoreCase: false, out var stage) ||
            !Enum.IsDefined(stage))
        {
            ModelState.AddModelError(nameof(Opportunity.Stage), "Select a valid opportunity stage.");
        }

        if (!await _context.Customers.AnyAsync(customer => customer.CustomerId == opportunity.CustomerId))
        {
            ModelState.AddModelError(nameof(Opportunity.CustomerId), "Select an existing customer.");
        }

        if (opportunity.LeadId.HasValue &&
            !await _context.Leads.AnyAsync(lead => lead.LeadId == opportunity.LeadId.Value))
        {
            ModelState.AddModelError(nameof(Opportunity.LeadId), "Select an existing lead or leave this field empty.");
        }

        if (!ModelState.IsValid)
        {
            await PopulateSelectionsAsync(opportunity.CustomerId, opportunity.LeadId);
            return View(opportunity);
        }

        existingOpportunity.OpportunityName = opportunity.OpportunityName;
        existingOpportunity.CustomerId = opportunity.CustomerId;
        existingOpportunity.LeadId = opportunity.LeadId;
        existingOpportunity.Amount = opportunity.Amount;
        existingOpportunity.Stage = opportunity.Stage;
        existingOpportunity.Probability = opportunity.Probability;
        existingOpportunity.ExpectedCloseDate = opportunity.ExpectedCloseDate;
        existingOpportunity.Status = opportunity.Status;

        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var opportunity = await _context.Opportunities
            .AsNoTracking()
            .Include(item => item.Customer)
            .Include(item => item.Lead)
            .Include(item => item.AssignedUser)
            .FirstOrDefaultAsync(item => item.OpportunityId == id);
        if (opportunity is null)
        {
            return NotFound();
        }

        return View(opportunity);
    }

    [HttpGet]
    public async Task<IActionResult> Delete(int id)
    {
        var opportunity = await _context.Opportunities
            .AsNoTracking()
            .Include(item => item.Customer)
            .Include(item => item.Lead)
            .FirstOrDefaultAsync(item => item.OpportunityId == id);
        if (opportunity is null)
        {
            return NotFound();
        }

        return View(opportunity);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var opportunity = await _context.Opportunities.FindAsync(id);
        if (opportunity is null)
        {
            return NotFound();
        }

        _context.Opportunities.Remove(opportunity);
        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    private async Task PopulateSelectionsAsync(int? selectedCustomerId = null, int? selectedLeadId = null)
    {
        ViewBag.Customers = await _context.Customers
            .AsNoTracking()
            .OrderBy(customer => customer.CustomerName)
            .Select(customer => new SelectListItem
            {
                Value = customer.CustomerId.ToString(),
                Text = customer.CustomerName,
                Selected = customer.CustomerId == selectedCustomerId
            })
            .ToListAsync();

        ViewBag.Leads = await _context.Leads
            .AsNoTracking()
            .OrderBy(lead => lead.LeadName)
            .Select(lead => new SelectListItem
            {
                Value = lead.LeadId.ToString(),
                Text = $"{lead.LeadCode} - {lead.LeadName}",
                Selected = lead.LeadId == selectedLeadId
            })
            .ToListAsync();
    }
}