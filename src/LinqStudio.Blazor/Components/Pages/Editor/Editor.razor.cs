using BlazorMonaco.Editor;
using BlazorMonaco.Languages;
using LinqStudio.Blazor.Services;
using LinqStudio.Core.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Options;
using System.Text;

namespace LinqStudio.Blazor.Components.Pages.Editor;

public partial class Editor : ComponentBase, IDisposable
{
    private StandaloneCodeEditor? _editor;
    private IDisposable? _providerDisposable;
    private bool _loaded = false;

    private StandaloneEditorConstructionOptions EditorConstructionOptions(StandaloneCodeEditor ed) => new()
    {
        AutomaticLayout = true,
        Language = "csharp",
        Theme = UISettings.CurrentValue.IsDarkMode ? "vs-dark" : null,
        Value = SampleCode
    };

    private string SampleCode =>
@"using LinqStudio.TestModels;

// Type 'context.' then request completion — the Roslyn backend has an in-memory TestDbContext and Person model
public class Example
{
    public void Run(TestDbContext context)
    {
        var q = context.People
            .Where(p => p.
    }
}"
    ;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await base.OnAfterRenderAsync(firstRender);

        if (!_loaded)
        {
            _loaded = true;
            await Task.Delay(250);
            StateHasChanged();
        }
    }

    private async Task OnEditorInitialized()
    {
        if (_editor == null)
            return;

        // register a completion provider that asks the CompilerService for completions
        _providerDisposable = await MonacoProvidersService.RegisterCompletionProviderAsync(_editor, "csharp", async (modelUri, position, context) =>
        {
            try
            {
                // get text from the editor
                var text = await _editor.GetValue();

                // compute 0-based offset from Monaco's 1-based line/column
                var cursorOffset = OffsetFromPosition(text, position);

                // create a compiler service (hard-coded model inside factory)
                var compiler = await CompilerServiceFactory.CreateAsync();

                var completions = await compiler.GetCompletionsAsync(text, cursorOffset);
                if (completions == null || completions.Count == 0)
                    return null;

                var items = completions.Select(c => new CompletionItem { InsertText = c }).ToList();

                return new CompletionList
                {
                    Suggestions = items,
                    Incomplete = false
                };
            }
            catch
            {
                return null;
            }
        });
    }

    private static int OffsetFromPosition(string text, BlazorMonaco.Position position)
    {
        if (position.LineNumber <= 1 && position.Column <= 1)
            return 0;

        // Normalize newlines to \n for consistent offsets
        text = text.Replace("\r\n", "\n");
        var lines = text.Split('\n');

        var offset = 0;
        var targetLineIndex = Math.Max(0, position.LineNumber - 1);

        for (var i = 0; i < targetLineIndex && i < lines.Length; i++)
            offset += lines[i].Length + 1; // include newline

        var columnIndex = Math.Max(0, position.Column - 1);
        if (targetLineIndex < lines.Length)
            offset += Math.Min(columnIndex, lines[targetLineIndex].Length);

        return offset;
    }

    public void Dispose()
    {
        _providerDisposable?.Dispose();
        GC.SuppressFinalize(this);
    }
}
