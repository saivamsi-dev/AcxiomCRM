using System.Security.Claims;
using System.Text.RegularExpressions;
using AcxiomCRM.Data;
using AcxiomCRM.Models;
using Microsoft.AspNetCore.Http;

namespace AcxiomCRM.Services;

public sealed class AuditService
{
    private const int MaxValueLength = 16000;
    private static readonly Regex SensitiveDataPattern = new(
        "(?:password(?:hash)?|pwd|access[_-]?token|refresh[_-]?token|token|cookie|secret|connection[_-]?string|authorization|security[_-]?stamp)\\s*[\\\"']?\\s*[:=]",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly ApplicationDbContext _context;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AuditService(
        ApplicationDbContext context,
        IHttpContextAccessor httpContextAccessor)
    {
        _context = context;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task LogAsync(
        string action,
        string entityName,
        string? recordId = null,
        string? oldValue = null,
        string? newValue = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(action);
        ArgumentException.ThrowIfNullOrWhiteSpace(entityName);

        action = action.Trim();
        entityName = entityName.Trim();
        EnsureNoSensitiveData(action, nameof(action));
        EnsureNoSensitiveData(entityName, nameof(entityName));
        recordId = ValidateSafeValue(recordId, nameof(recordId));
        oldValue = ValidateSafeValue(oldValue, nameof(oldValue));
        newValue = ValidateSafeValue(newValue, nameof(newValue));

        if (action.Length > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(action), "Action cannot exceed 100 characters.");
        }

        if (entityName.Length > 200)
        {
            throw new ArgumentOutOfRangeException(nameof(entityName), "Entity name cannot exceed 200 characters.");
        }

        if (recordId?.Length > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(recordId), "Record ID cannot exceed 100 characters.");
        }

        var httpContext = _httpContextAccessor.HttpContext;
        var auditLog = new AuditLog
        {
            UserId = httpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier),
            Action = action,
            EntityName = entityName,
            RecordId = recordId,
            OldValue = oldValue,
            NewValue = newValue,
            CreatedDate = DateTime.UtcNow,
            IpAddress = httpContext?.Connection.RemoteIpAddress?.ToString()
        };

        _context.AuditLogs.Add(auditLog);
        await _context.SaveChangesAsync(cancellationToken);
    }

    private static string? ValidateSafeValue(string? value, string parameterName)
    {
        if (value is null)
        {
            return null;
        }

        if (value.Length > MaxValueLength)
        {
            throw new ArgumentOutOfRangeException(parameterName, $"Audit values cannot exceed {MaxValueLength} characters.");
        }

        EnsureNoSensitiveData(value, parameterName);

        return value;
    }

    private static void EnsureNoSensitiveData(string value, string parameterName)
    {
        if (SensitiveDataPattern.IsMatch(value))
        {
            throw new ArgumentException(
                "Audit values must not contain passwords, tokens, cookies, secrets, connection strings, or security stamps.",
                parameterName);
        }
    }
}