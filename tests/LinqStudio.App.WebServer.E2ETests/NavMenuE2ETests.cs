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

		// Create a new project — a new project is dirty (HasUnsavedChanges = true) by default
		// since it has never been saved.
		await E2ETestHelpers.CreateNewProjectAsync(page, _app);

		// Verify project shows unsaved indicator (new projects are dirty immediately)
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

		// Verify new project was created — it is also "Untitled *" since new projects are dirty
		await Expect(projectGroup).ToContainTextAsync("Untitled *");
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

		// Create a new project — it is dirty by default since it has never been saved.
		await E2ETestHelpers.CreateNewProjectAsync(page, _app);

		// Verify project shows unsaved indicator (new projects are dirty immediately)
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
	public async Task Editor_ShowsNoQueryAlert_WhenAllQueriesClosed()
	{
		Assert.NotNull(_pw.Browser);

		await using var context = await _pw.Browser.NewContextAsync();
		var page = await context.NewPageAsync();

		// Create a project with a SQLite connection and open one query in the editor
		await E2ETestHelpers.SetupEditorAsync(page, _app);

		// Close the only open query tab using the close button in the editor toolbar.
		// New queries have HasUnsavedChanges = true, so a confirmation dialog will appear.
		var closeBtn = page.GetByTestId("query-close-btn");
		await Expect(closeBtn).ToBeVisibleAsync();
		await closeBtn.ClickAsync();

		// Confirm the unsaved-changes dialog (new query is always unsaved)
		var confirmBtn = page.GetByTestId("unsaved-changes-confirm-btn");
		await Expect(confirmBtn).ToBeVisibleAsync();
		await confirmBtn.ClickAsync();

		// Verify "no queries" message is shown when all tabs are closed
		var noQueryAlert = page.GetByTestId("no-query-alert");
		await Expect(noQueryAlert).ToBeVisibleAsync(new() { Timeout = 10_000 });
		await Expect(noQueryAlert).ToContainTextAsync("Right-click the database connection");
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
		await page.GetByTestId("nav-project").ClickAsync();
		await Task.Delay(100);
		await page.GetByTestId("nav-project-properties").ClickAsync();

		var editDialog = page.GetByTestId("edit-project-dialog");
		await Expect(editDialog).ToBeVisibleAsync();

		var connectionStringField = page.GetByTestId("project-connection-string-field");
		await connectionStringField.FillAsync("Server=localhost;Database=TestDb;Integrated Security=true;");

		var saveBtn = page.GetByTestId("edit-project-save-btn");
		await saveBtn.ClickAsync();

		await Expect(editDialog).Not.ToBeVisibleAsync();

		// Verify project shows unsaved indicator after properties update
		var projectGroup = page.GetByTestId("nav-project");
		await Expect(projectGroup).ToContainTextAsync("Untitled *");

		// --- Save the project via ProjectBrowserDialog ---
		await page.GetByTestId("nav-project").ClickAsync();
		await Task.Delay(100);
		await page.GetByTestId("nav-project-save-as").ClickAsync();

		// ProjectBrowserDialog should open
		var browserDialog = page.GetByTestId("project-browser-dialog");
		await Expect(browserDialog).ToBeVisibleAsync();

		// Type the project name
		var nameInput = page.GetByTestId("project-name-input");
		await nameInput.FillAsync("TestProject");

		// Click Save
		var saveBtnDialog = page.GetByTestId("project-browser-save-btn");
		await saveBtnDialog.ClickAsync();

		// Verify snackbar shows success message
		var snackbar = page.Locator(".mud-snackbar").Last;
		await Expect(snackbar).ToBeVisibleAsync();
		await Expect(snackbar).ToContainTextAsync("Project saved successfully");

		// Verify the file was created in the mock directory
		Assert.True(_app.MockFileSystemService.TestFileExists($"TestProject{FileExtensions.Project.WithDot()}"));

		// --- Verify the saved file contains all expected content ---
		var fileContent = _app.MockFileSystemService.ReadTestFile($"TestProject{FileExtensions.Project.WithDot()}");
		var project = JsonSerializer.Deserialize<Project>(fileContent);

		Assert.NotNull(project);

		// Verify connection string was saved
		Assert.Equal("Server=localhost;Database=TestDb;Integrated Security=true;", project.ConnectionString);

		// Verify unsaved indicator is cleared after save
		await Expect(projectGroup).Not.ToContainTextAsync("*");

		// Verify Save button is disabled
		await page.GetByTestId("nav-project").ClickAsync();
		await Task.Delay(100);
		saveBtn = page.GetByTestId("nav-project-save");
		await Expect(saveBtn).ToHaveAttributeAsync("aria-disabled", "true");
	}

	[Fact(Timeout = 120_000)]
	public async Task NavMenu_OpenProject_ExistingProject_LoadsProjectInEditor()
	{
		Assert.NotNull(_pw.Browser);

		await using var context = await _pw.Browser.NewContextAsync();
		var page = await context.NewPageAsync();

		// Step 1: Create a new project and save it as "OpenTestProject"
		// A new project always starts dirty (HasUnsavedChanges = true), so we save it
		// first so that we can close it cleanly without a confirmation dialog.
		await E2ETestHelpers.CreateNewProjectAsync(page, _app);

		await page.GetByTestId("nav-project").ClickAsync();
		await Task.Delay(100);
		await page.GetByTestId("nav-project-save-as").ClickAsync();

		var browserDialog = page.GetByTestId("project-browser-dialog");
		await Expect(browserDialog).ToBeVisibleAsync();

		var nameInput = page.GetByTestId("project-name-input");
		await nameInput.FillAsync("OpenTestProject");

		await page.GetByTestId("project-browser-save-btn").ClickAsync();

		var saveSnackbar = page.Locator(".mud-snackbar").Last;
		await Expect(saveSnackbar).ToBeVisibleAsync();
		await Expect(saveSnackbar).ToContainTextAsync("Project saved successfully");

		var projectGroup = page.GetByTestId("nav-project");
		await Expect(projectGroup).Not.ToContainTextAsync("*");

		// Step 2: Close the project — no unsaved-changes dialog because it was just saved
		await page.GetByTestId("nav-project").ClickAsync();
		await Task.Delay(100);
		await page.GetByTestId("nav-project-close").ClickAsync();

		// No confirmation dialog expected since HasUnsavedChanges = false after SaveAs
		await page.WaitForURLAsync(_app.BaseUrl.ToString());
		await Expect(projectGroup).ToContainTextAsync("Project");
		await Expect(projectGroup).Not.ToContainTextAsync("OpenTestProject");

		// Step 3: Open the project browser dialog in Open mode
		// With no project open, HasUnsavedChanges = false — the browser dialog opens directly
		await page.GetByTestId("nav-project").ClickAsync();
		await Task.Delay(100);
		await page.GetByTestId("nav-project-open").ClickAsync();

		// Verify dialog opened in Open mode (has "Open" button, no name text-field)
		await Expect(browserDialog).ToBeVisibleAsync();
		await Expect(page.GetByTestId("project-browser-open-btn")).ToBeVisibleAsync();

		// Step 4: Select "OpenTestProject" from the project list
		var projectItem = page.GetByTestId("project-list-item")
			.Filter(new() { HasText = "OpenTestProject" });
		await Expect(projectItem).ToBeVisibleAsync(new() { Timeout = 10_000 });
		await projectItem.ClickAsync();

		// Step 5: Confirm the open
		await page.GetByTestId("project-browser-open-btn").ClickAsync();

		// Step 6: Verify the project is now loaded in the workspace
		await Expect(projectGroup).ToContainTextAsync("OpenTestProject");
		await Expect(projectGroup).Not.ToContainTextAsync("*");

		var successSnackbar = page.Locator(".mud-snackbar").Last;
		await Expect(successSnackbar).ToContainTextAsync("loaded successfully");
	}
}