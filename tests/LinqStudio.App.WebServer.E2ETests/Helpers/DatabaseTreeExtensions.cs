using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace LinqStudio.App.WebServer.E2ETests.Helpers;

public static class DatabaseTreeExtensions
{
	public static ILocator Get_DatabaseTree_View(this IPage page) =>
		page.GetByTestId(E2ESelectors.DatabaseTreeView);

	public static ILocator Get_DatabaseTree_Placeholder(this IPage page) =>
		page.GetByTestId(E2ESelectors.DatabaseTreePlaceholder);

	public static ILocator Get_DatabaseTree_RefreshButton(this IPage page) =>
		page.GetByTestId(E2ESelectors.DatabaseTreeRefresh);

	public static ILocator Get_DatabaseTree_Connection(this IPage page) =>
		page.GetByTestId(E2ESelectors.DatabaseTreeConnection);

	public static ILocator Get_DatabaseTree_ConnectionBody(this IPage page) =>
		page.GetByTestId(E2ESelectors.DatabaseTreeConnectionBody);

	public static ILocator Get_DatabaseTree_NewQuery(this IPage page) =>
		page.GetByTestId(E2ESelectors.DatabaseTreeConnectionNewQuery);

	public static ILocator Get_DatabaseTree_TablesFolder(this IPage page) =>
		page.GetByTestId(E2ESelectors.DatabaseTreeTablesFolder);

	public static ILocator Get_DatabaseTree_Table(this IPage page, string fullName) =>
		page.GetByTestId(E2ESelectors.Table(fullName));

	public static ILocator Get_DatabaseTree_Column(this IPage page, string tableName, string columnName) =>
		page.GetByTestId(E2ESelectors.Column(tableName, columnName));

	public static ILocator Get_DatabaseTree_TablesFolderRefresh(this IPage page) =>
		page.GetByTestId(E2ESelectors.DatabaseTreeTablesFolderRefresh);

	public static ILocator Get_DatabaseTree_TableRefresh(this IPage page, string fullName) =>
		page.GetByTestId(E2ESelectors.TableRefresh(fullName));

	public static async Task ExpectDatabaseTreeVisibleAsync(this IPage page)
	{
		await Expect(page.Get_DatabaseTree_View()).ToBeVisibleAsync();
		await Expect(page.Get_DatabaseTree_Placeholder()).Not.ToBeVisibleAsync();
	}

	public static async Task ExpectNoDatabaseConnectionAsync(this IPage page)
	{
		await Expect(page.Get_DatabaseTree_Placeholder()).ToBeVisibleAsync();
		await Expect(page.Get_DatabaseTree_View()).Not.ToBeVisibleAsync();
	}

	public static async Task ExpandDatabaseTreeItemAsync(this ILocator treeItem)
	{
		await treeItem.Locator("button").First.ClickAsync();
		await Task.Delay(200);
	}
}
