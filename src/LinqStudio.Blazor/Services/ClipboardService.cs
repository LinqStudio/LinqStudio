using Microsoft.JSInterop;

namespace LinqStudio.Blazor.Services;

public interface IClipboardService
{
	Task<bool> CopyToClipboardAsync(string text);
}

internal sealed class ClipboardService(IJSRuntime jsRuntime) : IClipboardService
{
	public async Task<bool> CopyToClipboardAsync(string text)
	{
		try
		{
			return await jsRuntime.InvokeAsync<bool>("copyToClipboard", text);
		}
		catch
		{
			return false;
		}
	}
}
