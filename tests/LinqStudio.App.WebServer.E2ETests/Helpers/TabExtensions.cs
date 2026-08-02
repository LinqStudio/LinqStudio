using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace LinqStudio.App.WebServer.E2ETests.Helpers;

public static class TabExtensions
{
	public static ILocator Get_QueryTabs(this IPage page) =>
		page.GetByTestId(E2ESelectors.QueryExecutionBar);

	public static ILocator Get_QueryTabButtons(this IPage page) =>
		page.Locator(E2ESelectors.MudTab);

	public static ILocator Get_QueryResults_Tabs(this IPage page) =>
		page.GetByTestId(E2ESelectors.ResultsTabs).Locator(E2ESelectors.MudTab);

	public static ILocator Get_QueryResults_CSharpTabPanel(this IPage page) =>
		page.GetByTestId(E2ESelectors.CSharpTabPanel);

	public static ILocator Get_QueryResults_SqlTabPanel(this IPage page) =>
		page.GetByTestId(E2ESelectors.SqlTabPanel);

	public static async Task ExpectQueryTabCountAsync(this IPage page, int count)
	{
		await Expect(page.Get_QueryTabs()).ToHaveCountAsync(count, new() { Timeout = 5_000 });
	}

	public static async Task ClickQueryResultsTabAsync(this IPage page, string tabName)
	{
		var tab = page.Get_QueryResults_Tabs().Filter(new() { HasText = tabName });
		await Expect(tab).ToBeVisibleAsync();
		await tab.ClickAsync();
	}
}
