using BlazorMonaco.Editor;
using BlazorMonaco.Languages;
using Microsoft.JSInterop;
using System.Collections.Concurrent;

namespace LinqStudio.Blazor.Services;

/// <summary>
/// Used to keep track of which Monaco providers are already registered and prevent accumulating registrations.
/// Since Monaco track providers globally we can't easily differentiate between different Blazor components hosting multiple Monaco editors.
/// This is where this service comes handy. A component can simply call registration methods any time they need to and it'll keep track of globally registered providers and redistributing events to the right delegates.
/// </summary>
internal class MonacoProvidersService(IJSRuntime jSRuntime)
{
    private readonly IJSRuntime _jSRuntime = jSRuntime;

    private readonly ConcurrentDictionary<string, HoverProvider.ProvideDelegate> _hoverProviders = [];
    private readonly ConcurrentDictionary<string, CompletionItemProvider.ProvideDelegate> _completionProviders = [];

    private bool _registered = false;

    internal async Task<IDisposable> RegisterHoverProviderAsync(StandaloneCodeEditor editor, HoverProvider.ProvideDelegate provideDelegate)
    {
        await RegisterAll();

        var model = await editor.GetModel();
        _hoverProviders[model.Uri] = provideDelegate;

        return new UnregisterProviderDisposable(this, model.Uri);
    }

    internal async Task<IDisposable> RegisterCompletionProviderAsync(StandaloneCodeEditor editor, CompletionItemProvider.ProvideDelegate provideDelegate)
    {
        await RegisterAll();

        var model = await editor.GetModel();
        _completionProviders[model.Uri] = provideDelegate;

        return new UnregisterProviderDisposable(this, model.Uri);
    }

    private async Task RegisterAll()
    {
        if (_registered)
            return;

        // Sometimes we initialize the library in the frontend after the backend is ready.
        // I'm not exactly sure why but we can loop the first "Register" until it actually works, with a bit of delay between each attempts
        for (int i = 0; ; ++i)
        {
            try
            {
                await BlazorMonaco.Languages.Global.RegisterHoverProviderAsync(_jSRuntime, "csharp", ProvideDelegate);
                break;
            }
            catch (Exception ex) when (ex.Message.Contains("monaco is not defined"))
            {
                if (i == 5)
                    throw;

                await Task.Delay(250);
            }
        }


        await BlazorMonaco.Languages.Global.RegisterHoverProviderAsync(_jSRuntime, "json", ProvideDelegate);
        await BlazorMonaco.Languages.Global.RegisterCompletionItemProvider(_jSRuntime, "csharp", ProvideCompletionDelegate);
        _registered = true;
    }

    private Task<Hover?> ProvideDelegate(string modelUri, BlazorMonaco.Position position, HoverContext context)
    {
        if (!_hoverProviders.TryGetValue(modelUri, out var provideDelegate))
            return Task.FromResult<Hover?>(null);

        return provideDelegate(modelUri, position, context);
    }

    private Task<CompletionList?> ProvideCompletionDelegate(string modelUri, BlazorMonaco.Position position, CompletionContext context)
    {
        if (!_completionProviders.TryGetValue(modelUri, out var provideDelegate))
            return Task.FromResult<CompletionList?>(null);

        return provideDelegate(modelUri, position, context);
    }

    private void UnregisterHoverProvider(string modelUri)
    {
        _hoverProviders.TryRemove(modelUri, out var _);
        _completionProviders.TryRemove(modelUri, out var _);
    }

    private class UnregisterProviderDisposable(MonacoProvidersService monacoProvidersService, string uri) : IDisposable
    {
        private readonly MonacoProvidersService _monacoProvidersService = monacoProvidersService;
        private readonly string _uri = uri;

        public void Dispose()
        {
            _monacoProvidersService.UnregisterHoverProvider(_uri);
        }
    }
}
