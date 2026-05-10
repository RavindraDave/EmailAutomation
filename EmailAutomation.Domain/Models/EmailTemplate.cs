using System;

namespace EmailAutomation.Domain.Models;

public class EmailTemplate
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string SubjectTemplate { get; set; } = string.Empty;
    public string BodyTemplate { get; set; } = string.Empty;
}
