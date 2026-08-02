using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace LinqStudio.App.WebServer.E2ETests.Helpers;

public static class ResultGridExtensions
{
	public static ILocator Get_QueryResults_ResultContainer(this IPage page) =>
		E2ETestHelpers.GetActivePanel(page).GetByTestId(E2ESelectors.QueryResultContainer);

	public static ILocator Get_QueryResults_ResultContainer(this ILocator panel) =>
		panel.GetByTestId(E2ESelectors.QueryResultContainer);

	public static ILocator Get_QueryResults_Table(this IPage page) =>
		page.Get_QueryResults_ResultContainer().Locator(E2ESelectors.MudTableRoot);

	public static ILocator Get_QueryResults_Table(this ILocator panel) =>
		panel.Get_QueryResults_ResultContainer().Locator(E2ESelectors.MudTableRoot);

	public static ILocator Get_QueryResults_TableFromContainer(this ILocator resultContainer) =>
		resultContainer.Locator(E2ESelectors.MudTableRoot);

	public static ILocator Get_QueryResults_SelectionCount(this IPage page) =>
		E2ETestHelpers.GetActivePanel(page).GetByTestId(E2ESelectors.SelectionCount);

	public static ILocator Get_QueryResults_SelectionCount(this ILocator panel) =>
		panel.GetByTestId(E2ESelectors.SelectionCount);

	public static ILocator Get_QueryResults_GridContainer(this IPage page) =>
		page.Get_QueryResults_ResultContainer().Locator(E2ESelectors.QueryResultGridContainer);

	public static ILocator Get_QueryResults_SelectedRows(this IPage page) =>
		page.Get_QueryResults_ResultContainer().Locator(E2ESelectors.RowSelected);

	public static ILocator Get_QueryResults_SelectedRows(this ILocator resultContainer) =>
		resultContainer.Locator(E2ESelectors.RowSelected);

	public static ILocator Get_QueryResults_Cell(this ILocator table, int rowIndex, int columnIndex) =>
		table.GetByRole(AriaRole.Row).Nth(rowIndex + 1).GetByRole(AriaRole.Cell).Nth(columnIndex);

	public static ILocator Get_QueryResults_Header(this ILocator table, string columnName) =>
		table.GetByRole(AriaRole.Columnheader).Filter(new() { HasText = columnName });

	public static ILocator Get_QueryResults_ExecutionStatus(this IPage page, string text) =>
		page.Get_QueryResults_ResultContainer().Locator($"text={text}");

	public static ILocator Get_QueryResults_ResultOrError(this ILocator resultContainer) =>
		resultContainer.Locator(E2ESelectors.QueryResultOrError);

	public static ILocator Get_QueryResults_ErrorAlert(this ILocator resultContainer) =>
		resultContainer.Locator(E2ESelectors.MudAlert);

	public static ILocator Get_QueryResults_ExecutingIndicator(this ILocator resultContainer) =>
		resultContainer.Locator(E2ESelectors.QueryExecutingText);

	public static ILocator Get_QueryResults_ProgressSpinner(this ILocator resultContainer) =>
		resultContainer.Locator(E2ESelectors.MudProgressCircular);

	public static ILocator Get_QueryResults_RowCount(this ILocator resultContainer) =>
		resultContainer.Locator("text=/\\d+ rows? ·/");

	public static async Task ExpectQueryResultsVisibleAsync(this IPage page)
	{
		await Expect(page.Get_QueryResults_Table()).ToBeVisibleAsync(new() { Timeout = 10_000 });
	}

	public static async Task ExpectQueryResultOrErrorVisibleAsync(this IPage page)
	{
		var container = page.Get_QueryResults_ResultContainer();
		await Expect(container.Get_QueryResults_ResultOrError())
			.ToBeVisibleAsync(new() { Timeout = 10_000 });
	}

	public static async Task ExpectQueryResultErrorVisibleAsync(this IPage page)
	{
		await Expect(page.Get_QueryResults_ResultContainer().Get_QueryResults_ErrorAlert())
			.ToBeVisibleAsync(new() { Timeout = 10_000 });
	}

	public static async Task ExpectQueryExecutingAsync(this IPage page)
	{
		var resultContainer = page.Get_QueryResults_ResultContainer();
		await Expect(resultContainer.Get_QueryResults_ExecutingIndicator())
			.ToBeVisibleAsync(new() { Timeout = 5_000 });
		await Expect(resultContainer.Get_QueryResults_ProgressSpinner()).ToBeVisibleAsync();
	}
}
