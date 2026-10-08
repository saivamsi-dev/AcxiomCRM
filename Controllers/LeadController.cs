using AcxiomCRM.Data;
using AcxiomCRM.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Controllers;

[Authorize(Policy = "SalesAccess")]
public class LeadController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public LeadController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
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
        return View(leads);
    }
}