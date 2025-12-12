using LinqStudio.Core.Models;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace LinqStudio.Blazor.Components.Pages.Projects;

public partial class EditProjectDialog : ComponentBase
{
	[CascadingParameter]
	private IMudDialogInstance MudDialog { get; set; } = null!;

	[Parameter]
	public Project Project { get; set; } = null!;

	private string _connectionString = string.Empty;

	protected override void OnInitialized()
	{
		_connectionString = Project.ConnectionString;
	}

	private void Cancel() => MudDialog.Cancel();

	private void Save()
	{
		var updatedProject = Project with
		{
			ConnectionString = _connectionString
		};

		MudDialog.Close(DialogResult.Ok(updatedProject));
	}
}