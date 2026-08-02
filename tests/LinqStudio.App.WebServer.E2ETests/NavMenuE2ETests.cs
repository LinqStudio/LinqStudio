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
		await page.ExpectNoProjectIsOpenAsync();
		var projectGroup = page.Get_Navigation_ProjectMenu();

		// Open the Project menu and click "New" to create a project
		await page.Get_Navigation_ProjectMenu().ClickAsync();
		await Task.Delay(100); // Wait for menu to open
		await page.Get_Navigation_ProjectNew().ClickAsync();

		// Verify we're redirected to home
		await page.WaitForURLAsync(_app.BaseUrl.ToString());

		// Verify snackbar appears with success message
		var snackbar = page.Get_Navigation_Snackbar();
		await Expect(snackbar).ToBeVisibleAsync();
		await Expect(snackbar).ToContainTextAsync("New project created");

		// Verify project title shows "Untitled"
		await page.ExpectProjectIsOpenAsync();

		// Verify project-specific menu items are now visible (need to open menu to see them)
		await page.Get_Navigation_ProjectMenu().ClickAsync();
		await Task.Delay(100); // Wait for menu to open
		await Expect(page.Get_Navigation_ProjectProperties()).ToBeVisibleAsync();
		await Expect(page.Get_Navigation_ProjectSave()).ToBeVisibleAsync();
		await Expect(page.Get_Navigation_ProjectSaveAs()).ToBeVisibleAsync();
		await Expect(page.Get_Navigation_ProjectClose()).ToBeVisibleAsync();
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
		var projectGroup = page.Get_Navigation_ProjectMenu();
		await page.ExpectUnsavedProjectAsync();

		// Try to create a new project (should show confirmation dialog) - need to open menu first
		await page.Get_Navigation_ProjectMenu().ClickAsync();
		await Task.Delay(100); // Wait for menu to open
		await page.Get_Navigation_ProjectNew().ClickAsync();

		// Verify confirmation dialog appears
		var dialog = page.Get_Navigation_UnsavedChangesDialog();
		await Expect(dialog).ToBeVisibleAsync();

		// Click Cancel
		var cancelBtn = page.Get_Navigation_UnsavedChangesCancelButton();
		await cancelBtn.ClickAsync();

		// Verify we're still on the same project
		await page.ExpectUnsavedProjectAsync();

		// Try again and confirm - need to open menu again
		await page.Get_Navigation_ProjectMenu().ClickAsync();
		await Task.Delay(100); // Wait for menu to open
		await page.Get_Navigation_ProjectNew().ClickAsync();
		await Expect(dialog).ToBeVisibleAsync();

		var confirmBtn = page.Get_Navigation_UnsavedChangesConfirmButton();
		await confirmBtn.ClickAsync();

		// Verify new project was created — it is also "Untitled *" since new projects are dirty
		await page.ExpectUnsavedProjectAsync();
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
		var projectGroup = page.Get_Navigation_ProjectMenu();
		await page.ExpectProjectIsOpenAsync();

		// Close the project (new projects have unsaved changes, so we need to handle the dialog)
		// Need to open menu first
		await page.Get_Navigation_ProjectMenu().ClickAsync();
		await Task.Delay(100); // Wait for menu to open
		await page.Get_Navigation_ProjectClose().ClickAsync();

		// Verify confirmation dialog appears (new project is considered unsaved)
		var dialog = page.Get_Navigation_UnsavedChangesDialog();
		await Expect(dialog).ToBeVisibleAsync();

		// Click Continue to close without saving
		var confirmBtn = page.Get_Navigation_UnsavedChangesConfirmButton();
		await confirmBtn.ClickAsync();

		// Verify we're redirected to home
		await page.WaitForURLAsync(_app.BaseUrl.ToString());

		// Verify project-specific menu items are hidden (open menu to check)
		await page.Get_Navigation_ProjectMenu().ClickAsync();
		await Task.Delay(100); // Wait for menu to open
		await Expect(page.Get_Navigation_ProjectSave()).Not.ToBeVisibleAsync();
		await Expect(page.Get_Navigation_ProjectClose()).Not.ToBeVisibleAsync();
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
		var projectGroup = page.Get_Navigation_ProjectMenu();
		await page.ExpectUnsavedProjectAsync();

		// Try to close the project - need to open menu first
		await page.Get_Navigation_ProjectMenu().ClickAsync();
		await Task.Delay(100); // Wait for menu to open
		await page.Get_Navigation_ProjectClose().ClickAsync();

		// Verify confirmation dialog appears with Continue/Cancel options
		var dialog = page.Get_Navigation_UnsavedChangesDialog();
		await Expect(dialog).ToBeVisibleAsync();

		// Click "Cancel" to keep the project open
		var cancelBtn = page.Get_Navigation_UnsavedChangesCancelButton();
		await cancelBtn.ClickAsync();

		// Verify project is still open
		await page.ExpectUnsavedProjectAsync();

		// Try again and click "Continue" to close without saving - need to open menu again
		await page.Get_Navigation_ProjectMenu().ClickAsync();
		await Task.Delay(100); // Wait for menu to open
		await page.Get_Navigation_ProjectClose().ClickAsync();
		await Expect(dialog).ToBeVisibleAsync();

		var confirmBtn = page.Get_Navigation_UnsavedChangesConfirmButton();
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
		var closeBtn = page.Get_QueryEditor_CloseButton();
		await Expect(closeBtn).ToBeVisibleAsync();
		await closeBtn.ClickAsync();

		// Confirm the unsaved-changes dialog (new query is always unsaved)
		var confirmBtn = page.Get_Navigation_UnsavedChangesConfirmButton();
		await Expect(confirmBtn).ToBeVisibleAsync();
		await confirmBtn.ClickAsync();

		// Verify "no queries" message is shown when all tabs are closed
		var noQueryAlert = page.Get_QueryEditor_NoQueryAlert();
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
		await page.Get_Navigation_ProjectMenu().ClickAsync();
		await Task.Delay(100);
		await page.Get_Navigation_ProjectProperties().ClickAsync();

		var editDialog = page.Get_Navigation_EditProjectDialog();
		await Expect(editDialog).ToBeVisibleAsync();

		var connectionStringField = page.Get_Navigation_ProjectConnectionStringField();
		await connectionStringField.FillAsync("Server=localhost;Database=TestDb;Integrated Security=true;");

		var saveBtn = page.Get_Navigation_EditProjectSaveButton();
		await saveBtn.ClickAsync();

		await Expect(editDialog).Not.ToBeVisibleAsync();

		// Verify project shows unsaved indicator after properties update
		var projectGroup = page.Get_Navigation_ProjectMenu();
		await Expect(projectGroup).ToContainTextAsync("Untitled *");

		// --- Save the project via ProjectBrowserDialog ---
		await page.Get_Navigation_ProjectMenu().ClickAsync();
		await Task.Delay(100);
		await page.Get_Navigation_ProjectSaveAs().ClickAsync();

		// ProjectBrowserDialog should open
		var browserDialog = page.Get_Navigation_ProjectBrowserDialog();
		await Expect(browserDialog).ToBeVisibleAsync();

		// Type the project name
		var nameInput = page.Get_Navigation_ProjectNameInput();
		await nameInput.FillAsync("TestProject");

		// Click Save
		var saveBtnDialog = page.Get_Navigation_ProjectBrowserSaveButton();
		await saveBtnDialog.ClickAsync();

		// Verify snackbar shows success message
		var snackbar = page.Get_Navigation_Snackbar().Last;
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
		await page.Get_Navigation_ProjectMenu().ClickAsync();
		await Task.Delay(100);
		saveBtn = page.Get_Navigation_ProjectSave();
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

		await page.Get_Navigation_ProjectMenu().ClickAsync();
		await Task.Delay(100);
		await page.Get_Navigation_ProjectSaveAs().ClickAsync();

		var browserDialog = page.Get_Navigation_ProjectBrowserDialog();
		await Expect(browserDialog).ToBeVisibleAsync();

		var nameInput = page.Get_Navigation_ProjectNameInput();
		await nameInput.FillAsync("OpenTestProject");

		await page.Get_Navigation_ProjectBrowserSaveButton().ClickAsync();

		var saveSnackbar = page.Get_Navigation_Snackbar().Last;
		await Expect(saveSnackbar).ToBeVisibleAsync();
		await Expect(saveSnackbar).ToContainTextAsync("Project saved successfully");

		var projectGroup = page.Get_Navigation_ProjectMenu();
		await Expect(projectGroup).Not.ToContainTextAsync("*");

		// Step 2: Close the project — no unsaved-changes dialog because it was just saved
		await page.Get_Navigation_ProjectMenu().ClickAsync();
		await Task.Delay(100);
		await page.Get_Navigation_ProjectClose().ClickAsync();

		// No confirmation dialog expected since HasUnsavedChanges = false after SaveAs
		await page.WaitForURLAsync(_app.BaseUrl.ToString());
		await Expect(projectGroup).ToContainTextAsync("Project");
		await Expect(projectGroup).Not.ToContainTextAsync("OpenTestProject");

		// Step 3: Open the project browser dialog in Open mode
		// With no project open, HasUnsavedChanges = false — the browser dialog opens directly
		await page.Get_Navigation_ProjectMenu().ClickAsync();
		await Task.Delay(100);
		await page.Get_Navigation_ProjectOpen().ClickAsync();

		// Verify dialog opened in Open mode (has "Open" button, no name text-field)
		await Expect(browserDialog).ToBeVisibleAsync();
		await Expect(page.Get_Navigation_ProjectBrowserOpenButton()).ToBeVisibleAsync();

		// Step 4: Select "OpenTestProject" from the project list
		var projectItem = page.Get_Navigation_ProjectListItem("OpenTestProject");
		await Expect(projectItem).ToBeVisibleAsync(new() { Timeout = 10_000 });
		await projectItem.ClickAsync();

		// Step 5: Confirm the open
		await page.Get_Navigation_ProjectBrowserOpenButton().ClickAsync();

		// Step 6: Verify the project is now loaded in the workspace
		await Expect(projectGroup).ToContainTextAsync("OpenTestProject");
		await Expect(projectGroup).Not.ToContainTextAsync("*");

		var successSnackbar = page.Get_Navigation_Snackbar().Last;
		await Expect(successSnackbar).ToContainTextAsync("loaded successfully");
	}
}