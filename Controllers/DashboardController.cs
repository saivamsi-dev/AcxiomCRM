using AcxiomCRM.Data;
using AcxiomCRM.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Controllers;

[Authorize(Policy = "SalesAccess")]
public class DashboardController : Controller
{
    private readonly ApplicationDbContext _context;

    public DashboardController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        string? period = "Month",
        DateOnly? startDate = null,
        DateOnly? endDate = null)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var selectedPeriod = period?.ToLowerInvariant() switch
        {
            "today" => "Today",
            "week" => "Week",
            "custom" => "Custom",
            _ => "Month"
        };

        DateOnly rangeStart;
        DateOnly rangeEnd;
        string? dateFilterError = null;

        switch (selectedPeriod)
        {
            case "Today":
                rangeStart = today;
                rangeEnd = today;
                break;
            case "Week":
                rangeStart = today.AddDays(-((int)today.DayOfWeek + 6) % 7);
                rangeEnd = today;
                break;
            case "Custom":
                rangeStart = startDate ?? new DateOnly(today.Year, today.Month, 1);
                rangeEnd = endDate ?? today;
                if (rangeStart > rangeEnd)
                {
                    dateFilterError = "The start date must be on or before the end date.";
                    rangeStart = rangeEnd;
                }
                break;
            default:
                rangeStart = new DateOnly(today.Year, today.Month, 1);
                rangeEnd = today;
                break;
        }

        var startUtc = rangeStart.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var endUtcExclusive = rangeEnd.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        var customersInRange = _context.Customers
            .AsNoTracking()
            .Where(customer => customer.CreatedDate >= startUtc && customer.CreatedDate < endUtcExclusive);
        var leadsInRange = _context.Leads
            .AsNoTracking()
            .Where(lead => lead.CreatedDate >= startUtc && lead.CreatedDate < endUtcExclusive);

        var leadStatusRows = await leadsInRange
            .GroupBy(lead => lead.Status)
            .Select(group => new { Status = group.Key, Count = group.Count() })
            .ToListAsync();

        var model = new DashboardViewModel
        {
            Period = selectedPeriod,
            StartDate = rangeStart,
            EndDate = rangeEnd,
            DateFilterError = dateFilterError,
            TotalCustomers = await customersInRange.CountAsync(),
            TotalLeads = await leadsInRange.CountAsync(),
            OpenLeads = await leadsInRange.CountAsync(lead =>
                lead.Status == nameof(LeadStatus.New) ||
                lead.Status == nameof(LeadStatus.Contacted) ||
                lead.Status == nameof(LeadStatus.Qualified)),
            LeadStatusData = leadStatusRows
                .Select(row => new DashboardChartPoint { Label = row.Status, Value = row.Count })
                .ToList()
        };

        return View(model);
    }
}
