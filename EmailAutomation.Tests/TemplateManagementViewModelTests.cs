using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using EmailAutomation.Application.Services;
using EmailAutomation.Domain.Models;
using EmailAutomation.Infrastructure.Templates;
using EmailAutomation.UI.Services;
using EmailAutomation.UI.ViewModels;
using Moq;
using Xunit;

namespace EmailAutomation.Tests;

public class TemplateManagementViewModelTests
{
    private static Mock<IRepository> RepositoryWithNoTemplates()
    {
        var repo = new Mock<IRepository>();
        repo.Setup(r => r.GetTemplatesAsync()).ReturnsAsync(new List<EmailTemplate>());
        return repo;
    }

    [Fact]
    public async Task LoadHtmlFileCommand_ReadsChosenFile_IntoBodyTemplate()
    {
        var repo = RepositoryWithNoTemplates();
        var filePicker = new Mock<IFilePickerService>();
        var tempFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.html");
        var htmlContent = "<table><tr><td>Row 1</td></tr></table>";
        await File.WriteAllTextAsync(tempFile, htmlContent);
        filePicker.Setup(f => f.PickOpenHtmlFileAsync(It.IsAny<string>())).ReturnsAsync(tempFile);

        try
        {
            var vm = new TemplateManagementViewModel(repo.Object, filePicker.Object, new ScribanTemplateEngine())
            {
                SelectedTemplate = new EmailTemplate { Id = Guid.NewGuid(), Name = "T1" }
            };

            await vm.LoadHtmlFileAsync();

            Assert.Equal(htmlContent, vm.SelectedTemplate.BodyTemplate);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task LoadHtmlFileCommand_LeavesBodyUnchanged_WhenFilePickerIsCancelled()
    {
        var repo = RepositoryWithNoTemplates();
        var filePicker = new Mock<IFilePickerService>();
        filePicker.Setup(f => f.PickOpenHtmlFileAsync(It.IsAny<string>())).ReturnsAsync((string?)null);

        var vm = new TemplateManagementViewModel(repo.Object, filePicker.Object, new ScribanTemplateEngine())
        {
            SelectedTemplate = new EmailTemplate { Id = Guid.NewGuid(), Name = "T1", BodyTemplate = "original" }
        };

        await vm.LoadHtmlFileAsync();

        Assert.Equal("original", vm.SelectedTemplate.BodyTemplate);
    }

    [Fact]
    public async Task LoadHtmlFileCommand_DoesNothing_WhenNoTemplateSelected()
    {
        var repo = RepositoryWithNoTemplates();
        var filePicker = new Mock<IFilePickerService>();

        var vm = new TemplateManagementViewModel(repo.Object, filePicker.Object, new ScribanTemplateEngine());

        await vm.LoadHtmlFileAsync();

        filePicker.Verify(f => f.PickOpenHtmlFileAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void RenderPreviewHtml_PreservesTableMarkup_AndFillsPlaceholders()
    {
        var repo = RepositoryWithNoTemplates();
        var filePicker = new Mock<IFilePickerService>();

        var vm = new TemplateManagementViewModel(repo.Object, filePicker.Object, new ScribanTemplateEngine())
        {
            SelectedTemplate = new EmailTemplate
            {
                Id = Guid.NewGuid(),
                Name = "T1",
                BodyTemplate = "<table><tr><td>{{name}}</td></tr></table>"
            }
        };

        var html = vm.RenderPreviewHtml();

        // Placeholders render blank (no sample data supplied to the preview) but the surrounding
        // HTML/table structure - the thing users actually care about seeing - must survive intact.
        Assert.Equal("<table><tr><td></td></tr></table>", html);
    }

    [Fact]
    public void RenderPreviewHtml_ReturnsNull_WhenNoTemplateSelected()
    {
        var repo = RepositoryWithNoTemplates();
        var filePicker = new Mock<IFilePickerService>();

        var vm = new TemplateManagementViewModel(repo.Object, filePicker.Object, new ScribanTemplateEngine());

        Assert.Null(vm.RenderPreviewHtml());
    }
}
