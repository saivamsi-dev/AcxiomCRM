using AcxiomCRM.Data;
using AcxiomCRM.Models;
using AcxiomCRM.Services;
using ActivityEntity = AcxiomCRM.Models.Activity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Controllers;

[Authorize(Policy = "SalesAccess")]
public class ActivityController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly AuditService _auditService;

    public ActivityController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        AuditService auditService)
    {
        _context = context;
        _userManager = userManager;
        _auditService = auditService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        string? activityType,
        DateOnly? activityDate,
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

        IQueryable<ActivityEntity> activityQuery = _context.Activities
            .AsNoTracking()
            .Include(activity => activity.Customer)
            .Include(activity => activity.Lead)
            .Include(activity => activity.AssignedUser);

        if (!canViewAll || !string.IsNullOrWhiteSpace(assignedTo))
        {
            activityQuery = activityQuery.Where(activity => activity.AssignedTo == assignedTo);
        }

        if (!string.IsNullOrWhiteSpace(activityType))
        {
            if (Enum.TryParse<ActivityType>(activityType, ignoreCase: false, out var parsedType) &&
                Enum.IsDefined(parsedType))
            {
                activityQuery = activityQuery.Where(activity => activity.ActivityType == parsedType.ToString());
            }
            else
            {
                activityType = null;
            }
        }

        if (activityDate.HasValue)
        {
            activityQuery = activityQuery.Where(activity => activity.ActivityDate == activityDate);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            if (Enum.TryParse<ActivityStatus>(status, ignoreCase: false, out var parsedStatus) &&
                Enum.IsDefined(parsedStatus))
            {
                activityQuery = activityQuery.Where(activity => activity.Status == parsedStatus.ToString());
            }
            else
            {
                status = null;
            }
        }

        if (customerId.HasValue)
        {
            activityQuery = activityQuery.Where(activity => activity.CustomerId == customerId);
        }

        if (leadId.HasValue)
        {
            activityQuery = activityQuery.Where(activity => activity.LeadId == leadId);
        }

        var activities = await activityQuery
            .OrderByDescending(activity => activity.ActivityDate)
            .ThenByDescending(activity => activity.ActivityId)
            .ToListAsync();

        await PopulateFilterSelectionsAsync(
            canViewAll,
            currentUser,
            activityType,
            activityDate,
            status,
            assignedTo,
            customerId,
            leadId);

        return View(activities);
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        await PopulateRelatedSelectionsAsync();
        return View(new ActivityEntity
        {
            ActivityDate = DateOnly.FromDateTime(DateTime.UtcNow),
            Status = nameof(ActivityStatus.Planned)
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        [Bind(
            nameof(ActivityEntity.ActivityType),
            nameof(ActivityEntity.Subject),
            nameof(ActivityEntity.Description),
            nameof(ActivityEntity.ActivityDate),
            nameof(ActivityEntity.CustomerId),
            nameof(ActivityEntity.LeadId),
            nameof(ActivityEntity.Status))] ActivityEntity activity)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser is null)
        {
            return Forbid();
        }

        activity.ActivityId = 0;
        activity.AssignedTo = currentUser.Id;
        activity.AssignedUser = currentUser;
        ModelState.Remove(nameof(ActivityEntity.ActivityId));
        ModelState.Remove(nameof(ActivityEntity.AssignedTo));

        if (!Enum.TryParse<ActivityType>(activity.ActivityType, ignoreCase: false, out var parsedType) ||
            !Enum.IsDefined(parsedType))
        {
            ModelState.AddModelError(nameof(ActivityEntity.ActivityType), "Select a valid activity type.");
        }

        if (!Enum.TryParse<ActivityStatus>(activity.Status, ignoreCase: false, out var parsedStatus) ||
            !Enum.IsDefined(parsedStatus))
        {
            ModelState.AddModelError(nameof(ActivityEntity.Status), "Select a valid activity status.");
        }

        if (activity.CustomerId.HasValue &&
            !await _context.Customers.AnyAsync(customer => customer.CustomerId == activity.CustomerId.Value))
        {
            ModelState.AddModelError(nameof(ActivityEntity.CustomerId), "Select an existing customer.");
        }

        if (activity.LeadId.HasValue &&
            !await _context.Leads.AnyAsync(lead => lead.LeadId == activity.LeadId.Value))
        {
            ModelState.AddModelError(nameof(ActivityEntity.LeadId), "Select an existing lead.");
        }

        if (!ModelState.IsValid)
        {
            await PopulateRelatedSelectionsAsync(activity.CustomerId, activity.LeadId);
            return View(activity);
        }

        _context.Activities.Add(activity);
        await _context.SaveChangesAsync();
        await _auditService.TryLogAsync(
            "Created",
            "Activity",
            activity.ActivityId.ToString(),
            newValue: $"Type={activity.ActivityType};Status={activity.Status}");
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser is null)
        {
            return Forbid();
        }

        var activity = await GetVisibleActivities(currentUser.Id)
            .FirstOrDefaultAsync(item => item.ActivityId == id);
        if (activity is null)
        {
            return NotFound();
        }

        await PopulateRelatedSelectionsAsync(activity.CustomerId, activity.LeadId);
        return View(activity);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        int id,
        [Bind(
            nameof(ActivityEntity.ActivityType),
            nameof(ActivityEntity.Subject),
            nameof(ActivityEntity.Description),
            nameof(ActivityEntity.ActivityDate),
            nameof(ActivityEntity.CustomerId),
            nameof(ActivityEntity.LeadId),
            nameof(ActivityEntity.Status))] ActivityEntity activity)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser is null)
        {
            return Forbid();
        }

        var existingActivity = await GetVisibleActivities(currentUser.Id)
            .FirstOrDefaultAsync(item => item.ActivityId == id);
        if (existingActivity is null)
        {
            return NotFound();
        }

        activity.ActivityId = existingActivity.ActivityId;
        activity.AssignedTo = existingActivity.AssignedTo;
        ModelState.Remove(nameof(ActivityEntity.ActivityId));
        ModelState.Remove(nameof(ActivityEntity.AssignedTo));

        if (!Enum.TryParse<ActivityType>(activity.ActivityType, ignoreCase: false, out var parsedType) ||
            !Enum.IsDefined(parsedType))
        {
            ModelState.AddModelError(nameof(ActivityEntity.ActivityType), "Select a valid activity type.");
        }

        if (!Enum.TryParse<ActivityStatus>(activity.Status, ignoreCase: false, out var parsedStatus) ||
            !Enum.IsDefined(parsedStatus))
        {
            ModelState.AddModelError(nameof(ActivityEntity.Status), "Select a valid activity status.");
        }

        if (activity.CustomerId.HasValue &&
            !await _context.Customers.AnyAsync(customer => customer.CustomerId == activity.CustomerId.Value))
        {
            ModelState.AddModelError(nameof(ActivityEntity.CustomerId), "Select an existing customer.");
        }

        if (activity.LeadId.HasValue &&
            !await _context.Leads.AnyAsync(lead => lead.LeadId == activity.LeadId.Value))
        {
            ModelState.AddModelError(nameof(ActivityEntity.LeadId), "Select an existing lead.");
        }

        if (!ModelState.IsValid)
        {
            await PopulateRelatedSelectionsAsync(activity.CustomerId, activity.LeadId);
            return View(activity);
        }

        var oldValue = $"Type={existingActivity.ActivityType};Status={existingActivity.Status}";
        existingActivity.ActivityType = activity.ActivityType;
        existingActivity.Subject = activity.Subject;
        existingActivity.Description = activity.Description;
        existingActivity.ActivityDate = activity.ActivityDate;
        existingActivity.CustomerId = activity.CustomerId;
        existingActivity.LeadId = activity.LeadId;
        existingActivity.Status = activity.Status;

        await _context.SaveChangesAsync();
        await _auditService.TryLogAsync(
            "Updated",
            "Activity",
            existingActivity.ActivityId.ToString(),
            oldValue,
            $"Type={existingActivity.ActivityType};Status={existingActivity.Status}");
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser is null)
        {
            return Forbid();
        }

        var activity = await GetVisibleActivities(currentUser.Id)
            .AsNoTracking()
            .Include(item => item.Customer)
            .Include(item => item.Lead)
            .Include(item => item.AssignedUser)
            .FirstOrDefaultAsync(item => item.ActivityId == id);
        if (activity is null)
        {
            return NotFound();
        }

        return View(activity);
    }

    [HttpGet]
    public async Task<IActionResult> Delete(int id)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser is null)
        {
            return Forbid();
        }

        var activity = await GetVisibleActivities(currentUser.Id)
            .AsNoTracking()
            .Include(item => item.Customer)
            .Include(item => item.Lead)
            .FirstOrDefaultAsync(item => item.ActivityId == id);
        if (activity is null)
        {
            return NotFound();
        }

        return View(activity);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser is null)
        {
            return Forbid();
        }

        var activity = await GetVisibleActivities(currentUser.Id)
            .FirstOrDefaultAsync(item => item.ActivityId == id);
        if (activity is null)
        {
            return NotFound();
        }

        var activityId = activity.ActivityId.ToString();
        _context.Activities.Remove(activity);
        await _context.SaveChangesAsync();
        await _auditService.TryLogAsync("Deleted", "Activity", activityId);
        return RedirectToAction(nameof(Index));
    }

    private IQueryable<ActivityEntity> GetVisibleActivities(string currentUserId)
    {
        var activities = _context.Activities.AsQueryable();
        if (!User.IsInRole("Admin") && !User.IsInRole("Manager"))
        {
            activities = activities.Where(activity => activity.AssignedTo == currentUserId);
        }

        return activities;
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
        string? activityType,
        DateOnly? activityDate,
        string? status,
        string? assignedTo,
        int? customerId,
        int? leadId)
    {
        ViewData["ActivityTypeFilter"] = activityType;
        ViewData["ActivityDateFilter"] = activityDate?.ToString("yyyy-MM-dd");
        ViewData["StatusFilter"] = status;
        ViewData["AssignedToFilter"] = assignedTo;
        ViewData["CustomerFilter"] = customerId;
        ViewData["LeadFilter"] = leadId;

        ViewBag.ActivityTypes = Enum.GetNames<ActivityType>()
            .Select(value => new SelectListItem(value, value, value == activityType))
            .ToList();
        ViewBag.Statuses = Enum.GetNames<ActivityStatus>()
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