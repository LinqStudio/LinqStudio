using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace LinqStudio.App.WebServer.E2ETests.Helpers;

public static class NavigationExtensions
{
	public static ILocator Get_Navigation_ProjectMenu(this IPage page) =>
		page.GetByTestId(E2ESelectors.ProjectMenu);

	public static ILocator Get_Navigation_ProjectNew(this IPage page) =>
		page.GetByTestId(E2ESelectors.ProjectNew);

	public static ILocator Get_Navigation_ProjectOpen(this IPage page) =>
		page.GetByTestId(E2ESelectors.ProjectOpen);

	public static ILocator Get_Navigation_ProjectProperties(this IPage page) =>
		page.GetByTestId(E2ESelectors.ProjectProperties);

	public static ILocator Get_Navigation_ProjectSave(this IPage page) =>
		page.GetByTestId(E2ESelectors.ProjectSave);

	public static ILocator Get_Navigation_ProjectSaveAs(this IPage page) =>
		page.GetByTestId(E2ESelectors.ProjectSaveAs);

	public static ILocator Get_Navigation_ProjectClose(this IPage page) =>
		page.GetByTestId(E2ESelectors.ProjectClose);

	public static ILocator Get_Navigation_ProjectBrowserDialog(this IPage page) =>
		page.GetByTestId(E2ESelectors.ProjectBrowserDialog);

	public static ILocator Get_Navigation_ProjectListItem(this IPage page, string projectName) =>
		page.GetByTestId(E2ESelectors.ProjectListItem).Filter(new() { HasText = projectName });

	public static ILocator Get_Navigation_ProjectBrowserOpenButton(this IPage page) =>
		page.GetByTestId(E2ESelectors.ProjectBrowserOpenButton);

	public static ILocator Get_Navigation_ProjectBrowserSaveButton(this IPage page) =>
		page.GetByTestId(E2ESelectors.ProjectBrowserSaveButton);

	public static ILocator Get_Navigation_ProjectNameInput(this IPage page) =>
		page.GetByTestId(E2ESelectors.ProjectNameInput);

	public static ILocator Get_Navigation_ProjectConnectionStringField(this IPage page) =>
		page.GetByTestId(E2ESelectors.ProjectConnectionStringField);

	public static ILocator Get_Navigation_EditProjectDialog(this IPage page) =>
		page.GetByTestId(E2ESelectors.EditProjectDialog);

	public static ILocator Get_Navigation_EditProjectSaveButton(this IPage page) =>
		page.GetByTestId(E2ESelectors.EditProjectSaveButton);

	public static ILocator Get_Navigation_UnsavedChangesDialog(this IPage page) =>
		page.GetByTestId(E2ESelectors.UnsavedChangesDialog);

	public static ILocator Get_Navigation_UnsavedChangesCancelButton(this IPage page) =>
		page.GetByTestId(E2ESelectors.UnsavedChangesCancelButton);

	public static ILocator Get_Navigation_UnsavedChangesConfirmButton(this IPage page) =>
		page.GetByTestId(E2ESelectors.UnsavedChangesConfirmButton);

	public static ILocator Get_Navigation_Snackbar(this IPage page) =>
		page.Locator(E2ESelectors.MudSnackbar);

	public static async Task OpenProjectMenuAsync(this IPage page)
	{
		await page.Get_Navigation_ProjectMenu().ClickAsync();
		await Task.Delay(100);
	}

	public static async Task ExpectNoProjectIsOpenAsync(this IPage page)
	{
		await Expect(page.Get_Navigation_ProjectMenu()).ToContainTextAsync("Project");
		await Expect(page.Get_Navigation_ProjectMenu()).Not.ToContainTextAsync("Untitled");
	}

	public static async Task ExpectProjectIsOpenAsync(this IPage page, string? projectName = null)
	{
		var projectMenu = page.Get_Navigation_ProjectMenu();
		if (projectName is not null)
			await Expect(projectMenu).ToContainTextAsync(projectName);
		else
			await Expect(projectMenu).ToContainTextAsync("Untitled");
	}

	public static async Task ExpectUnsavedProjectAsync(this IPage page, string projectName = "Untitled")
	{
		await Expect(page.Get_Navigation_ProjectMenu()).ToContainTextAsync($"{projectName} *");
	}

	public static async Task ExpectProjectBrowserVisibleAsync(this IPage page) =>
		await Expect(page.Get_Navigation_ProjectBrowserDialog()).ToBeVisibleAsync();

	public static async Task ExpectUnsavedChangesDialogVisibleAsync(this IPage page) =>
		await Expect(page.Get_Navigation_UnsavedChangesDialog()).ToBeVisibleAsync();
}
