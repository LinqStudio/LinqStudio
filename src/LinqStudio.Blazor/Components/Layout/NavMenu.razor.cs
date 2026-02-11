using LinqStudio.Blazor.Abstractions;
using LinqStudio.Blazor.Components.Dialogs;
using LinqStudio.Blazor.Constants;
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

	private async Task<bool> ShowUnsavedChangesDialog(string message = "Current project has unsaved changes. Continue without saving?")
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

		var dialog = await DialogService.ShowAsync<UnsavedChangesDialog>("Unsaved Changes", parameters, options);
		var result = await dialog.Result;

		return (result is not null) && !result.Canceled && result.Data is bool confirm && confirm;
	}

	private void NewProject()
	{
		// Check for unsaved changes
		if (Workspace.HasUnsavedChanges)
		{
			var confirmTask = ShowUnsavedChangesDialog("Current project has unsaved changes. Continue without saving?");

			confirmTask.ContinueWith(async task =>
			{
				var confirm = await task;
				if (confirm)
				{
					await CreateNewProjectAsync();
				}
			}, TaskScheduler.FromCurrentSynchronizationContext());

			return;
		}

		_ = CreateNewProjectAsync();
	}

	private async Task CreateNewProjectAsync()
	{
		// Create a default project with empty connection string
		await Workspace.CreateNewAsync("Untitled");
		Snackbar.Add("New project created. Use 'Save' or 'Save As' to save it.", Severity.Info);

		// Navigate to home page since we a an empty project now
		NavigationManager.NavigateTo("/");
	}

	private async Task OpenProject()
	{
		// Check for unsaved changes
		if (Workspace.HasUnsavedChanges)
		{
			bool confirm = await ShowUnsavedChangesDialog("Current project has unsaved changes. Continue without saving?");

			if (!confirm)
			{
				return;
			}
		}

		try
		{
			// Use native file dialog directly
			var filePath = await FileSystemService.PromptOpenFileAsync(FileExtensions.Project);

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
			var filePath = await FileSystemService.PromptSaveFileAsync(FileExtensions.EnsureHasExtension(Workspace.CurrentProjectName, FileExtensions.Project));

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
			bool confirm = await ShowUnsavedChangesDialog("Current project has unsaved changes. Continue without saving?");

			if (!confirm)
			{
				return;
			}
		}

		Workspace.Close();
		Snackbar.Add("Project closed.", Severity.Info);
		NavigationManager.NavigateTo("/");
	}

	private void CreateNewQuery()
	{
		if (!Workspace.IsProjectOpen || Workspace.CurrentProject == null)
		{
			return;
		}

		var queryId = Workspace.Queries.CreateNewQuery();
		NavigationManager.NavigateTo($"/editor/{queryId}");
	}

	private async Task OpenQueryFromFile()
	{
		if (!Workspace.IsProjectOpen)
		{
			return;
		}

		try
		{
			var filePath = await FileSystemService.PromptOpenFileAsync(LinqStudio.Blazor.Constants.FileExtensions.Query);

			if (string.IsNullOrEmpty(filePath))
			{
				return; // User cancelled
			}

			var queryId = await Workspace.Queries.OpenQueryFromFileAsync(filePath);

			if (queryId.HasValue)
			{
				NavigationManager.NavigateTo($"/editor/{queryId.Value}");
				Snackbar.Add("Query opened successfully.", Severity.Success);
			}
			else
			{
				Snackbar.Add("Failed to open query file.", Severity.Error);
			}
		}
		catch (Exception ex)
		{
			await ErrorHandlingService.HandleErrorAsync(ex, "Failed to open query file.");
		}
	}

	private void OpenSavedQuery(Guid queryId)
	{
		if (!Workspace.IsProjectOpen)
		{
			return;
		}

		Workspace.Queries.OpenQuery(queryId);
		NavigationManager.NavigateTo($"/editor/{queryId}");
	}

	public void Dispose()
	{
		Workspace.WorkspaceChanged -= OnWorkspaceChanged;
	}
}