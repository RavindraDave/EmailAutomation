using System.Collections.Generic;
using System.Linq;

namespace EmailAutomation.Application.Services;

public enum ValidationSeverity
{
    Warning,
    Error,
}

public class ValidationIssue
{
    public int RowNumber { get; set; }
    public string Message { get; set; } = string.Empty;
    public ValidationSeverity Severity { get; set; } = ValidationSeverity.Error;
}

/// <summary>Fully rendered preview of one row, shown to the user before anything is sent.</summary>
public class RowPreview
{
    public int RowNumber { get; set; }
    public string To { get; set; } = string.Empty;
    public string RenderedSubject { get; set; } = string.Empty;
    public string RenderedBody { get; set; } = string.Empty;
}

public class ValidationReport
{
    public int TotalRows { get; set; }
    public int ValidRows { get; set; }
    public List<ValidationIssue> Issues { get; } = new();
    public List<RowPreview> Previews { get; } = new();
    public List<string> UnmatchedPlaceholders { get; } = new();

    public bool HasBlockingErrors => Issues.Any(i => i.Severity == ValidationSeverity.Error);
}
