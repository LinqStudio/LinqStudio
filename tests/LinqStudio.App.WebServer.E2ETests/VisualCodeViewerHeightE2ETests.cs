using LinqStudio.Abstractions.Models;
using LinqStudio.App.WebServer.E2ETests.Fixtures;
using LinqStudio.App.WebServer.E2ETests.Helpers;
using Microsoft.Playwright;
using Xunit;
using static Microsoft.Playwright.Assertions;

namespace LinqStudio.App.WebServer.E2ETests;

/// <summary>
/// Visual E2E tests verifying that the Monaco editor fills the full height
/// of the C# and SQL result tabs in QueryEditorPanel.
/// 
/// Bug: Monaco editor in C#/SQL tabs had zero or tiny height.
/// Fix: Changed .code-viewer to ::deep .code-viewer in QueryEditorPanel.razor.css
///      so Blazor's scoped CSS selector reaches the StandaloneCodeEditor child component.
/// </summary>
[Collection("E2E")]
public class VisualCodeViewerHeightE2ETests(AppServerFixture app, PlaywrightFixture pw)
{
    private readonly AppServerFixture _app = app;
    private readonly PlaywrightFixture _pw = pw;

    /// <summary>
    /// Verifies that the Monaco editor in the C# tab fills the tab panel vertically
    /// (has meaningful height, not zero or tiny).
    /// </summary>
    [Fact(Timeout = 90_000)]
    public async Task CSharpTab_MonacoEditor_FillsHeightVertically()
    {
        Assert.NotNull(_pw.Browser);
        await using var context = await _pw.Browser!.NewContextAsync(new()
        {
            ViewportSize = new ViewportSize { Width = 1400, Height = 900 }
        });
        var page = await context.NewPageAsync();

        // Setup: create project + query, configure mock result with C# output
        await E2ETestHelpers.SetupEditorAsync(page, _app);

        _app.MockQueryExecutionService.SetNextResult(new QueryExecutionResult
        {
            ColumnNames = ["Id", "Name"],
            Rows = [new Dictionary<string, object?> { ["Id"] = 1, ["Name"] = "Alice" }],
            GeneratedCSharp = "var q = context.People.Take(5);",
            GeneratedSql = "SELECT TOP 5 [p].[Id] FROM [People] AS [p]",
            Elapsed = TimeSpan.FromMilliseconds(42)
        });

        // Execute the query
        var executeBtn = page.GetByTestId("execute-query-btn");
        await Expect(executeBtn).ToBeVisibleAsync();
        await executeBtn.ClickAsync();

        // Wait for execution to complete (result or error)
        var resultContainer = page.GetByTestId("query-result-container");
        await Expect(resultContainer.Locator(".mud-table, .mud-alert")).ToBeVisibleAsync(new() { Timeout = 15000 });

        // Click the C# tab
        var csharpTab = page.Locator("[data-testid='results-tabs'] .mud-tab").Filter(new() { HasText = "C#" });
        await Expect(csharpTab).ToBeVisibleAsync();
        await csharpTab.ClickAsync();

        // Wait for Monaco editor to appear in the C# tab panel
        var csharpPanel = page.GetByTestId("csharp-tab-panel");
        var monacoEditor = csharpPanel.Locator(".monaco-editor").First;
        await Expect(monacoEditor).ToBeVisibleAsync(new() { Timeout = 10000 });

        // Take a screenshot for visual evidence
        await page.ScreenshotAsync(new() { Path = "screenshot-csharp-tab.png" });

        // Measure height — must be substantially taller than the minimum 200px we set
        var editorBox = await monacoEditor.BoundingBoxAsync();
        Assert.NotNull(editorBox);
        var height = editorBox.Height;

        // The results section should give the editor at least 150px.
        // Before the fix the height was 0 (element not matched by scoped CSS).
        Assert.True(height >= 150,
            $"Monaco editor in C# tab should be at least 150px tall, but was {height}px. " +
            $"This indicates the ::deep .code-viewer CSS fix may not be applied.");
    }

    /// <summary>
    /// Verifies that the Monaco editor in the SQL tab fills the tab panel vertically.
    /// </summary>
    [Fact(Timeout = 90_000)]
    public async Task SqlTab_MonacoEditor_FillsHeightVertically()
    {
        Assert.NotNull(_pw.Browser);
        await using var context = await _pw.Browser!.NewContextAsync(new()
        {
            ViewportSize = new ViewportSize { Width = 1400, Height = 900 }
        });
        var page = await context.NewPageAsync();

        await E2ETestHelpers.SetupEditorAsync(page, _app);

        _app.MockQueryExecutionService.SetNextResult(new QueryExecutionResult
        {
            ColumnNames = ["Id", "Name"],
            Rows = [new Dictionary<string, object?> { ["Id"] = 1, ["Name"] = "Alice" }],
            GeneratedCSharp = "var q = context.People.Take(5);",
            GeneratedSql = "SELECT TOP 5 [p].[Id] FROM [People] AS [p]",
            Elapsed = TimeSpan.FromMilliseconds(42)
        });

        var executeBtn = page.GetByTestId("execute-query-btn");
        await Expect(executeBtn).ToBeVisibleAsync();
        await executeBtn.ClickAsync();

        var resultContainer = page.GetByTestId("query-result-container");
        await Expect(resultContainer.Locator(".mud-table, .mud-alert")).ToBeVisibleAsync(new() { Timeout = 15000 });

        // Click the SQL tab
        var sqlTab = page.Locator("[data-testid='results-tabs'] .mud-tab").Filter(new() { HasText = "SQL" });
        await Expect(sqlTab).ToBeVisibleAsync();
        await sqlTab.ClickAsync();

        // Wait for Monaco editor to appear in the SQL tab panel
        var sqlPanel = page.GetByTestId("sql-tab-panel");
        var monacoEditor = sqlPanel.Locator(".monaco-editor").First;
        await Expect(monacoEditor).ToBeVisibleAsync(new() { Timeout = 10000 });

        // Take a screenshot for visual evidence
        await page.ScreenshotAsync(new() { Path = "screenshot-sql-tab.png" });

        var editorBox = await monacoEditor.BoundingBoxAsync();
        Assert.NotNull(editorBox);
        var height = editorBox.Height;

        Assert.True(height >= 150,
            $"Monaco editor in SQL tab should be at least 150px tall, but was {height}px. " +
            $"This indicates the ::deep .code-viewer CSS fix may not be applied.");
    }
}
