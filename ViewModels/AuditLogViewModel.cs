namespace AcxiomCRM.ViewModels;

public sealed class AuditLogViewModel
{
    public string? UserId { get; init; }
    public string? Action { get; init; }
    public string? EntityName { get; init; }
    public DateOnly? FromDate { get; init; }
    public DateOnly? ToDate { get; init; }
    public string? FilterError { get; set; }
    public IReadOnlyList<AuditFilterOption> Users { get; init; } = [];
    public IReadOnlyList<string> Actions { get; init; } = [];
    public IReadOnlyList<string> Entities { get; init; } = [];
    public IReadOnlyList<AuditLogRowViewModel> Entries { get; init; } = [];
}

public sealed class AuditFilterOption
{
    public AuditFilterOption(string value, string text)
    {
        Value = value;
        Text = text;
    }

    public string Value { get; }
    public string Text { get; }
}

public sealed class AuditLogRowViewModel
{
    public DateTime CreatedDate { get; init; }
    public string User { get; init; } = "System";
    public string Action { get; init; } = string.Empty;
    public string EntityName { get; init; } = string.Empty;
    public string? RecordId { get; init; }
    public string? IpAddress { get; init; }
}