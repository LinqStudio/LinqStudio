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

        await page.GotoAsync(_app.BaseUrl + "/editor");

        // Wait for Monaco container to appear with increased timeout (60s)
        // Use State.Attached instead of Visible as Monaco might take time to become visible
        await page.WaitForSelectorAsync("#editor-top .monaco-editor", new() { Timeout = 60_000, State = WaitForSelectorState.Attached });

        // Give Monaco additional time to fully initialize
        await Task.Delay(2000);

        // Click to focus the editor
        await page.ClickAsync("#editor-top .monaco-editor");

        // Trigger suggestions via Ctrl+Space
        await page.Keyboard.PressAsync("Control+Space");

        // Wait for suggest widget to appear
        var suggest = await page.WaitForSelectorAsync(".suggest-widget .monaco-list-row", new() { Timeout = 10000 });
        var text = await suggest.InnerTextAsync();
        Assert.False(string.IsNullOrWhiteSpace(text));

        // ensure we have some likely completion like Where or Select
        var suggestions = await page.QuerySelectorAllAsync(".suggest-widget .monaco-list-row");
        var any = false;
        foreach (var s in suggestions)
        {
            var t = await s.InnerTextAsync();
            if (t.Contains("Where") || t.Contains("Select") || t.Contains("People"))
            {
                any = true;
                break;
            }
        }
        Assert.True(any, "Expected at least one suggestion containing Where/Select/People");
    }

    [Fact(Timeout = 120_000)]
    public async Task Editor_Hover_ShowsSymbolInfo()
    {
        await using var context = await _pw.Browser!.NewContextAsync();
        var page = await context.NewPageAsync();

        await page.GotoAsync(_app.BaseUrl + "/editor");

        // Wait for Monaco container to appear with increased timeout (60s)
        // Use State.Attached instead of Visible as Monaco might take time to become visible
        await page.WaitForSelectorAsync("#editor-top .monaco-editor", new() { Timeout = 60_000, State = WaitForSelectorState.Attached });

        // Give Monaco additional time to fully initialize
        await Task.Delay(2000);

        // Find a token span with 'Where' text and hover it
        // Monaco renders token texts in .view-lines .mtk elements; look for a span that includes 'Where'
        var token = await page.WaitForSelectorAsync(".view-lines span:has-text(Where)", new() { Timeout = 10000 });
        if (token == null)
        {
            // As a fallback, focus editor and type 'Where' to ensure it exists
            await page.ClickAsync("#editor-top .monaco-editor");
            await page.Keyboard.TypeAsync("Where");
            token = await page.WaitForSelectorAsync(".view-lines span:has-text(Where)");
        }

        await token!.HoverAsync();

        // Wait for the hover widget
        var hover = await page.WaitForSelectorAsync(".monaco-hover .hover-contents", new() { Timeout = 10000 });
        var content = await hover!.InnerTextAsync();
        Assert.False(string.IsNullOrWhiteSpace(content));
        Assert.Contains("Where", content, System.StringComparison.OrdinalIgnoreCase);
    }
}
