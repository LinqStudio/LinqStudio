using LinqStudio.App.WebServer.E2ETests.Fixtures;
using Microsoft.Playwright;
using Xunit;

namespace LinqStudio.App.WebServer.E2ETests;

[CollectionDefinition("E2E")]
public class E2ECollection : ICollectionFixture<AppServerFixture>, ICollectionFixture<PlaywrightFixture>
{
    // collection shared between tests
}

[Collection("E2E")]
public class EditorE2ETests
{
    private readonly AppServerFixture _app;
    private readonly PlaywrightFixture _pw;

    public EditorE2ETests(AppServerFixture app, PlaywrightFixture pw)
    {
        _app = app;
        _pw = pw;
    }

    [Fact(Timeout = 120_000)]
    public async Task Editor_ShowsCompletions_WhenTyping()
    {
        await using var context = await _pw.Browser!.NewContextAsync();
        var page = await context.NewPageAsync();

        // Navigate to editor with cache-busting parameter to ensure fresh page load
        await page.GotoAsync(_app.BaseUrl + $"/editor?_t={DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}");

        // Wait for Monaco container to appear
        await page.WaitForSelectorAsync("#editor-top .monaco-editor");

        // Click to focus the editor
        await page.ClickAsync("#editor-top .monaco-editor");

        // Trigger suggestions via Ctrl+Space
        await page.Keyboard.PressAsync("Control+Space");

        // Wait for suggest widget to appear
        var suggest = await page.WaitForSelectorAsync(".suggest-widget .monaco-list-row", new() { Timeout = 10000 });
        var text = await suggest.InnerTextAsync();
        Assert.False(string.IsNullOrWhiteSpace(text));

        // Ensure we have some likely completion
        var suggestions = await page.QuerySelectorAllAsync(".suggest-widget .monaco-list-row");
        var any = false;
        foreach (var s in suggestions)
        {
            var t = await s.InnerTextAsync();
            if (t.Length > 0)
            {
                any = true;
                break;
            }
        }
        Assert.True(any, "Expected at least one completion suggestion");
    }

    [Fact(Timeout = 120_000)]
    public async Task Editor_Hover_ShowsSymbolInfo()
    {
        await using var context = await _pw.Browser!.NewContextAsync();
        var page = await context.NewPageAsync();

        // Navigate to editor with cache-busting parameter
        await page.GotoAsync(_app.BaseUrl + $"/editor?_t={DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}");
        
        // Reload page to ensure fresh Blazor SignalR connection
        await page.ReloadAsync();

        // Wait for Monaco container to appear
        await page.WaitForSelectorAsync("#editor-top .monaco-editor");

        // Wait for the editor content to be rendered
        await page.WaitForSelectorAsync(".view-lines .view-line");

        // Find a Monaco token element containing "Where" and hover over it
        var whereToken = page.Locator("span").Filter(new() { HasText = "Where", HasNotText = "context" });

		// Hover over the token
		await Task.Delay(500);
		await whereToken.First.HoverAsync();

        // Wait for the hover widget to appear and get its content
        var hoverContent = await page.Locator(".monaco-hover .hover-contents").InnerTextAsync();
        
        // Verify hover content exists and contains "Where"
        Assert.False(string.IsNullOrWhiteSpace(hoverContent));
        Assert.Contains("Where", hoverContent, System.StringComparison.OrdinalIgnoreCase);
    }
}
