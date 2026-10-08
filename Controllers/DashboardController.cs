using AcxiomCRM.Data;
using AcxiomCRM.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Controllers;

[Authorize(Policy = "SalesAccess")]
public class DashboardController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public DashboardController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        string? period = "ThisMonth",
        DateOnly? startDate = null,
        DateOnly? endDate = null)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser is null)
        {
            return Forbid();
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var selectedPeriod = period?.ToLowerInvariant() switch
        {
            "today" => "Today",
            "thisweek" => "ThisWeek",
            "custom" => "Custom",
            _ => "ThisMonth"
        };

        DateOnly? rangeStart;
        DateOnly? rangeEnd;
        string? dateFilterError = null;

        switch (selectedPeriod)
        {
            case "Today":
                rangeStart = today;
                rangeEnd = today;
                break;
            case "ThisWeek":
                rangeStart = today.AddDays(-((int)today.DayOfWeek + 6) % 7);
                rangeEnd = today;
                break;
            case "Custom":
                rangeStart = startDate;
                rangeEnd = endDate;
                var dateBindingFailed =
                    (ModelState.TryGetValue(nameof(startDate), out var startState) && startState.Errors.Count > 0) ||
                    (ModelState.TryGetValue(nameof(endDate), out var endState) && endState.Errors.Count > 0);
                if (dateBindingFailed)
                {
                    dateFilterError = "Enter valid start and end dates.";
                }
                else if (!rangeStart.HasValue || !rangeEnd.HasValue)
                {
                    dateFilterError = "Select both a start date and an end date for a custom range.";
                }
                else if (rangeStart.Value > rangeEnd.Value)
                {
                    dateFilterError = "The start date must be on or before the end date.";
                }
                break;
            default:
                rangeStart = new DateOnly(today.Year, today.Month, 1);
                rangeEnd = today;
                break;
        }

        if (!string.IsNullOrEmpty(dateFilterError))
        {
            return View(new DashboardViewModel
            {
                Period = selectedPeriod,
                StartDate = rangeStart,
                EndDate = rangeEnd,
                DateFilterError = dateFilterError,
                HasValidDateRange = false
            });
        }

        var start = rangeStart!.Value;
        var end = rangeEnd!.Value;
        var startUtc = start.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var endUtcExclusive = end.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var canViewAll = User.IsInRole("Admin") || User.IsInRole("Manager");
        var customerOwner = User.Identity?.Name ?? currentUser.UserName ?? currentUser.Id;

        var customersInRange = _context.Customers
            .AsNoTracking()
            .Where(customer => customer.CreatedDate >= startUtc && customer.CreatedDate < endUtcExclusive);
        var leadsInRange = _context.Leads
            .AsNoTracking()
            .Where(lead => lead.CreatedDate >= startUtc && lead.CreatedDate < endUtcExclusive);
        var authorizedOpportunities = _context.Opportunities.AsNoTracking();

        if (!canViewAll)
        {
            customersInRange = customersInRange.Where(customer => customer.CreatedBy == customerOwner);
            leadsInRange = leadsInRange.Where(lead => lead.AssignedTo == currentUser.Id);
            authorizedOpportunities = authorizedOpportunities.Where(opportunity => opportunity.AssignedTo == currentUser.Id);
        }

        var opportunitiesInRange = authorizedOpportunities
            .Where(opportunity => opportunity.CreatedDate >= startUtc && opportunity.CreatedDate < endUtcExclusive);

        var leadStatusRows = await leadsInRange
            .GroupBy(lead => lead.Status)
            .Select(group => new { Status = group.Key, Count = group.Count() })
            .ToListAsync();

        var leadStatusLabels = Enum.GetNames<LeadStatus>();
        var leadStatusLookup = leadStatusRows.ToDictionary(row => row.Status, row => row.Count);

        var opportunityStageRows = await opportunitiesInRange
            .GroupBy(opportunity => opportunity.Stage)
            .Select(group => new { Stage = group.Key, Amount = group.Sum(opportunity => opportunity.Amount) })
            .ToListAsync();
        var opportunityStageLabels = Enum.GetNames<OpportunityStage>();
        var opportunityStageLookup = opportunityStageRows.ToDictionary(row => row.Stage, row => row.Amount);

        var monthlyRows = await authorizedOpportunities
            .Where(opportunity => opportunity.ExpectedCloseDate.HasValue &&
                opportunity.ExpectedCloseDate.Value >= start && opportunity.ExpectedCloseDate.Value <= end)
            .GroupBy(opportunity => new
            {
                Year = opportunity.ExpectedCloseDate!.Value.Year,
                Month = opportunity.ExpectedCloseDate!.Value.Month,
                opportunity.Status
            })
            .Select(group => new
            {
                group.Key.Year,
                group.Key.Month,
                group.Key.Status,
                Amount = group.Sum(opportunity => opportunity.Amount)
            })
            .ToListAsync();

        var monthlyOutcomeData = monthlyRows
            .GroupBy(row => new { row.Year, row.Month })
            .OrderBy(group => group.Key.Year)
            .ThenBy(group => group.Key.Month)
            .Select(group => new MonthlyOutcomePoint
            {
                Month = new DateOnly(group.Key.Year, group.Key.Month, 1).ToString("MMM yyyy"),
                WonSales = group.Where(row => row.Status == nameof(OpportunityStatus.Won)).Sum(row => row.Amount),
                LostValue = group.Where(row => row.Status == nameof(OpportunityStatus.Lost)).Sum(row => row.Amount),
                OpenValue = group.Where(row => row.Status == nameof(OpportunityStatus.Open)).Sum(row => row.Amount)
            })
            .ToList();

        var model = new DashboardViewModel
        {
            Period = selectedPeriod,
            StartDate = start,
            EndDate = end,
            TotalCustomers = await customersInRange.CountAsync(),
            TotalLeads = await leadsInRange.CountAsync(),
            OpenLeads = await leadsInRange.CountAsync(lead =>
                lead.Status == nameof(LeadStatus.New) ||
                lead.Status == nameof(LeadStatus.Contacted) ||
                lead.Status == nameof(LeadStatus.Qualified)),
            TotalOpportunities = await opportunitiesInRange.CountAsync(),
            OpenOpportunities = await opportunitiesInRange.CountAsync(opportunity => opportunity.Status == nameof(OpportunityStatus.Open)),
            WonOpportunities = await opportunitiesInRange.CountAsync(opportunity => opportunity.Status == nameof(OpportunityStatus.Won)),
            LostOpportunities = await opportunitiesInRange.CountAsync(opportunity => opportunity.Status == nameof(OpportunityStatus.Lost)),
            TotalPipelineValue = await opportunitiesInRange
                .Where(opportunity => opportunity.Status == nameof(OpportunityStatus.Open))
                .SumAsync(opportunity => (decimal?)opportunity.Amount) ?? 0m,
            LeadStatusData = leadStatusLabels
                .Select(label => new DashboardChartPoint { Label = label, Value = leadStatusLookup.GetValueOrDefault(label) })
                .ToList(),
            OpportunityPipelineData = opportunityStageLabels
                .Select(label => new DashboardChartPoint { Label = label, Value = opportunityStageLookup.GetValueOrDefault(label) })
                .ToList(),
            MonthlyOutcomesData = monthlyOutcomeData
        };

        return View(model);
    }
}
