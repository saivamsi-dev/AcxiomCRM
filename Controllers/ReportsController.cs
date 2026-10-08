using AcxiomCRM.Data;
using AcxiomCRM.Models;
using AcxiomCRM.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Controllers;

[Authorize(Policy = "SalesAccess")]
public class ReportsController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public ReportsController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        string? reportType = "leads",
        DateOnly? fromDate = null,
        DateOnly? toDate = null,
        string? status = null,
        string? stage = null,
        string? source = null,
        string? assignedTo = null,
        string? searchText = null)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser is null)
        {
            return Forbid();
        }

        reportType = NormalizeReportType(reportType);
        var isBroadScope = User.IsInRole("Admin") || User.IsInRole("Manager");
        var customerOwner = User.Identity?.Name ?? currentUser.UserName ?? currentUser.Id;
        if (!isBroadScope)
        {
            assignedTo = reportType == "customers" ? customerOwner : currentUser.Id;
        }

        var customers = _context.Customers.AsNoTracking();
        var leads = _context.Leads.AsNoTracking();
        IQueryable<Opportunity> opportunities = _context.Opportunities
            .AsNoTracking()
            .Include(item => item.Customer)
            .Include(item => item.Lead)
            .Include(item => item.AssignedUser);
        IQueryable<FollowUp> followUps = _context.FollowUps
            .AsNoTracking()
            .Include(item => item.Customer)
            .Include(item => item.Lead)
            .Include(item => item.AssignedUser);

        if (!isBroadScope)
        {
            customers = customers.Where(item => item.CreatedBy == customerOwner);
            leads = leads.Where(item => item.AssignedTo == currentUser.Id);
            opportunities = opportunities.Where(item => item.AssignedTo == currentUser.Id);
            followUps = followUps.Where(item => item.AssignedTo == currentUser.Id);
        }

        var model = new ReportsViewModel
        {
            ReportType = reportType,
            FromDate = fromDate,
            ToDate = toDate,
            Status = status,
            Stage = stage,
            Source = source,
            AssignedTo = assignedTo,
            SearchText = searchText,
            CanChooseAssignedUser = isBroadScope
        };

        model = await PopulateFilterOptionsAsync(model, customers, leads, isBroadScope, currentUser);

        if (HasDateBindingError() || (fromDate.HasValue && toDate.HasValue && fromDate.Value > toDate.Value))
        {
            model.DateFilterError = HasDateBindingError()
                ? "Enter valid From and To dates."
                : "From Date cannot be after To Date.";
            return View(model);
        }

        var pattern = string.IsNullOrWhiteSpace(searchText) ? null : $"%{searchText.Trim()}%";
        var fromUtc = fromDate?.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var toUtcExclusive = toDate?.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        switch (reportType)
        {
            case "customers":
            {
                var query = customers;
                if (fromUtc.HasValue)
                {
                    query = query.Where(item => item.CreatedDate >= fromUtc.Value);
                }
                if (toUtcExclusive.HasValue)
                {
                    query = query.Where(item => item.CreatedDate < toUtcExclusive.Value);
                }
                if (!string.IsNullOrWhiteSpace(status))
                {
                    query = query.Where(item => item.Status == status);
                }
                if (isBroadScope && !string.IsNullOrWhiteSpace(assignedTo))
                {
                    query = query.Where(item => item.CreatedBy == assignedTo);
                }
                if (pattern is not null)
                {
                    query = query.Where(item =>
                        EF.Functions.ILike(item.CustomerName, pattern) ||
                        EF.Functions.ILike(item.Email, pattern) ||
                        EF.Functions.ILike(item.Phone, pattern) ||
                        (item.CompanyName != null && EF.Functions.ILike(item.CompanyName, pattern)) ||
                        EF.Functions.ILike(item.CustomerCode, pattern));
                }

                var statusCounts = await query
                    .GroupBy(item => item.Status)
                    .Select(group => new ReportStatusCount { Status = group.Key, Count = group.Count() })
                    .ToListAsync();
                model.CustomerSummary = new CustomerReportSummary
                {
                    Total = statusCounts.Sum(item => item.Count),
                    Active = statusCounts.Where(item => item.Status == "Active").Sum(item => item.Count),
                    OtherStatuses = statusCounts.Where(item => item.Status != "Active").ToList()
                };
                model.Customers = await query
                    .OrderByDescending(item => item.CreatedDate)
                    .Select(item => new CustomerReportRow
                    {
                        CustomerCode = item.CustomerCode,
                        CustomerName = item.CustomerName,
                        Email = item.Email,
                        Phone = item.Phone,
                        CompanyName = item.CompanyName,
                        City = item.City,
                        State = item.State,
                        Status = item.Status,
                        CreatedDate = item.CreatedDate,
                        CreatedBy = item.CreatedBy
                    })
                    .ToListAsync();
                break;
            }
            case "leads":
            {
                var query = leads;
                if (fromUtc.HasValue)
                {
                    query = query.Where(item => item.CreatedDate >= fromUtc.Value);
                }
                if (toUtcExclusive.HasValue)
                {
                    query = query.Where(item => item.CreatedDate < toUtcExclusive.Value);
                }
                if (!string.IsNullOrWhiteSpace(status) && Enum.GetNames<LeadStatus>().Contains(status))
                {
                    query = query.Where(item => item.Status == status);
                }
                if (!string.IsNullOrWhiteSpace(source))
                {
                    query = query.Where(item => item.Source == source);
                }
                if (isBroadScope && !string.IsNullOrWhiteSpace(assignedTo))
                {
                    query = query.Where(item => item.AssignedTo == assignedTo);
                }
                if (pattern is not null)
                {
                    query = query.Where(item =>
                        EF.Functions.ILike(item.LeadCode, pattern) ||
                        EF.Functions.ILike(item.LeadName, pattern) ||
                        EF.Functions.ILike(item.Email, pattern) ||
                        EF.Functions.ILike(item.Phone, pattern) ||
                        (item.CompanyName != null && EF.Functions.ILike(item.CompanyName, pattern)) ||
                        EF.Functions.ILike(item.Source, pattern));
                }

                var statusCounts = await query
                    .GroupBy(item => item.Status)
                    .Select(group => new { group.Key, Count = group.Count() })
                    .ToListAsync();
                var counts = statusCounts.ToDictionary(item => item.Key, item => item.Count);
                model.LeadSummary = new LeadReportSummary
                {
                    Total = statusCounts.Sum(item => item.Count),
                    New = counts.GetValueOrDefault(nameof(LeadStatus.New)),
                    Contacted = counts.GetValueOrDefault(nameof(LeadStatus.Contacted)),
                    Qualified = counts.GetValueOrDefault(nameof(LeadStatus.Qualified)),
                    Converted = counts.GetValueOrDefault(nameof(LeadStatus.Converted)),
                    Lost = counts.GetValueOrDefault(nameof(LeadStatus.Lost)),
                    Unqualified = counts.GetValueOrDefault(nameof(LeadStatus.Unqualified)),
                    TotalExpectedValue = await query.SumAsync(item => (decimal?)item.ExpectedValue) ?? 0m
                };
                model.Leads = await query
                    .OrderByDescending(item => item.CreatedDate)
                    .Select(item => new LeadReportRow
                    {
                        LeadCode = item.LeadCode,
                        LeadName = item.LeadName,
                        CompanyName = item.CompanyName,
                        Source = item.Source,
                        Status = item.Status,
                        ExpectedValue = item.ExpectedValue,
                        AssignedTo = item.AssignedUser == null
                            ? item.AssignedTo ?? "-"
                            : (item.AssignedUser.FullName ?? item.AssignedUser.Email ?? item.AssignedTo ?? "-"),
                        CreatedDate = item.CreatedDate
                    })
                    .ToListAsync();
                break;
            }
            case "opportunities":
            {
                var query = opportunities;
                if (fromDate.HasValue)
                {
                    query = query.Where(item => item.ExpectedCloseDate >= fromDate);
                }
                if (toDate.HasValue)
                {
                    query = query.Where(item => item.ExpectedCloseDate <= toDate);
                }
                if (!string.IsNullOrWhiteSpace(status) && new[] { "Open", "Won", "Lost" }.Contains(status))
                {
                    query = query.Where(item => item.Status == status);
                }
                if (!string.IsNullOrWhiteSpace(stage) && Enum.GetNames<OpportunityStage>().Contains(stage))
                {
                    query = query.Where(item => item.Stage == stage);
                }
                if (isBroadScope && !string.IsNullOrWhiteSpace(assignedTo))
                {
                    query = query.Where(item => item.AssignedTo == assignedTo);
                }
                if (pattern is not null)
                {
                    query = query.Where(item =>
                        EF.Functions.ILike(item.OpportunityName, pattern) ||
                        (item.Customer != null && EF.Functions.ILike(item.Customer.CustomerName, pattern)) ||
                        (item.Lead != null && EF.Functions.ILike(item.Lead.LeadName, pattern)));
                }

                var statusRows = await query
                    .GroupBy(item => item.Status)
                    .Select(group => new
                    {
                        Status = group.Key,
                        Count = group.Count(),
                        Amount = group.Sum(item => item.Amount),
                        Weighted = group.Sum(item => item.Amount * item.Probability / 100m)
                    })
                    .ToListAsync();
                var statusLookup = statusRows.ToDictionary(item => item.Status, item => item);
                var openRow = statusLookup.GetValueOrDefault(nameof(OpportunityStatus.Open));
                var wonRow = statusLookup.GetValueOrDefault(nameof(OpportunityStatus.Won));
                var lostRow = statusLookup.GetValueOrDefault(nameof(OpportunityStatus.Lost));
                model.OpportunitySummary = new OpportunityReportSummary
                {
                    Total = statusRows.Sum(item => item.Count),
                    Open = openRow?.Count ?? 0,
                    Won = wonRow?.Count ?? 0,
                    Lost = lostRow?.Count ?? 0,
                    OpenPipelineValue = openRow?.Amount ?? 0m,
                    WeightedPipelineValue = openRow?.Weighted ?? 0m,
                    WonValue = wonRow?.Amount ?? 0m,
                    LostValue = lostRow?.Amount ?? 0m
                };
                model.Opportunities = await query
                    .OrderBy(item => item.ExpectedCloseDate)
                    .Select(item => new OpportunityReportRow
                    {
                        OpportunityName = item.OpportunityName,
                        CustomerName = item.Customer == null ? string.Empty : item.Customer.CustomerName,
                        LeadName = item.Lead == null ? null : item.Lead.LeadCode + " - " + item.Lead.LeadName,
                        Amount = item.Amount,
                        Stage = item.Stage,
                        Probability = item.Probability,
                        WeightedValue = item.Amount * item.Probability / 100m,
                        Status = item.Status,
                        ExpectedCloseDate = item.ExpectedCloseDate,
                        AssignedTo = item.AssignedUser == null
                            ? item.AssignedTo
                            : (item.AssignedUser.FullName ?? item.AssignedUser.Email ?? item.AssignedTo)
                    })
                    .ToListAsync();
                break;
            }
            case "followups":
            {
                var query = followUps;
                if (fromDate.HasValue)
                {
                    query = query.Where(item => item.FollowUpDate >= fromDate);
                }
                if (toDate.HasValue)
                {
                    query = query.Where(item => item.FollowUpDate <= toDate);
                }
                if (!string.IsNullOrWhiteSpace(status) && Enum.GetNames<FollowUpStatus>().Contains(status))
                {
                    query = query.Where(item => item.Status == status);
                }
                if (isBroadScope && !string.IsNullOrWhiteSpace(assignedTo))
                {
                    query = query.Where(item => item.AssignedTo == assignedTo);
                }
                if (pattern is not null)
                {
                    query = query.Where(item =>
                        EF.Functions.ILike(item.FollowUpType, pattern) ||
                        (item.Remarks != null && EF.Functions.ILike(item.Remarks, pattern)) ||
                        (item.Customer != null && EF.Functions.ILike(item.Customer.CustomerName, pattern)) ||
                        (item.Lead != null && EF.Functions.ILike(item.Lead.LeadName, pattern)));
                }

                var statusRows = await query
                    .GroupBy(item => item.Status)
                    .Select(group => new { Status = group.Key, Count = group.Count() })
                    .ToListAsync();
                var counts = statusRows.ToDictionary(item => item.Status, item => item.Count);
                var today = DateOnly.FromDateTime(DateTime.UtcNow);
                model.FollowUpSummary = new FollowUpReportSummary
                {
                    Planned = counts.GetValueOrDefault(nameof(FollowUpStatus.Planned)),
                    Completed = counts.GetValueOrDefault(nameof(FollowUpStatus.Completed)),
                    Missed = counts.GetValueOrDefault(nameof(FollowUpStatus.Missed)),
                    Cancelled = counts.GetValueOrDefault(nameof(FollowUpStatus.Cancelled)),
                    Upcoming = await query.CountAsync(item =>
                        item.Status == nameof(FollowUpStatus.Planned) && item.FollowUpDate >= today),
                    OverdueOrMissed = await query.CountAsync(item =>
                        item.Status == nameof(FollowUpStatus.Missed) ||
                        (item.Status == nameof(FollowUpStatus.Planned) && item.FollowUpDate < today))
                };
                model.FollowUps = await query
                    .OrderBy(item => item.FollowUpDate)
                    .Select(item => new FollowUpReportRow
                    {
                        FollowUpDate = item.FollowUpDate,
                        FollowUpType = item.FollowUpType,
                        CustomerName = item.Customer == null ? null : item.Customer.CustomerName,
                        LeadName = item.Lead == null ? null : item.Lead.LeadCode + " - " + item.Lead.LeadName,
                        Remarks = item.Remarks,
                        Status = item.Status,
                        AssignedTo = item.AssignedUser == null
                            ? item.AssignedTo
                            : (item.AssignedUser.FullName ?? item.AssignedUser.Email ?? item.AssignedTo)
                    })
                    .ToListAsync();
                break;
            }
        }

        return View(model);
    }

    private async Task<ReportsViewModel> PopulateFilterOptionsAsync(
        ReportsViewModel model,
        IQueryable<Customer> customers,
        IQueryable<Lead> leads,
        bool isBroadScope,
        ApplicationUser currentUser)
    {
        var customerStatuses = await customers.Select(item => item.Status).Distinct().OrderBy(item => item).ToListAsync();
        var leadSources = await leads.Select(item => item.Source).Distinct().OrderBy(item => item).ToListAsync();

        var statuses = model.ReportType switch
        {
            "customers" => customerStatuses,
            "leads" => Enum.GetNames<LeadStatus>().ToList(),
            "opportunities" => new List<string> { "Open", "Won", "Lost" },
            _ => Enum.GetNames<FollowUpStatus>().ToList()
        };

        var userOptions = isBroadScope
            ? await _userManager.Users.AsNoTracking()
                .OrderBy(user => user.FullName)
                .Select(user => new ReportFilterOption(
                    model.ReportType == "customers" ? user.UserName ?? user.Id : user.Id,
                    string.IsNullOrWhiteSpace(user.FullName) ? user.Email ?? user.UserName ?? user.Id : user.FullName))
                .ToListAsync()
            : new List<ReportFilterOption>
            {
                new(
                    model.ReportType == "customers"
                        ? User.Identity?.Name ?? currentUser.UserName ?? currentUser.Id
                        : currentUser.Id,
                    string.IsNullOrWhiteSpace(currentUser.FullName)
                        ? currentUser.Email ?? currentUser.UserName ?? currentUser.Id
                        : currentUser.FullName)
            };

        return new ReportsViewModel
        {
            ReportType = model.ReportType,
            FromDate = model.FromDate,
            ToDate = model.ToDate,
            Status = model.Status,
            Stage = model.Stage,
            Source = model.Source,
            AssignedTo = model.AssignedTo,
            SearchText = model.SearchText,
            CanChooseAssignedUser = isBroadScope,
            StatusOptions = statuses.Select(item => new ReportFilterOption(item, item)).ToList(),
            StageOptions = Enum.GetNames<OpportunityStage>().Select(item => new ReportFilterOption(item, item)).ToList(),
            SourceOptions = leadSources.Select(item => new ReportFilterOption(item, item)).ToList(),
            AssignedUserOptions = userOptions
        };
    }

    private bool HasDateBindingError()
    {
        return (ModelState.TryGetValue("fromDate", out var fromState) && fromState.Errors.Count > 0) ||
            (ModelState.TryGetValue("toDate", out var toState) && toState.Errors.Count > 0);
    }

    private static string NormalizeReportType(string? reportType)
    {
        return reportType?.Trim().ToLowerInvariant() switch
        {
            "customers" => "customers",
            "opportunities" => "opportunities",
            "followups" => "followups",
            _ => "leads"
        };
    }
}