using LinqStudio.Blazor.Extensions;
using LinqStudio.Blazor.Models;
using LinqStudio.Core.Repositories;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace LinqStudio.Blazor.Components.Dialogs;

/// <summary>
/// A MudBlazor dialog that lets users browse, select, and delete saved projects.
/// Supports two modes: <see cref="ProjectBrowserMode.Open"/> to pick an existing project,
/// and <see cref="ProjectBrowserMode.SaveAs"/> to enter or choose a project name for saving.
/// </summary>
public partial class ProjectBrowserDialog : ComponentBase
{
	/// <summary>Gets or sets the MudBlazor dialog instance used to close or cancel this dialog.</summary>
	[CascadingParameter]
	private IMudDialogInstance MudDialog { get; set; } = null!;

	/// <summary>Gets or sets the repository used to list and delete projects.</summary>
	[Inject]
	private IProjectRepository ProjectRepository { get; set; } = null!;

	/// <summary>Gets or sets the snackbar service used for transient error notifications.</summary>
	[Inject]
	private ISnackbar Snackbar { get; set; } = null!;

	/// <summary>Gets or sets the dialog service used to show confirmation prompts.</summary>
	[Inject]
	private IDialogService DialogService { get; set; } = null!;

	/// <summary>
	/// Gets or sets the dialog mode, which controls whether the user is opening an existing
	/// project or providing a name to save the current one.
	/// </summary>
	[Parameter]
	public ProjectBrowserMode Mode { get; set; }

	private IReadOnlyList<ProjectSummary> _projects = [];
	private bool _isLoading = true;
	private ProjectSummary? _selected;
	private string _projectName = string.Empty;
	private string? _errorMessage;

	/// <inheritdoc />
	protected override async Task OnInitializedAsync()
	{
		await LoadProjectsAsync();
	}

	/// <summary>
	/// Fetches the project list from the repository and updates component state.
	/// </summary>
	/// <remarks>
	/// <see cref="StateHasChanged"/> is called immediately after setting <c>_isLoading = true</c>
	/// so the spinner is rendered before the async repository call begins — without it the UI would
	/// not update until the awaited task completes.
	/// <para>
	/// Errors are stored in <c>_errorMessage</c> rather than shown via <see cref="ISnackbar"/>
	/// because the failure is persistent and contextual to this dialog (the list could not be loaded),
	/// not a brief, recoverable action. The inline alert keeps the feedback visible alongside the
	/// empty list, helping the user understand why nothing is shown.
	/// </para>
	/// </remarks>
	private async Task LoadProjectsAsync()
	{
		_isLoading = true;
		_errorMessage = null;
		// Force re-render now so the spinner appears before the awaited repo call starts.
		StateHasChanged();

		try
		{
			_projects = (await ProjectRepository.ListProjectsAsync())
				.OrderByDescending(p => p.ModifiedDate)
				.ToList();
		}
		catch (Exception ex)
		{
			_errorMessage = $"Failed to load projects: {ex.Message}";
			_projects = [];
		}
		finally
		{
			_isLoading = false;
		}
	}

	/// <summary>
	/// Sets <paramref name="project"/> as the currently selected item.
	/// In <see cref="ProjectBrowserMode.SaveAs"/> mode, also copies the project name
	/// into the editable name field so the user can quickly save over an existing project.
	/// </summary>
	/// <param name="project">The project the user clicked on.</param>
	private void SelectProject(ProjectSummary project)
	{
		_selected = project;

		if (Mode == ProjectBrowserMode.SaveAs)
		{
			_projectName = project.Name;
		}
	}

	/// <summary>
	/// Asks the user to confirm, then deletes <paramref name="project"/> from the repository and refreshes the list.
	/// </summary>
	/// <param name="project">The project to delete.</param>
	/// <remarks>
	/// Errors here are shown via <see cref="ISnackbar"/> (toast) rather than stored in
	/// <c>_errorMessage</c> because the rest of the list is still valid and usable — the
	/// failure applies only to this single delete action.
	/// <para>
	/// In the razor markup the delete button is wrapped in a <c>&lt;span @onclick:stopPropagation="true"&gt;</c>
	/// so clicking Delete does not also trigger the row's <c>SelectProject</c> handler.
	/// </para>
	/// </remarks>
	private async Task DeleteProjectAsync(ProjectSummary project)
	{
		var confirmed = await DialogService.ShowDeleteProjectConfirmationAsync(project.Name);
		if (!confirmed)
		{
			return;
		}

		try
		{
			await ProjectRepository.DeleteProjectAsync(project.Id);
		}
		catch (Exception ex)
		{
			Snackbar.Add($"Failed to delete project: {ex.Message}", Severity.Error);
			return;
		}

		if (_selected?.Id == project.Id)
		{
			_selected = null;

			if (Mode == ProjectBrowserMode.SaveAs)
			{
				_projectName = string.Empty;
			}
		}

		await LoadProjectsAsync();
	}

	/// <summary>
	/// Confirms the dialog and closes it with a <see cref="ProjectBrowserResult"/>.
	/// </summary>
	/// <remarks>
	/// In <see cref="ProjectBrowserMode.Open"/> mode the selected project's ID is returned directly.
	/// In <see cref="ProjectBrowserMode.SaveAs"/> mode the result ID is set to the selected project's
	/// ID only when the trimmed name matches the selected project's name (i.e. the user is overwriting
	/// the same project); otherwise an empty string signals the caller to create a new project.
	/// </remarks>
	private void Confirm()
	{
		if (Mode == ProjectBrowserMode.Open)
		{
			MudDialog.Close(DialogResult.Ok(new ProjectBrowserResult(_selected!.Id, _selected.Name)));
		}
		else
		{
			// Re-use the existing project ID only when the name is unchanged (overwrite).
			// An empty ID tells the workspace to create a brand-new project entry.
			var id = _selected?.Name.Trim() == _projectName.Trim() ? _selected.Id : string.Empty;
			MudDialog.Close(DialogResult.Ok(new ProjectBrowserResult(id, _projectName.Trim())));
		}
	}

	/// <summary>Cancels the dialog without returning a result.</summary>
	private void Cancel() => MudDialog.Cancel();
}
