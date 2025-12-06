using BlazorMonaco;
using BlazorMonaco.Editor;
using BlazorMonaco.Languages;
using LinqStudio.Blazor.Services;
using LinqStudio.Core.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.CodeAnalysis.Tags;
using Microsoft.Extensions.Options;
using System.Text;

namespace LinqStudio.Blazor.Components.Pages.Editor;

public partial class Editor : ComponentBase, IDisposable
{
    private StandaloneCodeEditor? _editor;
    private IDisposable? _providerDisposable;
    private IDisposable? _hoverProviderDisposable;

    private StandaloneEditorConstructionOptions EditorConstructionOptions(StandaloneCodeEditor ed) => new()
    {
        AutomaticLayout = true,
        Language = "csharp",
        Theme = UISettings.CurrentValue.IsDarkMode ? "vs-dark" : null,
        Hover = new() { Enabled = true },
        Value = SampleCode
    };

    private string SampleCode = "context.People.Where(p => p.";

    private async Task OnEditorInitialized()
    {
        if (_editor == null)
            return;

        await Task.Delay(250); // slight delay to ensure Monaco is ready

        // register a completion provider that asks the CompilerService for completions
        _providerDisposable = await MonacoProvidersService.RegisterCompletionProviderAsync(_editor, async (modelUri, position, context) =>
        {
            try
            {
                // get text from the editor
                var text = await _editor.GetValue();

                var model = await _editor.GetModel();

                var cursorOffset = await model.GetOffsetAt(position);

                // create a compiler service (hard-coded model inside factory)
                var compiler = await CompilerServiceFactory.CreateAsync();

                var completions = await compiler.GetCompletionsAsync(text, cursorOffset);
                if (completions == null || completions.Count == 0)
                    return null;

                var items = completions.Select(c => new CompletionItem
                {
                    InsertText = GetInsertText(c.Item),
                    LabelAsString = c.Item.DisplayTextPrefix + c.Item.DisplayText + c.Item.DisplayTextSuffix,
                    FilterText = c.Item.FilterText,
                    Detail = c.Item.InlineDescription,
                    Kind = MapCompletionItemKind(c.Item.Tags),
                    DocumentationAsString = c.Description,
                }).ToList();

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

        // register a hover provider that asks the CompilerService for richer hover information
        _hoverProviderDisposable = await MonacoProvidersService.RegisterHoverProviderAsync(_editor, async (uri, position, context) =>
        {
            try
            {
                var text = await _editor.GetValue();
                var model = await _editor.GetModel();
                if (model == null)
                    return null;

                var cursorOffset = await model.GetOffsetAt(position);

                var compiler = await CompilerServiceFactory.CreateAsync();
                var hover = await compiler.GetHoverAsync(text, cursorOffset);
                if (hover == null)
                    return null;

                // map offsets back to Monaco positions
                var startPos = await model.GetPositionAt(hover.StartOffset);
                var endPos = await model.GetPositionAt(hover.StartOffset + hover.Length);

                return new Hover
                {
                    Contents = [ new MarkdownString { Value = hover.Markdown ?? string.Empty, SupportThemeIcons = false } ],
                    Range = new BlazorMonaco.Range
                    {
                        StartLineNumber = startPos.LineNumber,
                        EndLineNumber = endPos.LineNumber,
                        StartColumn = startPos.Column,
                        EndColumn = endPos.Column
                    }
                };
            }
            catch
            {
                return null;
            }
        });
    }

    private string GetInsertText(Microsoft.CodeAnalysis.Completion.CompletionItem item)
    {
        var text = item.Properties.TryGetValue("InsertionText", out var v) ? v : item.DisplayText;

        if(item.Properties.TryGetValue("ShouldProvideParenthesisCompletion", out var s) && s.Equals("true", StringComparison.OrdinalIgnoreCase))
        {
            text += "(";
        }

        return text;
    }

    private CompletionItemKind MapCompletionItemKind(IEnumerable<string> tags)
    {
        if (tags.Contains(WellKnownTags.Property))
            return CompletionItemKind.Property;

        if (tags.Contains(WellKnownTags.Method) || tags.Contains(WellKnownTags.ExtensionMethod))
            return CompletionItemKind.Method;

        if (tags.Contains(WellKnownTags.Field))
            return CompletionItemKind.Field;

        if (tags.Contains(WellKnownTags.Class))
            return CompletionItemKind.Class;

        return CompletionItemKind.Text;
    }

    public void Dispose()
    {
        _providerDisposable?.Dispose();
        _hoverProviderDisposable?.Dispose();
        GC.SuppressFinalize(this);
    }
}
