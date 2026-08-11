using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace LinqStudio.Blazor.Components;

public partial class ResizableSplitter : ComponentBase, IDisposable, IAsyncDisposable
{
	[Inject] private IJSRuntime JSRuntime { get; set; } = null!;

	[Parameter, EditorRequired] public string SplitterId { get; set; } = string.Empty;
	[Parameter, EditorRequired] public string AccessibleLabel { get; set; } = string.Empty;
	[Parameter] public ResizeSplitterOrientation Orientation { get; set; } = ResizeSplitterOrientation.Horizontal;
	[Parameter] public string? FirstPanelId { get; set; }
	[Parameter] public string? SecondPanelId { get; set; }
	[Parameter] public string? TargetId { get; set; }
	[Parameter] public string? TestId { get; set; }

	private bool _initialized;
	private bool _disposed;

	protected override async Task OnAfterRenderAsync(bool firstRender)
	{
		await base.OnAfterRenderAsync(firstRender);

		if (!firstRender || _disposed)
			return;

		_initialized = true;
		var initialized = await JSRuntime.InvokeAsync<bool>(
			"initResizableSplitter",
			SplitterId,
			Orientation == ResizeSplitterOrientation.Horizontal ? "horizontal" : "vertical",
			FirstPanelId,
			SecondPanelId,
			TargetId);

		if (!initialized)
			_initialized = false;
	}

	private string GetSplitterClass()
		=> Orientation == ResizeSplitterOrientation.Horizontal
			? "resize-splitter resize-splitter-horizontal"
			: "resize-splitter resize-splitter-vertical";

	private string GetAriaOrientation()
		=> Orientation == ResizeSplitterOrientation.Horizontal ? "horizontal" : "vertical";

	public void Dispose()
	{
		if (_disposed)
			return;

		_disposed = true;
		GC.SuppressFinalize(this);
	}

	public async ValueTask DisposeAsync()
	{
		if (_disposed)
			return;

		_disposed = true;

		if (_initialized)
		{
			try
			{
				await JSRuntime.InvokeVoidAsync("disposeResizableSplitter", SplitterId);
			}
			catch (JSDisconnectedException)
			{
			}
		}

		GC.SuppressFinalize(this);
	}
}

public enum ResizeSplitterOrientation
{
	Horizontal,
	Vertical,
}
