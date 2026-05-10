using System.Collections.Generic;
using Scriban;
using Scriban.Runtime;
using EmailAutomation.Application.Services;

namespace EmailAutomation.Infrastructure.Templates;

public class ScribanTemplateEngine : ITemplateEngine
{
    public string Render(string template, Dictionary<string, string> variables)
    {
        if (string.IsNullOrEmpty(template)) return string.Empty;

        var compiledTemplate = Template.Parse(template);
        if (compiledTemplate.HasErrors)
        {
            throw new System.Exception("Template parsing error: " + string.Join(", ", compiledTemplate.Messages));
        }

        var scriptObject = new ScriptObject();
        foreach (var kvp in variables)
        {
            scriptObject.Add(kvp.Key, kvp.Value);
        }

        var context = new TemplateContext();
        context.PushGlobal(scriptObject);

        return compiledTemplate.Render(context);
    }
}
