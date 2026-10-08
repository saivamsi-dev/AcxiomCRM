using AcxiomCRM.Data;
using AcxiomCRM.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Controllers;

[Authorize(Policy = "SalesAccess")]
public class FollowUpController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public FollowUpController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        DateOnly? followUpDate,
        string? status,
        string? assignedTo,
        int? customerId,
        int? leadId)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser is null)
        {
            return Forbid();
        }

        var canViewAll = User.IsInRole("Admin") || User.IsInRole("Manager");
        if (!canViewAll)
        {
            assignedTo = currentUser.Id;
        }

        IQueryable<FollowUp> followUpQuery = _context.FollowUps
            .AsNoTracking()
            .Include(followUp => followUp.Customer)
            .Include(followUp => followUp.Lead)
            .Include(followUp => followUp.AssignedUser);

        if (!canViewAll || !string.IsNullOrWhiteSpace(assignedTo))
        {
            followUpQuery = followUpQuery.Where(followUp => followUp.AssignedTo == assignedTo);
        }

        if (followUpDate.HasValue)
        {
            followUpQuery = followUpQuery.Where(followUp => followUp.FollowUpDate == followUpDate);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            if (Enum.TryParse<FollowUpStatus>(status, ignoreCase: false, out var parsedStatus) &&
                Enum.IsDefined(parsedStatus))
            {
                followUpQuery = followUpQuery.Where(followUp => followUp.Status == parsedStatus.ToString());
            }
            else
            {
                status = null;
            }
        }

        if (customerId.HasValue)
        {
            followUpQuery = followUpQuery.Where(followUp => followUp.CustomerId == customerId);
        }

        if (leadId.HasValue)
        {
            followUpQuery = followUpQuery.Where(followUp => followUp.LeadId == leadId);
        }

        var followUps = await followUpQuery
            .OrderBy(followUp => followUp.FollowUpDate)
            .ThenBy(followUp => followUp.FollowUpId)
            .ToListAsync();

        await PopulateFilterSelectionsAsync(
            canViewAll,
            currentUser,
            followUpDate,
            status,
            assignedTo,
            customerId,
            leadId);

        return View(followUps);
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        await PopulateRelatedSelectionsAsync();
        return View(new FollowUp
        {
            FollowUpDate = DateOnly.FromDateTime(DateTime.UtcNow),
            Status = nameof(FollowUpStatus.Planned)
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        [Bind(
            nameof(FollowUp.CustomerId),
            nameof(FollowUp.LeadId),
            nameof(FollowUp.FollowUpDate),
            nameof(FollowUp.FollowUpType),
            nameof(FollowUp.Remarks),
            nameof(FollowUp.Status))] FollowUp followUp)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser is null)
        {
            return Forbid();
        }

        followUp.FollowUpId = 0;
        followUp.AssignedTo = currentUser.Id;
        followUp.AssignedUser = currentUser;
        ModelState.Remove(nameof(FollowUp.FollowUpId));
        ModelState.Remove(nameof(FollowUp.AssignedTo));

        if (!Enum.TryParse<FollowUpStatus>(followUp.Status, ignoreCase: false, out var parsedStatus) ||
            !Enum.IsDefined(parsedStatus))
        {
            ModelState.AddModelError(nameof(FollowUp.Status), "Select a valid follow-up status.");
        }

        if (followUp.CustomerId.HasValue &&
            !await _context.Customers.AnyAsync(customer => customer.CustomerId == followUp.CustomerId.Value))
        {
            ModelState.AddModelError(nameof(FollowUp.CustomerId), "Select an existing customer.");
        }

        if (followUp.LeadId.HasValue &&
            !await _context.Leads.AnyAsync(lead => lead.LeadId == followUp.LeadId.Value))
        {
            ModelState.AddModelError(nameof(FollowUp.LeadId), "Select an existing lead.");
        }

        if (!ModelState.IsValid)
        {
            await PopulateRelatedSelectionsAsync(followUp.CustomerId, followUp.LeadId);
            return View(followUp);
        }

        _context.FollowUps.Add(followUp);
        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    private async Task PopulateRelatedSelectionsAsync(int? selectedCustomerId = null, int? selectedLeadId = null)
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

    private async Task PopulateFilterSelectionsAsync(
        bool canViewAll,
        ApplicationUser currentUser,
        DateOnly? followUpDate,
        string? status,
        string? assignedTo,
        int? customerId,
        int? leadId)
    {
        ViewData["FollowUpDate"] = followUpDate?.ToString("yyyy-MM-dd");
        ViewData["StatusFilter"] = status;
        ViewData["AssignedToFilter"] = assignedTo;
        ViewData["CustomerFilter"] = customerId;
        ViewData["LeadFilter"] = leadId;

        ViewBag.Statuses = Enum.GetNames<FollowUpStatus>()
            .Select(value => new SelectListItem(value, value, value == status))
            .ToList();

        ViewBag.Customers = await _context.Customers
            .AsNoTracking()
            .OrderBy(customer => customer.CustomerName)
            .Select(customer => new SelectListItem(
                customer.CustomerName,
                customer.CustomerId.ToString(),
                customer.CustomerId == customerId))
            .ToListAsync();

        ViewBag.Leads = await _context.Leads
            .AsNoTracking()
            .OrderBy(lead => lead.LeadName)
            .Select(lead => new SelectListItem(
                $"{lead.LeadCode} - {lead.LeadName}",
                lead.LeadId.ToString(),
                lead.LeadId == leadId))
            .ToListAsync();

        var users = canViewAll
            ? await _context.Users.AsNoTracking().OrderBy(user => user.FullName).ToListAsync()
            : [currentUser];

        ViewBag.AssignedUsers = users
            .Select(user => new SelectListItem(
                string.IsNullOrWhiteSpace(user.FullName) ? user.Email ?? user.UserName ?? user.Id : user.FullName,
                user.Id,
                user.Id == assignedTo))
            .ToList();
    }
}