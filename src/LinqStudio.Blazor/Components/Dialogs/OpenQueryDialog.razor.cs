using LinqStudio.Core.Models;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace LinqStudio.Blazor.Components.Dialogs;

public partial class OpenQueryDialog
{
	[CascadingParameter] private IMudDialogInstance MudDialog { get; set; } = null!;

	[Parameter] public IReadOnlyList<SavedQuery>? Queries { get; set; }

	private void Select(Guid queryId) => MudDialog.Close(DialogResult.Ok(queryId));

	private void Cancel() => MudDialog.Cancel();
}
