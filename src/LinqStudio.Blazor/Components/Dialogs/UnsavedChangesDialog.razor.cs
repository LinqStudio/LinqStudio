using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace LinqStudio.Blazor.Components.Dialogs;

public partial class UnsavedChangesDialog : ComponentBase
{
	[CascadingParameter]
	private IMudDialogInstance MudDialog { get; set; } = null!;

	[Parameter]
	public string Title { get; set; } = "Unsaved Changes";

	[Parameter]
	public string Message { get; set; } = "Current project has unsaved changes. Continue without saving?";

	[Parameter]
	public string ConfirmText { get; set; } = "Continue";

	[Parameter]
	public string CancelText { get; set; } = "Cancel";

	private void Confirm() => MudDialog.Close(DialogResult.Ok(true));

	private void Cancel() => MudDialog.Cancel();
}