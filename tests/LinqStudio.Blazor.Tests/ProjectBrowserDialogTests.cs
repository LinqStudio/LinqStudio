using Bunit;
using LinqStudio.Blazor.Components.Dialogs;
using LinqStudio.Blazor.Extensions;
using LinqStudio.Blazor.Models;
using LinqStudio.Blazor.Tests.Fakes;
using LinqStudio.Core.Extensions;
using LinqStudio.Core.Models;
using LinqStudio.Core.Repositories;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using Xunit;

namespace LinqStudio.Blazor.Tests;

/// <summary>
/// Tests for the delete-confirmation flow in <see cref="ProjectBrowserDialog"/>.
/// Dialogs in MudBlazor are hosted by <see cref="MudDialogProvider"/>; this test renders
/// that provider and drives interactions through the real <see cref="IDialogService"/>
/// so the full rendering pipeline is exercised.
/// </summary>
public class ProjectBrowserDialogTests : BunitContext, IAsyncLifetime
{
private readonly InMemoryProjectRepository _repository = new();

public ProjectBrowserDialogTests()
{
Services
.AddLinqStudio()
.AddLinqStudioBlazor();

// Provide the in-memory fake for project persistence.
Services.AddSingleton<IProjectRepository>(_repository);
Services.AddLogging();

// Loosen JS interop so MudBlazor components work in headless mode.
JSInterop.Mode = JSRuntimeMode.Loose;
}

/// <summary>Adds a project to the in-memory repository and returns its summary.</summary>
private async Task<ProjectSummary> AddProjectAsync(string name = "TestProject")
{
var project = new Project { Name = name };
var id = await _repository.SaveProjectAsync(project);
return (await _repository.ListProjectsAsync()).First(p => p.Id == id);
}

/// <summary>
/// Opens <see cref="ProjectBrowserDialog"/> via the real <see cref="IDialogService"/> and
/// returns the rendered <see cref="MudDialogProvider"/> which hosts all dialogs.
/// </summary>
private async Task<IRenderedComponent<MudDialogProvider>> OpenProjectBrowserDialogAsync(
ProjectBrowserMode mode = ProjectBrowserMode.Open)
{
var provider = Render<MudDialogProvider>();
var dialogService = Services.GetRequiredService<IDialogService>();

var parameters = new DialogParameters<ProjectBrowserDialog>
{
{ x => x.Mode, mode }
};

await provider.InvokeAsync(async () =>
await dialogService.ShowAsync<ProjectBrowserDialog>("Open Project", parameters));

return provider;
}

[Fact]
public async Task DeleteProject_WhenUserConfirms_RemovesProjectFromList()
{
// Arrange
await AddProjectAsync("ProjectToDelete");
var provider = await OpenProjectBrowserDialogAsync();

// Wait for the project browser dialog to load the project list.
provider.WaitForAssertion(
() => Assert.NotEmpty(provider.FindAll("[data-testid='project-delete-btn']")));

// Act — click the delete button to trigger the confirmation dialog.
await provider.InvokeAsync(() => provider.Find("[data-testid='project-delete-btn']").Click());

// Wait for the confirmation dialog (UnsavedChangesDialog) to appear.
provider.WaitForAssertion(
() => Assert.NotEmpty(provider.FindAll("[data-testid='unsaved-changes-dialog']")));

// Verify the confirmation shows the correct title.
Assert.Contains("Delete project?", provider.Markup, StringComparison.OrdinalIgnoreCase);

// Verify the project name is displayed in the confirmation message.
Assert.Contains("ProjectToDelete", provider.Markup, StringComparison.Ordinal);

// Confirm deletion.
await provider.InvokeAsync(() => provider.Find("[data-testid='unsaved-changes-confirm-btn']").Click());

// Assert — project was removed from the repository.
var remaining = await _repository.ListProjectsAsync();
Assert.Empty(remaining);

// Assert — the project list in the dialog is now empty.
provider.WaitForAssertion(
() => Assert.Empty(provider.FindAll("[data-testid='project-list-item']")));
}

[Fact]
public async Task DeleteProject_WhenUserCancels_KeepsProjectInList()
{
// Arrange
await AddProjectAsync("ProjectToKeep");
var provider = await OpenProjectBrowserDialogAsync();

provider.WaitForAssertion(
() => Assert.NotEmpty(provider.FindAll("[data-testid='project-delete-btn']")));

// Act — click delete, then cancel the confirmation.
await provider.InvokeAsync(() => provider.Find("[data-testid='project-delete-btn']").Click());

// Wait for the confirmation dialog to appear.
provider.WaitForAssertion(
() => Assert.NotEmpty(provider.FindAll("[data-testid='unsaved-changes-dialog']")));

// Cancel deletion.
await provider.InvokeAsync(() => provider.Find("[data-testid='unsaved-changes-cancel-btn']").Click());

// Assert — project was NOT deleted from the repository.
var remaining = await _repository.ListProjectsAsync();
Assert.Single(remaining);
Assert.Equal("ProjectToKeep", remaining[0].Name);

// Assert — the project list still shows the project item.
Assert.NotEmpty(provider.FindAll("[data-testid='project-list-item']"));
}

[Fact]
public async Task DeleteProject_ConfirmationDialog_ShowsProjectNameInMessage()
{
// Arrange
const string projectName = "MySpecialProject";
await AddProjectAsync(projectName);
var provider = await OpenProjectBrowserDialogAsync();

provider.WaitForAssertion(
() => Assert.NotEmpty(provider.FindAll("[data-testid='project-delete-btn']")));

// Act — click delete to open the confirmation dialog.
await provider.InvokeAsync(() => provider.Find("[data-testid='project-delete-btn']").Click());

// Assert — wait for and verify the confirmation dialog content.
provider.WaitForAssertion(
() => Assert.NotEmpty(provider.FindAll("[data-testid='unsaved-changes-dialog']")));

// The dialog message must contain the project name.
var message = provider.Find("[data-testid='unsaved-changes-message']").TextContent;
Assert.Contains(projectName, message, StringComparison.Ordinal);

// The dialog title must say "Delete project?".
Assert.Contains("Delete project?", provider.Markup, StringComparison.OrdinalIgnoreCase);
	}

	// IAsyncLifetime allows xUnit to dispose the context asynchronously, which is required
	// because MudBlazor 8.x registers services (e.g. PointerEventsNoneService) that only
	// implement IAsyncDisposable. Without this, xUnit's synchronous Dispose() path throws.
	public Task InitializeAsync() => Task.CompletedTask;

	public new async Task DisposeAsync()
	{
		await base.DisposeAsync();
	}
}
