using LinqStudio.Blazor.Abstractions;
using LinqStudio.Blazor.Components.Pages.Projects;
using LinqStudio.Blazor.Services;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace LinqStudio.Blazor.Components.Layout;

public partial class NavMenu : ComponentBase, IDisposable
{
	[Inject] private ProjectWorkspace Workspace { get; set; } = null!;
	[Inject] private NavigationManager NavigationManager { get; set; } = null!;
	[Inject] private IFileSystemService FileSystemService { get; set; } = null!;
	[Inject] private ErrorHandlingService ErrorHandlingService { get; set; } = null!;
	[Inject] private IDialogService DialogService { get; set; } = null!;
	[Inject] private ISnackbar Snackbar { get; set; } = null!;

	private readonly bool _queriesExpanded = true;
	private readonly bool _projectExpanded = true;

	protected override void OnInitialized()
	{
		Workspace.WorkspaceChanged += OnWorkspaceChanged;
	}

	private void OnWorkspaceChanged(object? sender, EventArgs e)
	{
		InvokeAsync(StateHasChanged);
	}

	private string GetProjectTitle()
	{
		if (Workspace.IsProjectOpen)
		{
			var unsaved = Workspace.HasUnsavedChanges ? " *" : "";
			return $"{Workspace.CurrentProjectName}{unsaved}";
		}
		return "Project";
	}

	#region Project Actions

	private void NewProject()
	{
		// Check for unsaved changes
		if (Workspace.HasUnsavedChanges)
		{
			var confirmTask = DialogService.ShowMessageBox(
				"Unsaved Changes",
				"Current project has unsaved changes. Continue without saving?",
				yesText: "Continue", cancelText: "Cancel");

			confirmTask.ContinueWith(async task =>
			{
				var confirm = await task;
				if (confirm == true)
				{
					CreateNewProject();
				}
			}, TaskScheduler.FromCurrentSynchronizationContext());

			return;
		}

		CreateNewProject();
	}

	private void CreateNewProject()
	{
		// Create a default project with empty connection string
		Workspace.CreateNew("Untitled", string.Empty);
		Snackbar.Add("New project created. Use 'Save' or 'Save As' to save it.", Severity.Info);
		NavigationManager.NavigateTo("/editor");
	}

	private async Task OpenProject()
	{
		// Check for unsaved changes
		if (Workspace.HasUnsavedChanges)
		{
			bool? confirm = await DialogService.ShowMessageBox(
				"Unsaved Changes",
				"Current project has unsaved changes. Continue without saving?",
				yesText: "Continue", cancelText: "Cancel");

			if (confirm != true)
			{
				return;
			}
		}

		try
		{
			// Use native file dialog directly
			var filePath = await FileSystemService.PromptOpenFileAsync("linq");

			if (string.IsNullOrWhiteSpace(filePath))
			{
				return; // User cancelled
			}

			await Workspace.LoadAsync(filePath);
			Snackbar.Add($"Project '{Workspace.CurrentProjectName}' loaded successfully.", Severity.Success);
		}
		catch (Exception ex)
		{
			await ErrorHandlingService.HandleErrorAsync(ex, "Failed to open project file.");
		}
	}

	private async Task EditProject()
	{
		if (!Workspace.IsProjectOpen)
		{
			return;
		}

		var parameters = new DialogParameters<EditProjectDialog>
		{
			{ x => x.Project, Workspace.CurrentProject! }
		};

		var options = new DialogOptions
		{
			CloseOnEscapeKey = true,
			MaxWidth = MaxWidth.Medium,
			FullWidth = true
		};

		var dialog = await DialogService.ShowAsync<EditProjectDialog>("Edit Project", parameters, options);
		var result = await dialog.Result;

		if (result != null && !result.Canceled && result.Data is Core.Models.Project updatedProject)
		{
			Workspace.Update(updatedProject);
			Snackbar.Add("Project updated. Don't forget to save your changes.", Severity.Info);
		}
	}

	private async Task SaveProject()
	{
		if (!Workspace.IsProjectOpen)
		{
			return;
		}

		try
		{
			// If no file path, use Save As
			if (string.IsNullOrEmpty(Workspace.CurrentFilePath))
			{
				await SaveAsProject();
			}
			else
			{
				await Workspace.SaveAsync();
				Snackbar.Add("Project saved successfully.", Severity.Success);
			}
		}
		catch (Exception ex)
		{
			await ErrorHandlingService.HandleErrorAsync(ex, "Failed to save project.");
		}
	}

	private async Task SaveAsProject()
	{
		if (!Workspace.IsProjectOpen)
		{
			return;
		}

		try
		{
			// Use native file dialog directly with current project name
			var filePath = await FileSystemService.PromptSaveFileAsync($"{Workspace.CurrentProjectName}.linq");

			if (string.IsNullOrWhiteSpace(filePath))
			{
				return; // User cancelled
			}

			await Workspace.SaveAsAsync(filePath);
			Snackbar.Add("Project saved successfully.", Severity.Success);
		}
		catch (Exception ex)
		{
			await ErrorHandlingService.HandleErrorAsync(ex, "Failed to save project.");
		}
	}

	private async Task CloseProject()
	{
		// Check for unsaved changes
		if (Workspace.HasUnsavedChanges)
		{
			bool? confirm = await DialogService.ShowMessageBox(
				"Unsaved Changes",
				"Current project has unsaved changes. Save before closing?",
				yesText: "Save", noText: "Don't Save", cancelText: "Cancel");

			if (confirm == true)
			{
				await SaveProject();
			}
			else if (confirm == null)
			{
				return; // Cancelled
			}
		}

		Workspace.Close();
		Snackbar.Add("Project closed.", Severity.Info);
		NavigationManager.NavigateTo("/");
	}

	#endregion

	#region Query Actions

	private string GetQueryEditorUrl(int queryIndex) => $"/editor/{queryIndex}";

	#endregion

	public void Dispose()
	{
		Workspace.WorkspaceChanged -= OnWorkspaceChanged;
	}
}