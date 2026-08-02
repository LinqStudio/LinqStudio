using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace LinqStudio.App.WebServer.E2ETests.Helpers;

public static class QueryEditorExtensions
{
	public static ILocator Get_QueryExecution_ExecuteButton(this IPage page) =>
		E2ETestHelpers.GetActivePanel(page).GetByTestId(E2ESelectors.ExecuteQueryButton);

	public static ILocator Get_QueryExecution_ExecuteButton(this ILocator panel) =>
		panel.GetByTestId(E2ESelectors.ExecuteQueryButton);

	public static ILocator Get_QueryExecution_StopButton(this IPage page) =>
		E2ETestHelpers.GetActivePanel(page).GetByTestId(E2ESelectors.StopQueryButton);

	public static ILocator Get_QueryExecution_StopButton(this ILocator panel) =>
		panel.GetByTestId(E2ESelectors.StopQueryButton);

	public static ILocator Get_QueryExecution_TimeoutSelect(this IPage page) =>
		E2ETestHelpers.GetActivePanel(page).GetByTestId(E2ESelectors.TimeoutSelect);

	public static ILocator Get_QueryExecution_TimeoutSelect(this ILocator panel) =>
		panel.GetByTestId(E2ESelectors.TimeoutSelect);

	public static ILocator Get_QueryExecution_TimeoutOptions(this IPage page) =>
		page.Locator(E2ESelectors.QueryTimeoutOption);

	public static ILocator Get_QueryEditor_CloseButton(this IPage page) =>
		E2ETestHelpers.GetActivePanel(page).GetByTestId(E2ESelectors.QueryCloseButton);

	public static ILocator Get_QueryEditor_CloseButton(this ILocator panel) =>
		panel.GetByTestId(E2ESelectors.QueryCloseButton);

	public static ILocator Get_QueryEditor_UnsavedIndicator(this IPage page) =>
		E2ETestHelpers.GetActivePanel(page).GetByTestId(E2ESelectors.QueryUnsavedIndicator);

	public static ILocator Get_QueryEditor_MonacoContainer(this IPage page) =>
		E2ETestHelpers.GetActivePanel(page).GetByTestId(E2ESelectors.MonacoEditorContainer);

	public static ILocator Get_QueryEditor_MonacoContainer(this ILocator panel) =>
		panel.GetByTestId(E2ESelectors.MonacoEditorContainer);

	public static ILocator Get_QueryEditor_ViewLines(this IPage page) =>
		E2ETestHelpers.GetActivePanel(page).Locator(E2ESelectors.MonacoViewLines);

	public static ILocator Get_QueryEditor_ViewLine(this IPage page) =>
		E2ETestHelpers.GetActivePanel(page).Locator(E2ESelectors.MonacoViewLine);

	public static ILocator Get_QueryEditor_SuggestRows(this IPage page, bool visibleOnly = true) =>
		page.Locator(visibleOnly ? E2ESelectors.MonacoSuggestRows : E2ESelectors.MonacoSuggestRowsAny);

	public static ILocator Get_QueryEditor_HoverContent(this IPage page) =>
		page.Locator(E2ESelectors.MonacoHoverContent);

	public static ILocator Get_QueryEditor_Splitter(this IPage page) =>
		page.GetByTestId(E2ESelectors.EditorResultsSplitter);

	public static ILocator Get_QueryEditor_EditorPage(this IPage page) =>
		page.GetByTestId(E2ESelectors.EditorPage);

	public static ILocator Get_QueryEditor_NoQueryAlert(this IPage page) =>
		page.GetByTestId(E2ESelectors.NoQueryAlert);

	public static async Task ExpectQueryExecutionIdleAsync(this IPage page)
	{
		await Expect(page.Get_QueryExecution_ExecuteButton()).ToBeVisibleAsync();
		await Expect(page.Get_QueryExecution_StopButton()).Not.ToBeVisibleAsync();
	}

	public static async Task ExpectNoQueryIsOpenAsync(this IPage page) =>
		await Expect(page.GetByTestId(E2ESelectors.NoQueryAlert)).ToBeVisibleAsync();

	public static async Task ExpectQueryResultContainerVisibleAsync(this IPage page) =>
		await Expect(page.Get_QueryResults_ResultContainer()).ToBeVisibleAsync();

	public static async Task ExpectQueryUnsavedAsync(this IPage page, string text = "Unsaved")
	{
		var indicator = page.Get_QueryEditor_UnsavedIndicator();
		await Expect(indicator).ToBeVisibleAsync();
		await Expect(indicator).ToContainTextAsync(text);
	}
}
