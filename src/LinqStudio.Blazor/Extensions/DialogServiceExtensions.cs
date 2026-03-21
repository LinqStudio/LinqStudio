using LinqStudio.Blazor.Components.Dialogs;
using MudBlazor;

namespace LinqStudio.Blazor.Extensions;

/// <summary>
/// Extension methods for <see cref="IDialogService"/> that surface shared MudBlazor dialogs
/// used across multiple pages and components in the IDE.
/// </summary>
public static class DialogServiceExtensions
{
	/// <summary>
	/// Shows the shared "Unsaved Changes" confirmation dialog and awaits the user's choice.
	/// </summary>
	/// <param name="dialogService">The MudBlazor dialog service to open the dialog with.</param>
	/// <param name="message">
	/// The body text displayed inside the dialog.
	/// Defaults to a generic unsaved-changes prompt when omitted.
	/// </param>
	/// <returns>
	/// <see langword="true"/> when the user confirms they want to discard changes and proceed;
	/// <see langword="false"/> when the user cancels (closes the dialog or presses Escape).
	/// </returns>
	/// <remarks>
	/// Callers should treat a <see langword="false"/> return as "abort the current action" —
	/// the project/query state should remain unchanged.
	/// The dialog is opened at <see cref="MaxWidth.Small"/> and can be dismissed with Escape,
	/// both of which are treated as a cancel (returns <see langword="false"/>).
	/// </remarks>
	public static async Task<bool> ShowUnsavedChangesDialogAsync(
		this IDialogService dialogService,
		string message = "Current project has unsaved changes. Continue without saving?")
	{
		var options = new DialogOptions
		{
			CloseOnEscapeKey = true,
			MaxWidth = MaxWidth.Small
		};

		var parameters = new DialogParameters<UnsavedChangesDialog>
		{
			{ x => x.Message, message }
		};

		var dialog = await dialogService.ShowAsync<UnsavedChangesDialog>("Unsaved Changes", parameters, options);
		var result = await dialog.Result;

		return (result is not null) && !result.Canceled && result.Data is bool confirm && confirm;
	}
}
