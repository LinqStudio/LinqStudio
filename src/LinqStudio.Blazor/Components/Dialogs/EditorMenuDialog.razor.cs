using LinqStudio.Core.Models;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace LinqStudio.Blazor.Components.Dialogs;

public partial class EditorMenuDialog
{
	[CascadingParameter] private IMudDialogInstance MudDialog { get; set; } = null!;

	[Parameter] public bool HasSavedQueries { get; set; }

	private void New() => MudDialog.Close(DialogResult.Ok(EditorMenuAction.New));

	private void Open()
	{
		if (!HasSavedQueries)
		{
			return;
		}

		MudDialog.Close(DialogResult.Ok(EditorMenuAction.Open));
	}

	private void Cancel() => MudDialog.Cancel();
}

public enum EditorMenuAction
{
	New,
	Open
}
