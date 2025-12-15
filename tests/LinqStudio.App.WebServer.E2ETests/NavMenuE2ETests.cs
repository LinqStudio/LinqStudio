using LinqStudio.App.WebServer.E2ETests.Fixtures;
using Microsoft.Playwright;
using Xunit;
using static Microsoft.Playwright.Assertions;

namespace LinqStudio.App.WebServer.E2ETests;

[Collection("E2E")]
public class NavMenuE2ETests(AppServerFixture app, PlaywrightFixture pw)
{
	private readonly AppServerFixture _app = app;
	private readonly PlaywrightFixture _pw = pw;

	/// <summary>
	/// Helper method to create a new project
	/// </summary>
	private async Task CreateNewProjectAsync(IPage page)
	{
		await page.GotoAsync(_app.BaseUrl.ToString());
		await page.GetByTestId("nav-project-new").ClickAsync();
		await page.WaitForURLAsync(_app.BaseUrl.ToString());
	}

	/// <summary>
	/// Helper method to create a query for testing
	/// </summary>
	private async Task CreateQueryAsync(IPage page, string queryText = "context.")
	{
		await page.GetByTestId("nav-query-create").ClickAsync();
		await page.WaitForURLAsync($"{_app.BaseUrl}editor/*");
		await Expect(page.GetByTestId("monaco-editor-container")).ToBeVisibleAsync();

		// Type some content to make the query "dirty"
		var monacoEditor = page.Locator("#editor-top .monaco-editor");
		await Expect(monacoEditor).ToBeVisibleAsync();
		await monacoEditor.ClickAsync();
		await page.Keyboard.PressAsync("Control+A");
		await page.Keyboard.TypeAsync(queryText);
	}

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
		await CreateNewProjectAsync(page);
		await CreateQueryAsync(page, "context.People");

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
		await CreateNewProjectAsync(page);

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
		await CreateNewProjectAsync(page);
		await CreateQueryAsync(page, "context.People");

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
	public async Task NavMenu_SaveButton_DisabledWhenNoChanges()
	{
		Assert.NotNull(_pw.Browser);

		await using var context = await _pw.Browser.NewContextAsync();
		var page = await context.NewPageAsync();

		// Create a project
		await CreateNewProjectAsync(page);

		// Verify Save button is disabled (no changes yet)
		var saveBtn = page.GetByTestId("nav-project-save");
		await Expect(saveBtn).ToBeVisibleAsync();
		await Expect(saveBtn).ToBeDisabledAsync();

		// Make a change by creating a query
		await CreateQueryAsync(page, "context.People");

		// Navigate back to home
		await page.GetByTestId("nav-home").ClickAsync();

		// Verify Save button is now enabled
		await Expect(saveBtn).ToBeEnabledAsync();

		// Verify project shows unsaved indicator
		var projectGroup = page.GetByTestId("nav-project-group");
		await Expect(projectGroup).ToContainTextAsync("Untitled *");
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
		await Expect(editorLink).ToBeDisabledAsync();

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
		await CreateNewProjectAsync(page);

		// Verify queries section shows empty message
		var queriesGroup = page.GetByTestId("nav-queries-group");
		await Expect(queriesGroup).ToBeVisibleAsync();

		var emptyMessage = page.GetByTestId("nav-queries-empty");
		await Expect(emptyMessage).ToBeVisibleAsync();
		await Expect(emptyMessage).ToContainTextAsync("No queries yet");
	}

	[Fact(Timeout = 60_000)]
	public async Task NavMenu_CreateQuery_AddsQueryToList()
	{
		Assert.NotNull(_pw.Browser);

		await using var context = await _pw.Browser.NewContextAsync();
		var page = await context.NewPageAsync();

		// Create a project
		await CreateNewProjectAsync(page);

		// Create a query
		await page.GetByTestId("nav-query-create").ClickAsync();

		// Wait for editor to load
		await page.WaitForURLAsync($"{_app.BaseUrl}editor/*");

		// Navigate back to home
		await page.GetByTestId("nav-home").ClickAsync();

		// Verify query appears in the list
		var query0 = page.GetByTestId("nav-query-0");
		await Expect(query0).ToBeVisibleAsync();
		await Expect(query0).ToContainTextAsync("Query");

		// Verify empty message is no longer shown
		var emptyMessage = page.GetByTestId("nav-queries-empty");
		await Expect(emptyMessage).Not.ToBeVisibleAsync();
	}

	[Fact(Timeout = 60_000)]
	public async Task NavMenu_QueryList_ShowsUnsavedIndicator()
	{
		Assert.NotNull(_pw.Browser);

		await using var context = await _pw.Browser.NewContextAsync();
		var page = await context.NewPageAsync();

		// Create a project and query
		await CreateNewProjectAsync(page);
		await CreateQueryAsync(page, "context.People");

		// Navigate back to home
		await page.GetByTestId("nav-home").ClickAsync();

		// Verify query shows unsaved indicator (asterisk)
		var query0 = page.GetByTestId("nav-query-0");
		await Expect(query0).ToBeVisibleAsync();
		await Expect(query0).ToContainTextAsync("Query 1 *");
	}

	[Fact(Timeout = 60_000)]
	public async Task NavMenu_Properties_UpdateConnectionString()
	{
		Assert.NotNull(_pw.Browser);

		await using var context = await _pw.Browser.NewContextAsync();
		var page = await context.NewPageAsync();

		// Create a project
		await CreateNewProjectAsync(page);

		// Click Properties
		await page.GetByTestId("nav-project-properties").ClickAsync();

		// Verify Edit Project dialog appears
		var dialog = page.GetByTestId("edit-project-dialog");
		await Expect(dialog).ToBeVisibleAsync();

		// Update connection string
		var connectionStringField = page.GetByTestId("project-connection-string-field");
		await connectionStringField.FillAsync("Server=localhost;Database=TestDb;");

		// Save changes
		var saveBtn = page.GetByTestId("edit-project-save-btn");
		await saveBtn.ClickAsync();

		// Verify dialog is closed
		await Expect(dialog).Not.ToBeVisibleAsync();

		// Verify project shows unsaved indicator
		var projectGroup = page.GetByTestId("nav-project-group");
		await Expect(projectGroup).ToContainTextAsync("Untitled *");
	}

	[Fact(Timeout = 60_000)]
	public async Task NavMenu_SaveAs_SavesProjectToFile()
	{
		Assert.NotNull(_pw.Browser);

		await using var context = await _pw.Browser.NewContextAsync();
		var page = await context.NewPageAsync();

		// Create a project with a query
		await CreateNewProjectAsync(page);
		await CreateQueryAsync(page, "context.People.Where(x => x.Id > 0)");

		// Navigate back to home
		await page.GetByTestId("nav-home").ClickAsync();

		// Set up mock to return a file path within the test directory
		_app.MockFileSystemService.SetNextSaveFileResult("TestProject.linq");

		// Click Save As
		await page.GetByTestId("nav-project-save-as").ClickAsync();

		// Verify snackbar shows success message
		var snackbar = page.Locator(".mud-snackbar").Last;
		await Expect(snackbar).ToBeVisibleAsync();
		await Expect(snackbar).ToContainTextAsync("Project saved successfully");

		// Verify the file was created in the test directory
		Assert.True(_app.MockFileSystemService.TestFileExists("TestProject.linq"));

		// Verify the file contains expected content
		var fileContent = _app.MockFileSystemService.ReadTestFile("TestProject.linq");
		Assert.Contains("context.People.Where(x => x.Id > 0)", fileContent);
	}
}