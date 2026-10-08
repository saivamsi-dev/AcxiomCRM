using AcxiomCRM.Data;
using AcxiomCRM.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Controllers;

[Authorize(Policy = "AdminOnly")]
public class AuditController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<AuditController> _logger;

    public AuditController(ApplicationDbContext context, ILogger<AuditController> logger)
    {
        _context = context;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        string? userId,
        string? action,
        string? entityName,
        DateOnly? fromDate,
        DateOnly? toDate)
    {
        var model = new AuditLogViewModel
        {
            UserId = userId,
            Action = action,
            EntityName = entityName,
            FromDate = fromDate,
            ToDate = toDate,
            Users = await _context.Users
                .AsNoTracking()
                .OrderBy(user => user.FullName)
                .Select(user => new AuditFilterOption(
                    user.Id,
                    string.IsNullOrWhiteSpace(user.FullName) ? user.Email ?? user.Id : user.FullName))
                .ToListAsync(),
            Actions = await _context.AuditLogs.AsNoTracking()
                .Select(log => log.Action)
                .Distinct()
                .OrderBy(value => value)
                .ToListAsync(),
            Entities = await _context.AuditLogs.AsNoTracking()
                .Select(log => log.EntityName)
                .Distinct()
                .OrderBy(value => value)
                .ToListAsync()
        };

        var invalidDate =
            (ModelState.TryGetValue(nameof(fromDate), out var fromState) && fromState.Errors.Count > 0) ||
            (ModelState.TryGetValue(nameof(toDate), out var toState) && toState.Errors.Count > 0);
        if (invalidDate || (fromDate.HasValue && toDate.HasValue && fromDate.Value > toDate.Value))
        {
            model.FilterError = invalidDate
                ? "Enter valid From and To dates."
                : "From Date cannot be after To Date.";
            return View(model);
        }

        IQueryable<Models.AuditLog> query = _context.AuditLogs
            .AsNoTracking()
            .Include(log => log.User);

        if (!string.IsNullOrWhiteSpace(userId))
        {
            query = query.Where(log => log.UserId == userId);
        }
        if (!string.IsNullOrWhiteSpace(action))
        {
            query = query.Where(log => log.Action == action);
        }
        if (!string.IsNullOrWhiteSpace(entityName))
        {
            query = query.Where(log => log.EntityName == entityName);
        }
        if (fromDate.HasValue)
        {
            var startUtc = fromDate.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            query = query.Where(log => log.CreatedDate >= startUtc);
        }
        if (toDate.HasValue)
        {
            var endUtcExclusive = toDate.Value.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            query = query.Where(log => log.CreatedDate < endUtcExclusive);
        }

        try
        {
            model = new AuditLogViewModel
            {
                UserId = model.UserId,
                Action = model.Action,
                EntityName = model.EntityName,
                FromDate = model.FromDate,
                ToDate = model.ToDate,
                Users = model.Users,
                Actions = model.Actions,
                Entities = model.Entities,
                Entries = await query
                    .OrderByDescending(log => log.CreatedDate)
                    .Take(500)
                    .Select(log => new AuditLogRowViewModel
                    {
                        CreatedDate = log.CreatedDate,
                        User = log.User == null
                            ? (log.UserId == null ? "System" : "Deleted user")
                            : (log.User.FullName ?? log.User.Email ?? log.User.UserName ?? "User"),
                        Action = log.Action,
                        EntityName = log.EntityName,
                        RecordId = log.RecordId,
                        IpAddress = log.IpAddress
                    })
                    .ToListAsync()
            };
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to load audit log entries.");
            model.FilterError = "Audit entries could not be loaded. Try again later.";
        }

        return View(model);
    }
}