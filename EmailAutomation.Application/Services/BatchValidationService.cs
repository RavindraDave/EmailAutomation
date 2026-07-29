using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using EmailAutomation.Domain.Models;

namespace EmailAutomation.Application.Services;

/// <summary>
/// Dry-run validation: reads the Excel file and renders every row's subject/body against the
/// template, WITHOUT sending anything, so mistakes (bad addresses, missing attachments, template
/// placeholders that don't match any column) surface before a real batch goes out. Scriban
/// silently renders an unknown variable as an empty string rather than erroring, so a typo'd
/// placeholder would otherwise send blank fields to every recipient without any warning.
/// </summary>
public class BatchValidationService
{
    private static readonly Regex PlaceholderPattern = new(@"\{\{\s*([a-zA-Z_][a-zA-Z0-9_]*)\s*\}\}", RegexOptions.Compiled);

    private readonly IExcelReader _excelReader;
    private readonly ITemplateEngine _templateEngine;
    private readonly IEmailAddressValidator _addressValidator;

    public BatchValidationService(IExcelReader excelReader, ITemplateEngine templateEngine, IEmailAddressValidator addressValidator)
    {
        _excelReader = excelReader;
        _templateEngine = templateEngine;
        _addressValidator = addressValidator;
    }

    public ValidationReport Validate(string excelFilePath, EmailTemplate template, int previewRows = 5)
    {
        var report = new ValidationReport();

        List<EmailJob> jobs;
        try
        {
            jobs = _excelReader.ReadJobs(excelFilePath).ToList();
        }
        catch (Exception ex)
        {
            report.Issues.Add(new ValidationIssue { RowNumber = 0, Message = $"Could not read the Excel file: {ex.Message}", Severity = ValidationSeverity.Error });
            return report;
        }

        report.TotalRows = jobs.Count;

        if (jobs.Count == 0)
        {
            report.Issues.Add(new ValidationIssue { RowNumber = 0, Message = "The Excel file has no enabled rows with a recipient address.", Severity = ValidationSeverity.Error });
            return report;
        }

        var referencedPlaceholders = ExtractPlaceholders(template.SubjectTemplate)
            .Concat(ExtractPlaceholders(template.BodyTemplate))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var knownVariableNames = jobs
            .SelectMany(j => j.Variables.Keys)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var placeholder in referencedPlaceholders.Where(p => !knownVariableNames.Contains(p)))
        {
            report.UnmatchedPlaceholders.Add(placeholder);
            report.Issues.Add(new ValidationIssue
            {
                RowNumber = 0,
                Message = $"Template uses {{{{{placeholder}}}}} but no Excel column named '{placeholder}' was found - it will send blank for every recipient.",
                Severity = ValidationSeverity.Error,
            });
        }

        var validCount = 0;
        foreach (var job in jobs)
        {
            var rowIsValid = true;

            if (!_addressValidator.IsValid(job.To))
            {
                report.Issues.Add(new ValidationIssue { RowNumber = job.RowNumber, Message = $"'{job.To}' is not a valid email address.", Severity = ValidationSeverity.Error });
                rowIsValid = false;
            }

            if (!string.IsNullOrWhiteSpace(job.Cc) && !_addressValidator.IsValid(job.Cc))
            {
                report.Issues.Add(new ValidationIssue { RowNumber = job.RowNumber, Message = $"Cc address '{job.Cc}' is not valid.", Severity = ValidationSeverity.Error });
                rowIsValid = false;
            }

            string renderedSubject;
            string renderedBody;
            try
            {
                var subjectToRender = job.Subject ?? template.SubjectTemplate;
                renderedSubject = _templateEngine.Render(subjectToRender, job.Variables);
                renderedBody = _templateEngine.Render(template.BodyTemplate, job.Variables);
            }
            catch (Exception ex)
            {
                report.Issues.Add(new ValidationIssue { RowNumber = job.RowNumber, Message = $"Template failed to render: {ex.Message}", Severity = ValidationSeverity.Error });
                rowIsValid = false;
                renderedSubject = string.Empty;
                renderedBody = string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(job.AttachmentPath))
            {
                var attachmentPath = job.AttachmentPath;
                try
                {
                    attachmentPath = _templateEngine.Render(job.AttachmentPath, job.Variables);
                }
                catch
                {
                    // Already reported as a template render error above.
                }

                if (!File.Exists(attachmentPath))
                {
                    report.Issues.Add(new ValidationIssue { RowNumber = job.RowNumber, Message = $"Attachment not found: '{attachmentPath}'.", Severity = ValidationSeverity.Error });
                    rowIsValid = false;
                }
            }

            if (rowIsValid)
            {
                validCount++;
            }

            if (report.Previews.Count < previewRows)
            {
                report.Previews.Add(new RowPreview
                {
                    RowNumber = job.RowNumber,
                    To = job.To,
                    RenderedSubject = renderedSubject,
                    RenderedBody = renderedBody,
                });
            }
        }

        report.ValidRows = validCount;
        return report;
    }

    private static IEnumerable<string> ExtractPlaceholders(string? template)
    {
        if (string.IsNullOrEmpty(template))
        {
            yield break;
        }

        // Scriban templates in this app are simple "bare variable" substitutions - a regex is
        // enough to catch the common case; loops/filters/conditionals are not parsed here.
        foreach (Match match in PlaceholderPattern.Matches(template))
        {
            yield return match.Groups[1].Value;
        }
    }
}
