using LinqStudio.App.WebServer.E2ETests.Fixtures;
using LinqStudio.App.WebServer.E2ETests.Helpers;
using LinqStudio.Blazor.Constants;
using LinqStudio.Core.Models;
using System.Text.Json;
using Xunit;
using static Microsoft.Playwright.Assertions;

namespace LinqStudio.App.WebServer.E2ETests;

[Collection("E2E")]
public class NavMenuE2ETests(AppServerFixture app, PlaywrightFixture pw)
{
	private readonly AppServerFixture _app = app;
	private readonly PlaywrightFixture _pw = pw;

	[Fact(Timeout = 60_000)]
	public async Task NavMenu_NewProject_CreatesUntitledProject()
	{
		Assert.NotNull(_pw.Browser);

		await using var context = await _pw.Browser.NewContextAsync();
		var page = await context.NewPageAsync();

		// Navigate to home page
		await page.GotoAsync(_app.BaseUrl.ToString());

		// Verify no project is open initially
		var projectGroup = page.GetByTestId("nav-project");
		await Expect(projectGroup).ToContainTextAsync("Project");

		// Open the Project menu and click "New" to create a project
		await page.GetByTestId("nav-project").ClickAsync();
		await Task.Delay(100); // Wait for menu to open
		await page.GetByTestId("nav-project-new").ClickAsync();

		// Verify we're redirected to home
		await page.WaitForURLAsync(_app.BaseUrl.ToString());

		// Verify snackbar appears with success message
		var snackbar = page.Locator(".mud-snackbar");
		await Expect(snackbar).ToBeVisibleAsync();
		await Expect(snackbar).ToContainTextAsync("New project created");

		// Verify project title shows "Untitled"
		await Expect(projectGroup).ToContainTextAsync("Untitled");

		// Verify project-specific menu items are now visible (need to open menu to see them)
		await page.GetByTestId("nav-project").ClickAsync();
		await Task.Delay(100); // Wait for menu to open
		await Expect(page.GetByTestId("nav-project-properties")).ToBeVisibleAsync();
		await Expect(page.GetByTestId("nav-project-save")).ToBeVisibleAsync();
		await Expect(page.GetByTestId("nav-project-save-as")).ToBeVisibleAsync();
		await Expect(page.GetByTestId("nav-project-close")).ToBeVisibleAsync();
	}

	[Fact(Timeout = 60_000)]
	public async Task NavMenu_NewProject_PromptsWhenUnsavedChanges()
	{
		Assert.NotNull(_pw.Browser);

		await using var context = await _pw.Browser.NewContextAsync();
		var page = await context.NewPageAsync();

		// Create a project and make some changes
		await E2ETestHelpers.CreateNewProjectAsync(page, _app);
		await E2ETestHelpers.CreateQueryAsync(page, _app, "context.People");

		// Navigate back to home
		await page.GetByTestId("nav-home").ClickAsync();

		// Verify project shows unsaved indicator
		var projectGroup = page.GetByTestId("nav-project");
		await Expect(projectGroup).ToContainTextAsync("Untitled *");

		// Try to create a new project (should show confirmation dialog) - need to open menu first
		await page.GetByTestId("nav-project").ClickAsync();
		await Task.Delay(100); // Wait for menu to open
		await page.GetByTestId("nav-project-new").ClickAsync();

		// Verify confirmation dialog appears
		var dialog = page.GetByTestId("unsaved-changes-dialog");
		await Expect(dialog).ToBeVisibleAsync();

		// Click Cancel
		var cancelBtn = page.GetByTestId("unsaved-changes-cancel-btn");
		await cancelBtn.ClickAsync();

		// Verify we're still on the same project
		await Expect(projectGroup).ToContainTextAsync("Untitled *");

		// Try again and confirm - need to open menu again
		await page.GetByTestId("nav-project").ClickAsync();
		await Task.Delay(100); // Wait for menu to open
		await page.GetByTestId("nav-project-new").ClickAsync();
		await Expect(dialog).ToBeVisibleAsync();

		var confirmBtn = page.GetByTestId("unsaved-changes-confirm-btn");
		await confirmBtn.ClickAsync();

		// Verify new project was created
		// New projects are also "Untitled" with unsaved changes (asterisk)
		await Expect(projectGroup).ToContainTextAsync("Untitled *");

		// Navigate to editor to verify it's a new project with no queries
		await page.GetByTestId("nav-editor").ClickAsync();
		await page.WaitForURLAsync($"{_app.BaseUrl}editor");

		// Verify "no queries" message is shown (proving it's a new project)
		var noQueryAlert = page.GetByTestId("no-query-alert");
		await Expect(noQueryAlert).ToBeVisibleAsync();
		await Expect(noQueryAlert).ToContainTextAsync("No queries are currently open");
	}

	[Fact(Timeout = 60_000)]
	public async Task NavMenu_CloseProject_ClosesProjectAndRedirectsToHome()
	{
		Assert.NotNull(_pw.Browser);

		await using var context = await _pw.Browser.NewContextAsync();
		var page = await context.NewPageAsync();

		// Create a project
		await E2ETestHelpers.CreateNewProjectAsync(page, _app);

		// Verify project is open
		var projectGroup = page.GetByTestId("nav-project");
		await Expect(projectGroup).ToContainTextAsync("Untitled");

		// Close the project (new projects have unsaved changes, so we need to handle the dialog)
		// Need to open menu first
		await page.GetByTestId("nav-project").ClickAsync();
		await Task.Delay(100); // Wait for menu to open
		await page.GetByTestId("nav-project-close").ClickAsync();

		// Verify confirmation dialog appears (new project is considered unsaved)
		var dialog = page.GetByTestId("unsaved-changes-dialog");
		await Expect(dialog).ToBeVisibleAsync();

		// Click Continue to close without saving
		var confirmBtn = page.GetByTestId("unsaved-changes-confirm-btn");
		await confirmBtn.ClickAsync();

		// Verify we're redirected to home
		await page.WaitForURLAsync(_app.BaseUrl.ToString());

		// Verify project-specific menu items are hidden (open menu to check)
		await page.GetByTestId("nav-project").ClickAsync();
		await Task.Delay(100); // Wait for menu to open
		await Expect(page.GetByTestId("nav-project-save")).Not.ToBeVisibleAsync();
		await Expect(page.GetByTestId("nav-project-close")).Not.ToBeVisibleAsync();
	}

	[Fact(Timeout = 60_000)]
	public async Task NavMenu_CloseProject_PromptsWhenUnsavedChanges()
	{
		Assert.NotNull(_pw.Browser);

		await using var context = await _pw.Browser.NewContextAsync();
		var page = await context.NewPageAsync();

		// Create a project and make some changes
		await E2ETestHelpers.CreateNewProjectAsync(page, _app);
		await E2ETestHelpers.CreateQueryAsync(page, _app, "context.People");

		// Navigate back to home
		await page.GetByTestId("nav-home").ClickAsync();

		// Verify project shows unsaved indicator
		var projectGroup = page.GetByTestId("nav-project");
		await Expect(projectGroup).ToContainTextAsync("Untitled *");

		// Try to close the project - need to open menu first
		await page.GetByTestId("nav-project").ClickAsync();
		await Task.Delay(100); // Wait for menu to open
		await page.GetByTestId("nav-project-close").ClickAsync();

		// Verify confirmation dialog appears with Continue/Cancel options
		var dialog = page.GetByTestId("unsaved-changes-dialog");
		await Expect(dialog).ToBeVisibleAsync();

		// Click "Cancel" to keep the project open
		var cancelBtn = page.GetByTestId("unsaved-changes-cancel-btn");
		await cancelBtn.ClickAsync();

		// Verify project is still open
		await Expect(projectGroup).ToContainTextAsync("Untitled *");

		// Try again and click "Continue" to close without saving - need to open menu again
		await page.GetByTestId("nav-project").ClickAsync();
		await Task.Delay(100); // Wait for menu to open
		await page.GetByTestId("nav-project-close").ClickAsync();
		await Expect(dialog).ToBeVisibleAsync();

		var confirmBtn = page.GetByTestId("unsaved-changes-confirm-btn");
		await confirmBtn.ClickAsync();

		// Verify project was closed
		await Expect(projectGroup).ToContainTextAsync("Project");
		await Expect(projectGroup).Not.ToContainTextAsync("Untitled");
	}

	[Fact(Timeout = 60_000)]
	public async Task NavMenu_QueriesSection_HiddenWhenNoProject()
	{
		Assert.NotNull(_pw.Browser);

		await using var context = await _pw.Browser.NewContextAsync();
		var page = await context.NewPageAsync();

		// Navigate to home page
		await page.GotoAsync(_app.BaseUrl.ToString());

		// Verify editor menu is not visible when no project
		var editorMenu = page.GetByTestId("nav-editor-menu");
		await Expect(editorMenu).Not.ToBeVisibleAsync();

		// Verify editor link is disabled
		var editorLink = page.GetByTestId("nav-editor-disabled");
		await Expect(editorLink).ToBeVisibleAsync();

		// Create a project - need to open menu first
		await page.GetByTestId("nav-project").ClickAsync();
		await Task.Delay(100); // Wait for menu to open
		await page.GetByTestId("nav-project-new").ClickAsync();

		// Verify editor menu is now visible
		await Expect(editorMenu).ToBeVisibleAsync();

		// Verify disabled editor link is no longer shown
		await Expect(editorLink).Not.ToBeVisibleAsync();
	}

	[Fact(Timeout = 60_000)]
	public async Task NavMenu_QueriesSection_ShowsEmptyMessage()
	{
		Assert.NotNull(_pw.Browser);

		await using var context = await _pw.Browser.NewContextAsync();
		var page = await context.NewPageAsync();

		// Create a project
		await E2ETestHelpers.CreateNewProjectAsync(page, _app);

		// Navigate to editor page
		await page.GetByTestId("nav-editor").ClickAsync();
		await page.WaitForURLAsync($"{_app.BaseUrl}editor");

		// Verify "no queries" message is shown
		var noQueryAlert = page.GetByTestId("no-query-alert");
		await Expect(noQueryAlert).ToBeVisibleAsync();
		await Expect(noQueryAlert).ToContainTextAsync("No queries are currently open");
	}

	[Fact(Timeout = 120_000)]
	public async Task NavMenu_SaveAs_SavesCompleteProjectToFile()
	{
		Assert.NotNull(_pw.Browser);

		await using var context = await _pw.Browser.NewContextAsync();
		var page = await context.NewPageAsync();

		// Create a project
		await E2ETestHelpers.CreateNewProjectAsync(page, _app);

		// --- Update connection string via Properties dialog ---
		// Need to open menu first
		await page.GetByTestId("nav-project").ClickAsync();
		await Task.Delay(100); // Wait for menu to open
		await page.GetByTestId("nav-project-properties").ClickAsync();

		var dialog = page.GetByTestId("edit-project-dialog");
		await Expect(dialog).ToBeVisibleAsync();

		var connectionStringField = page.GetByTestId("project-connection-string-field");
		await connectionStringField.FillAsync("Server=localhost;Database=TestDb;Integrated Security=true;");

		var saveBtn = page.GetByTestId("edit-project-save-btn");
		await saveBtn.ClickAsync();

		await Expect(dialog).Not.ToBeVisibleAsync();

		// Verify project shows unsaved indicator after properties update
		var projectGroup = page.GetByTestId("nav-project");
		await Expect(projectGroup).ToContainTextAsync("Untitled *");

		// --- Create first query with custom name and content ---
		await E2ETestHelpers.CreateQueryAsync(page, _app, "context.People.Where(x => x.Id > 10).OrderBy(x => x.Name)");

		// Verify unsaved indicator appears in editor (scope to active panel - single tab at this point)
		var activePanel = E2ETestHelpers.GetActivePanel(page);
		var unsavedIndicator = activePanel.GetByTestId("query-unsaved-indicator");
		await Expect(unsavedIndicator).ToBeVisibleAsync();

		_app.MockFileSystemService.SetNextSaveFileResult($"Get Filtered People{FileExtensions.Query.WithDot()}");
		await activePanel.GetByTestId("query-save-btn").ClickAsync();
		var snackbar = page.Locator(".mud-snackbar").Last;
		await Expect(snackbar).ToBeVisibleAsync();
		await Expect(snackbar).ToContainTextAsync("Query saved successfully");

		// Verify name matches saved filename
		var queryName = activePanel.GetByTestId("query-name-display");
		await Expect(queryName).ToContainTextAsync("Get Filtered People");

		// --- Create second query with different content ---
		await E2ETestHelpers.CreateQueryAsync(page, _app, "context.People.Select(x => new { x.Id, x.Name }).Take(100)", 1);

		// Verify unsaved indicator appears (scope to active panel — 2nd tab is now active)
		activePanel = E2ETestHelpers.GetActivePanel(page);
		unsavedIndicator = activePanel.GetByTestId("query-unsaved-indicator");
		await Expect(unsavedIndicator).ToBeVisibleAsync();

		_app.MockFileSystemService.SetNextSaveFileResult($"Get People Summary{FileExtensions.Query.WithDot()}");
		await activePanel.GetByTestId("query-save-btn").ClickAsync();
		snackbar = page.Locator(".mud-snackbar").Last;
		await Expect(snackbar).ToBeVisibleAsync();
		await Expect(snackbar).ToContainTextAsync("Query saved successfully");

		// Verify rename succeeded
		queryName = activePanel.GetByTestId("query-name-display");
		await Expect(queryName).ToContainTextAsync("Get People Summary");

		// Navigate back to home
		await page.GetByTestId("nav-home").ClickAsync();

		// Verify project still shows unsaved indicator
		await Expect(projectGroup).ToContainTextAsync("Untitled *");

		// --- Save the project ---
		_app.MockFileSystemService.SetNextSaveFileResult($"TestProject{FileExtensions.Project.WithDot()}");

		// Need to open menu first
		await page.GetByTestId("nav-project").ClickAsync();
		await Task.Delay(100); // Wait for menu to open
		await page.GetByTestId("nav-project-save-as").ClickAsync();

		// Verify snackbar shows success message
		snackbar = page.Locator(".mud-snackbar").Last;
		await Expect(snackbar).ToBeVisibleAsync();
		await Expect(snackbar).ToContainTextAsync("Project saved successfully");

		// Verify the file was created
		Assert.True(_app.MockFileSystemService.TestFileExists($"TestProject{FileExtensions.Project.WithDot()}"));

		// --- Verify the saved file contains all expected content ---
		var fileContent = _app.MockFileSystemService.ReadTestFile($"TestProject{FileExtensions.Project.WithDot()}");
		var project = JsonSerializer.Deserialize<Project>(fileContent);

		Assert.NotNull(project);

		// Verify connection string was saved
		Assert.Equal("Server=localhost;Database=TestDb;Integrated Security=true;", project.ConnectionString);

		// Verify unsaved indicator is cleared after save
		await Expect(projectGroup).Not.ToContainTextAsync("*");

		// Verify Save button is disabled - need to open menu to check
		await page.GetByTestId("nav-project").ClickAsync();
		await Task.Delay(100); // Wait for menu to open
		saveBtn = page.GetByTestId("nav-project-save");
		// MudBlazor uses aria-disabled instead of disabled attribute
		await Expect(saveBtn).ToHaveAttributeAsync("aria-disabled", "true");
	}
}