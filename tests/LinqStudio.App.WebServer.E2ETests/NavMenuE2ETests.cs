using LinqStudio.App.WebServer.E2ETests.Fixtures;
using LinqStudio.App.WebServer.E2ETests.Helpers;
using LinqStudio.Core.Models;
using LinqStudio.Core.Services;
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
		var projectGroup = page.GetByTestId("nav-project-group");
		await Expect(projectGroup).ToContainTextAsync("Project");

		// Click "New" to create a project
		await page.GetByTestId("nav-project-new").ClickAsync();

		// Verify we're redirected to home
		await page.WaitForURLAsync(_app.BaseUrl.ToString());

		// Verify snackbar appears with success message
		var snackbar = page.Locator(".mud-snackbar");
		await Expect(snackbar).ToBeVisibleAsync();
		await Expect(snackbar).ToContainTextAsync("New project created");

		// Verify project title shows "Untitled"
		await Expect(projectGroup).ToContainTextAsync("Untitled");

		// Verify project-specific menu items are now visible
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

		// Verify project shows unsaved indicator and has a query
		var projectGroup = page.GetByTestId("nav-project-group");
		await Expect(projectGroup).ToContainTextAsync("Untitled *");
		var query0 = page.GetByTestId("nav-query-0");
		await Expect(query0).ToBeVisibleAsync();

		// Try to create a new project (should show confirmation dialog)
		await page.GetByTestId("nav-project-new").ClickAsync();

		// Verify confirmation dialog appears
		var dialog = page.GetByTestId("unsaved-changes-dialog");
		await Expect(dialog).ToBeVisibleAsync();

		// Click Cancel
		var cancelBtn = page.GetByTestId("unsaved-changes-cancel-btn");
		await cancelBtn.ClickAsync();

		// Verify we're still on the same project with the query
		await Expect(projectGroup).ToContainTextAsync("Untitled *");
		await Expect(query0).ToBeVisibleAsync();

		// Try again and confirm
		await page.GetByTestId("nav-project-new").ClickAsync();
		await Expect(dialog).ToBeVisibleAsync();

		var confirmBtn = page.GetByTestId("unsaved-changes-confirm-btn");
		await confirmBtn.ClickAsync();

		// Verify new project was created
		// New projects are also "Untitled" with unsaved changes (asterisk), 
		// but the queries list should be empty
		await Expect(projectGroup).ToContainTextAsync("Untitled *");

		// Verify the queries list is empty (proving it's a new project)
		var emptyMessage = page.GetByTestId("nav-queries-empty");
		await Expect(emptyMessage).ToBeVisibleAsync();
		await Expect(emptyMessage).ToContainTextAsync("No queries yet");
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
		var projectGroup = page.GetByTestId("nav-project-group");
		await Expect(projectGroup).ToContainTextAsync("Untitled");

		// Close the project (new projects have unsaved changes, so we need to handle the dialog)
		await page.GetByTestId("nav-project-close").ClickAsync();

		// Verify confirmation dialog appears (new project is considered unsaved)
		var dialog = page.GetByTestId("unsaved-changes-dialog");
		await Expect(dialog).ToBeVisibleAsync();

		// Click Continue to close without saving
		var confirmBtn = page.GetByTestId("unsaved-changes-confirm-btn");
		await confirmBtn.ClickAsync();

		// Verify we're redirected to home
		await page.WaitForURLAsync(_app.BaseUrl.ToString());

		// Verify project-specific menu items are hidden
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
		var projectGroup = page.GetByTestId("nav-project-group");
		await Expect(projectGroup).ToContainTextAsync("Untitled *");

		// Try to close the project
		await page.GetByTestId("nav-project-close").ClickAsync();

		// Verify confirmation dialog appears with Continue/Cancel options
		var dialog = page.GetByTestId("unsaved-changes-dialog");
		await Expect(dialog).ToBeVisibleAsync();

		// Click "Cancel" to keep the project open
		var cancelBtn = page.GetByTestId("unsaved-changes-cancel-btn");
		await cancelBtn.ClickAsync();

		// Verify project is still open
		await Expect(projectGroup).ToContainTextAsync("Untitled *");

		// Try again and click "Continue" to close without saving
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

		// Verify queries section is not visible
		var queriesGroup = page.GetByTestId("nav-queries-group");
		await Expect(queriesGroup).Not.ToBeVisibleAsync();

		// Verify editor link is disabled
		var editorLink = page.GetByTestId("nav-editor-disabled");
		await Expect(editorLink).ToBeVisibleAsync();

		// Create a project
		await page.GetByTestId("nav-project-new").ClickAsync();

		// Verify queries section is now visible
		await Expect(queriesGroup).ToBeVisibleAsync();

		// Verify editor link is no longer shown (queries section replaces it)
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

		// Verify queries section shows empty message
		var queriesGroup = page.GetByTestId("nav-queries-group");
		await Expect(queriesGroup).ToBeVisibleAsync();

		var emptyMessage = page.GetByTestId("nav-queries-empty");
		await Expect(emptyMessage).ToBeVisibleAsync();
		await Expect(emptyMessage).ToContainTextAsync("No queries yet");
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
		await page.GetByTestId("nav-project-properties").ClickAsync();

		var dialog = page.GetByTestId("edit-project-dialog");
		await Expect(dialog).ToBeVisibleAsync();

		var connectionStringField = page.GetByTestId("project-connection-string-field");
		await connectionStringField.FillAsync("Server=localhost;Database=TestDb;Integrated Security=true;");

		var saveBtn = page.GetByTestId("edit-project-save-btn");
		await saveBtn.ClickAsync();

		await Expect(dialog).Not.ToBeVisibleAsync();

		// Verify project shows unsaved indicator after properties update
		var projectGroup = page.GetByTestId("nav-project-group");
		await Expect(projectGroup).ToContainTextAsync("Untitled *");

		// --- Create first query with custom name and content ---
		await E2ETestHelpers.CreateQueryAsync(page, _app, "context.People.Where(x => x.Id > 10).OrderBy(x => x.Name)");

		// Verify unsaved indicator appears in editor
		var unsavedIndicator = page.GetByTestId("query-unsaved-indicator");
		await Expect(unsavedIndicator).ToBeVisibleAsync();

		// Rename the first query
		await page.GetByTestId("query-rename-btn").ClickAsync();
		var renameInput = page.GetByTestId("query-name-input");
		await Expect(renameInput).ToBeVisibleAsync();
		await renameInput.FillAsync("Get Filtered People");
		await page.GetByTestId("query-rename-save-btn").ClickAsync();

		// Verify rename succeeded
		var queryName = page.GetByTestId("query-name-display");
		await Expect(queryName).ToContainTextAsync("Get Filtered People");

		// --- Create second query with different content ---
		await E2ETestHelpers.CreateQueryAsync(page, _app, "context.People.Select(x => new { x.Id, x.Name }).Take(100)", 1);

		// Verify unsaved indicator appears
		unsavedIndicator = page.GetByTestId("query-unsaved-indicator");
		await Expect(unsavedIndicator).ToBeVisibleAsync();

		// Rename the second query
		await page.GetByTestId("query-rename-btn").ClickAsync();
		renameInput = page.GetByTestId("query-name-input");
		await Expect(renameInput).ToBeVisibleAsync();
		await renameInput.FillAsync("Get People Summary");
		await page.GetByTestId("query-rename-save-btn").ClickAsync();

		// Verify rename succeeded
		queryName = page.GetByTestId("query-name-display");
		await Expect(queryName).ToContainTextAsync("Get People Summary");

		// Navigate back to home
		await page.GetByTestId("nav-home").ClickAsync();

		// Verify both queries show unsaved indicators in nav menu
		var query0 = page.GetByTestId("nav-query-0");
		var query1 = page.GetByTestId("nav-query-1");
		await Expect(query0).ToContainTextAsync("Get Filtered People *");
		await Expect(query1).ToContainTextAsync("Get People Summary *");

		// --- Save the project ---
		_app.MockFileSystemService.SetNextSaveFileResult("TestProject.linq");

		await page.GetByTestId("nav-project-save-as").ClickAsync();

		// Verify snackbar shows success message
		var snackbar = page.Locator(".mud-snackbar").Last;
		await Expect(snackbar).ToBeVisibleAsync();
		await Expect(snackbar).ToContainTextAsync("Project saved successfully");

		// Verify the file was created
		Assert.True(_app.MockFileSystemService.TestFileExists("TestProject.linq"));

		// --- Verify the saved file contains all expected content ---
		var fileContent = _app.MockFileSystemService.ReadTestFile("TestProject.linq");
		var project = JsonSerializer.Deserialize<Project>(fileContent);

		Assert.NotNull(project);

		// Verify connection string was saved
		Assert.Equal("Server=localhost;Database=TestDb;Integrated Security=true;", project.ConnectionString);

		// Verify we have 2 queries in separate files
		var queryService = new QueryService();
		var queries = await queryService.LoadQueriesAsync("TestProject.linq");
		Assert.Equal(2, queries.Count);

		// Verify first query
		var firstQuery = queries.FirstOrDefault(q => q.Name == "Get Filtered People");
		Assert.NotNull(firstQuery);
		Assert.Contains("context.People.Where(x => x.Id > 10).OrderBy(x => x.Name)", firstQuery.QueryText);

		// Verify second query
		var secondQuery = queries.FirstOrDefault(q => q.Name == "Get People Summary");
		Assert.NotNull(secondQuery);
		Assert.Contains("context.People.Select(x => new { x.Id, x.Name }).Take(100)", secondQuery.QueryText);

		// Verify unsaved indicators are cleared after save
		await Expect(projectGroup).Not.ToContainTextAsync("*");
		await Expect(query0).Not.ToContainTextAsync("*");
		await Expect(query1).Not.ToContainTextAsync("*");

		// Verify Save button is disabled
		saveBtn = page.GetByTestId("nav-project-save");
		await Expect(saveBtn).ToHaveAttributeAsync("disabled", "");
	}
}