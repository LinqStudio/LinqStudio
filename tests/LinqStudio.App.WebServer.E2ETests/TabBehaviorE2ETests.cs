using LinqStudio.App.WebServer.E2ETests.Fixtures;
using LinqStudio.App.WebServer.E2ETests.Helpers;
using Microsoft.Playwright;
using Xunit;
using static Microsoft.Playwright.Assertions;

namespace LinqStudio.App.WebServer.E2ETests;

/// <summary>
/// E2E tests for MudTabs KeepPanelsAlive behavior: tab switching preserves state,
/// each Monaco editor is independent, tabs can be closed cleanly.
/// </summary>
[Collection("E2E")]
public class TabBehaviorE2ETests(AppServerFixture app, PlaywrightFixture pw)
{
	private readonly AppServerFixture _app = app;
	private readonly PlaywrightFixture _pw = pw;

	[Fact(Timeout = 120_000)]
	public async Task TabSwitch_PreservesQueryResult_AcrossTabActivations()
	{
		// Contract: KeepPanelsAlive preserves result DOM when switching back to a previously executed tab.
		Assert.NotNull(_pw.Browser);
		await using var context = await _pw.Browser.NewContextAsync();
		var page = await context.NewPageAsync();

		// Setup Tab 1
		await E2ETestHelpers.SetupEditorAsync(page, _app);

		// Execute a query on Tab 1 and wait for results
		_app.MockQueryExecutionService.SetNextResult(E2ETestHelpers.CreateMultiColumnResult(rows: 3));
		await E2ETestHelpers.GetActivePanel(page).GetByTestId("execute-query-btn").ClickAsync();
		await Expect(
			E2ETestHelpers.GetActivePanel(page)
				.GetByTestId("query-result-container")
				.Locator(".mud-table-root")
		).ToBeVisibleAsync(new() { Timeout = 10_000 });

		// Create Tab 2 and execute a different query
		await E2ETestHelpers.CreateAdditionalTabAsync(page, _app);
		_app.MockQueryExecutionService.SetNextResult(E2ETestHelpers.CreateMultiColumnResult(rows: 5));
		await E2ETestHelpers.GetActivePanel(page).GetByTestId("execute-query-btn").ClickAsync();
		await Expect(
			E2ETestHelpers.GetActivePanel(page)
				.GetByTestId("query-result-container")
				.Locator(".mud-table-root")
		).ToBeVisibleAsync(new() { Timeout = 10_000 });

		// Switch back to Tab 1 — results must still be visible (KeepPanelsAlive contract)
		await E2ETestHelpers.ClickTabAtIndexAsync(page, 0);
		await Expect(
			E2ETestHelpers.GetActivePanel(page)
				.GetByTestId("query-result-container")
				.Locator(".mud-table-root")
		).ToBeVisibleAsync(new() { Timeout = 5_000 });

		// Switch back to Tab 2 — its results must also be preserved
		await E2ETestHelpers.ClickTabAtIndexAsync(page, 1);
		await Expect(
			E2ETestHelpers.GetActivePanel(page)
				.GetByTestId("query-result-container")
				.Locator(".mud-table-root")
		).ToBeVisibleAsync(new() { Timeout = 5_000 });
	}

	[Fact(Timeout = 90_000)]
	public async Task TabSwitch_PreservesEditorContent_AcrossTabActivations()
	{
		// Contract: Each Monaco instance keeps its content when tabs are switched and switched back.
		Assert.NotNull(_pw.Browser);
		await using var context = await _pw.Browser.NewContextAsync();
		var page = await context.NewPageAsync();

		// Setup Tab 1 with unique content
		await E2ETestHelpers.SetupEditorAsync(page, _app);
		await E2ETestHelpers.ClearAndWriteQueryAsync(page, "TABTEST_ALPHA");

		// Create Tab 2 with different unique content
		await E2ETestHelpers.CreateAdditionalTabAsync(page, _app);
		await E2ETestHelpers.ClearAndWriteQueryAsync(page, "TABTEST_BETA");

		// Switch to Tab 1 — Monaco must still show Tab 1's text
		await E2ETestHelpers.ClickTabAtIndexAsync(page, 0);
		await Expect(
			E2ETestHelpers.GetActivePanel(page).Locator(".view-lines")
		).ToContainTextAsync("TABTEST_ALPHA", new() { Timeout = 5_000 });

		// Switch to Tab 2 — Monaco must still show Tab 2's text
		await E2ETestHelpers.ClickTabAtIndexAsync(page, 1);
		await Expect(
			E2ETestHelpers.GetActivePanel(page).Locator(".view-lines")
		).ToContainTextAsync("TABTEST_BETA", new() { Timeout = 5_000 });
	}

	[Fact(Timeout = 120_000)]
	public async Task MultiTab_MonacoEditors_AreIndependent()
	{
		// Contract: Three open tabs each maintain their own Monaco model with no cross-contamination.
		Assert.NotNull(_pw.Browser);
		await using var context = await _pw.Browser.NewContextAsync();
		var page = await context.NewPageAsync();

		// Create 3 tabs with distinct editor content
		await E2ETestHelpers.SetupEditorAsync(page, _app);
		await E2ETestHelpers.ClearAndWriteQueryAsync(page, "EDITOR_ONE");

		await E2ETestHelpers.CreateAdditionalTabAsync(page, _app);
		await E2ETestHelpers.ClearAndWriteQueryAsync(page, "EDITOR_TWO");

		await E2ETestHelpers.CreateAdditionalTabAsync(page, _app);
		await E2ETestHelpers.ClearAndWriteQueryAsync(page, "EDITOR_THREE");

		// Cycle through all 3 tabs and verify each shows only its own content
		await E2ETestHelpers.ClickTabAtIndexAsync(page, 0);
		await Expect(
			E2ETestHelpers.GetActivePanel(page).Locator(".view-lines")
		).ToContainTextAsync("EDITOR_ONE", new() { Timeout = 5_000 });

		await E2ETestHelpers.ClickTabAtIndexAsync(page, 1);
		await Expect(
			E2ETestHelpers.GetActivePanel(page).Locator(".view-lines")
		).ToContainTextAsync("EDITOR_TWO", new() { Timeout = 5_000 });

		await E2ETestHelpers.ClickTabAtIndexAsync(page, 2);
		await Expect(
			E2ETestHelpers.GetActivePanel(page).Locator(".view-lines")
		).ToContainTextAsync("EDITOR_THREE", new() { Timeout = 5_000 });
	}

	[Fact(Timeout = 120_000)]
	public async Task TabClose_RemovesTab_AndRemainingTabsWork()
	{
		// Contract: Closing the middle tab reduces the tab strip to 2, remaining tabs are functional.
		Assert.NotNull(_pw.Browser);
		await using var context = await _pw.Browser.NewContextAsync();
		var page = await context.NewPageAsync();

		// Create 3 tabs
		await E2ETestHelpers.SetupEditorAsync(page, _app);
		await E2ETestHelpers.CreateAdditionalTabAsync(page, _app);
		await E2ETestHelpers.CreateAdditionalTabAsync(page, _app);

		// Verify 3 tabs exist — one query-execution-bar per open query panel (works with KeepPanelsAlive)
		// Avoids counting .mud-tab which also includes inner Results|C#|SQL tab buttons
		var queryPanelCount = page.GetByTestId("query-execution-bar");
		await Expect(queryPanelCount).ToHaveCountAsync(3, new() { Timeout = 5_000 });

		// Switch to the middle tab, type content to mark it as having unsaved changes
		await E2ETestHelpers.ClickTabAtIndexAsync(page, 1);
		await E2ETestHelpers.ClearAndWriteQueryAsync(page, "UNSAVED_CONTENT");

		var closeBtn = E2ETestHelpers.GetActivePanel(page).GetByTestId("query-close-btn");
		await Expect(closeBtn).ToBeVisibleAsync();
		await closeBtn.ClickAsync();

		// The tab has unsaved content — the unsaved-changes dialog must appear
		var dialog = page.GetByTestId("unsaved-changes-dialog");
		await Expect(dialog).ToBeVisibleAsync(new() { Timeout = 3_000 });
		var confirmBtn = page.GetByTestId("unsaved-changes-confirm-btn");
		await confirmBtn.ClickAsync();

		// Wait for the tab removal to complete (URL changes on close)
		await page.WaitForURLAsync($"{_app.BaseUrl}editor/**", new() { Timeout = 5_000 });

		// Verify only 2 tabs remain
		await Expect(queryPanelCount).ToHaveCountAsync(2, new() { Timeout = 5_000 });

		// Verify both remaining tabs can be focused and their editors are visible
		await E2ETestHelpers.ClickTabAtIndexAsync(page, 0);
		await Expect(
			E2ETestHelpers.GetActivePanel(page).GetByTestId("monaco-editor-container")
		).ToBeVisibleAsync(new() { Timeout = 5_000 });

		await E2ETestHelpers.ClickTabAtIndexAsync(page, 1);
		await Expect(
			E2ETestHelpers.GetActivePanel(page).GetByTestId("monaco-editor-container")
		).ToBeVisibleAsync(new() { Timeout = 5_000 });
	}

	[Fact(Timeout = 90_000)]
	public async Task TabActivation_MonacoEditor_IsVisibleAfterTabSwitch()
	{
		// Contract: Monaco editor must have non-zero dimensions after every tab activation.
		// Validates that OnTabActivatedAsync correctly triggers Monaco layout().
		Assert.NotNull(_pw.Browser);
		await using var context = await _pw.Browser.NewContextAsync();
		var page = await context.NewPageAsync();

		// Setup 2 tabs
		await E2ETestHelpers.SetupEditorAsync(page, _app);
		await E2ETestHelpers.CreateAdditionalTabAsync(page, _app);

		// Switch between the two tabs 3 times and verify Monaco has height > 0 each time
		int[] switchSequence = [0, 1, 0];
		for (int i = 0; i < switchSequence.Length; i++)
		{
			await E2ETestHelpers.ClickTabAtIndexAsync(page, switchSequence[i]);

			var editorContainer = E2ETestHelpers.GetActivePanel(page)
				.GetByTestId("monaco-editor-container");
			await Expect(editorContainer).ToBeVisibleAsync(new() { Timeout = 5_000 });

			var box = await editorContainer.BoundingBoxAsync();
			Assert.NotNull(box);
			Assert.True(
				box.Height > 0,
				$"Monaco editor height should be > 0 after switching to tab {switchSequence[i]} (switch #{i + 1}), but got {box.Height}"
			);
		}
	}
}
