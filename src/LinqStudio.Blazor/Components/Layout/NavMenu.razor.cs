using LinqStudio.Blazor.Components.Dialogs;
using LinqStudio.Blazor.Extensions;
using LinqStudio.Blazor.Models;
using LinqStudio.Blazor.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using MudBlazor;

namespace LinqStudio.Blazor.Components.Layout;

/// <summary>
/// Navigation menu component that drives all project lifecycle actions (New, Open, Edit,
/// Save, Save As, Close) and provides shortcuts to create or navigate to queries.
/// Subscribes to <see cref="ProjectWorkspace.WorkspaceChanged"/> to keep the displayed
/// project title and menu items in sync with workspace state.
/// </summary>
public partial class NavMenu : ComponentBase, IDisposable
{
	/// <summary>Gets or sets the logger for this component.</summary>
	[Inject] private ILogger<NavMenu> Logger { get; set; } = null!;

	/// <summary>Gets or sets the shared project workspace that holds current project state.</summary>
	[Inject] private ProjectWorkspace Workspace { get; set; } = null!;

	/// <summary>Gets or sets the navigation manager used to redirect after project operations.</summary>
	[Inject] private NavigationManager NavigationManager { get; set; } = null!;

	/// <summary>Gets or sets the error handling service for surfacing unexpected exceptions to the user.</summary>
	[Inject] private ErrorHandlingService ErrorHandlingService { get; set; } = null!;

	/// <summary>Gets or sets the dialog service used to show confirmation and browser dialogs.</summary>
	[Inject] private IDialogService DialogService { get; set; } = null!;

	/// <summary>Gets or sets the snackbar service for transient user notifications.</summary>
	[Inject] private ISnackbar Snackbar { get; set; } = null!;

	/// <inheritdoc />
	protected override void OnInitialized()
	{
		Workspace.WorkspaceChanged += OnWorkspaceChanged;
	}

	/// <summary>
	/// Handles workspace state changes by scheduling a Blazor re-render on the UI thread.
	/// </summary>
	/// <param name="sender">The source of the event (unused).</param>
	/// <param name="e">Event arguments (unused).</param>
	/// <remarks>
	/// <see cref="ComponentBase.InvokeAsync"/> is required here because the workspace event
	/// may be raised from a non-UI thread, and Blazor's renderer must be accessed from its
	/// synchronization context.
	/// </remarks>
	private void OnWorkspaceChanged(object? sender, EventArgs e)
	{
		InvokeAsync(StateHasChanged);
	}

	/// <summary>
	/// Returns the text to display in the Project navigation link.
	/// Appends <c>" *"</c> when the workspace has unsaved changes.
	/// </summary>
	/// <returns>
	/// The current project name (with optional unsaved-changes marker), or <c>"Project"</c>
	/// when no project is open.
	/// </returns>
	private string GetProjectTitle()
	{
		if (Workspace.IsProjectOpen)
		{
			var unsaved = Workspace.HasUnsavedChanges ? " *" : "";
			return $"{Workspace.CurrentProjectName}{unsaved}";
		}
		return "Project";
	}

	/// <summary>
	/// Shows a confirmation dialog warning the user about unsaved changes.
	/// </summary>
	/// <param name="message">The message to display in the dialog body.</param>
	/// <returns>
	/// <see langword="true"/> if the user confirmed they want to continue without saving;
	/// <see langword="false"/> if they chose to cancel.
	/// </returns>
	private Task<bool> ShowUnsavedChangesDialog(string message = "Current project has unsaved changes. Continue without saving?")
		=> DialogService.ShowUnsavedChangesDialogAsync(message);

	/// <summary>
	/// Handles the "New" menu action. Prompts for confirmation if there are unsaved changes,
	/// then creates a blank project and navigates home.
	/// </summary>
	/// <remarks>
	/// The unsaved-changes guard short-circuits with an early <c>return</c> after the
	/// <c>if</c> block regardless of whether the user confirmed, so the <see cref="CreateNewProjectAsync"/>
	/// call below the block is only reached when there were no unsaved changes to begin with.
	/// This avoids a double-create if the user confirms the dialog.
	/// </remarks>
	private async Task NewProject()
	{
		if (Workspace.HasUnsavedChanges)
		{
			var confirm = await ShowUnsavedChangesDialog("Current project has unsaved changes. Continue without saving?");
			if (confirm)
			{
				await CreateNewProjectAsync();
			}
			// Always return here — whether confirmed or cancelled — to prevent the
			// unconditional CreateNewProjectAsync() call below from also running.
			return;
		}

		await CreateNewProjectAsync();
	}

	/// <summary>
	/// Creates a new untitled project in the workspace and redirects to the home page.
	/// </summary>
	private async Task CreateNewProjectAsync()
	{
		await Workspace.CreateNewAsync("Untitled");
		Logger.LogInformation("New project created.");
		Snackbar.Add("New project created. Use 'Save' or 'Save As' to save it.", Severity.Info);

		NavigationManager.NavigateTo("/");
	}

	/// <summary>
	/// Handles the "Open" menu action. Optionally prompts for confirmation of unsaved changes,
	/// then shows the project browser dialog and loads the selected project.
	/// </summary>
	private async Task OpenProject()
	{
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
			var options = new DialogOptions { CloseOnEscapeKey = true, MaxWidth = MaxWidth.Medium, FullWidth = true };
			var parameters = new DialogParameters<ProjectBrowserDialog>
			{
				{ x => x.Mode, ProjectBrowserMode.Open }
			};
			var dialog = await DialogService.ShowAsync<ProjectBrowserDialog>("Open Project", parameters, options);
			var result = await dialog.Result;

			if (result is null || result.Canceled || result.Data is not ProjectBrowserResult browserResult)
			{
				return;
			}

			await Workspace.LoadAsync(browserResult.ProjectId);
			Logger.LogInformation("Project '{ProjectName}' opened.", Workspace.CurrentProjectName);
			Snackbar.Add($"Project '{Workspace.CurrentProjectName}' loaded successfully.", Severity.Success);
		}
		catch (Exception ex)
		{
			Logger.LogError(ex, "Failed to open project.");
			await ErrorHandlingService.HandleErrorAsync(ex, "Failed to open project.");
		}
	}

	/// <summary>
	/// Handles the "Properties" menu action. Opens the project edit dialog and applies
	/// any changes the user confirms to the current workspace project.
	/// Does nothing if no project is open.
	/// </summary>
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

	/// <summary>
	/// Handles the "Save" menu action. Delegates to <see cref="SaveAsProject"/> when the project
	/// has never been saved (no ID), otherwise persists in place.
	/// Does nothing if no project is open.
	/// </summary>
	private async Task SaveProject()
	{
		if (!Workspace.IsProjectOpen)
		{
			return;
		}

		try
		{
			// A missing project ID means the project was never persisted — treat Save like Save As.
			if (string.IsNullOrEmpty(Workspace.CurrentProjectId))
			{
				await SaveAsProject();
			}
			else
			{
				await Workspace.SaveAsync();
				Logger.LogInformation("Project '{ProjectName}' saved.", Workspace.CurrentProjectName);
				Snackbar.Add("Project saved successfully.", Severity.Success);
			}
		}
		catch (Exception ex)
		{
			Logger.LogError(ex, "Failed to save project '{ProjectName}'.", Workspace.CurrentProjectName);
			await ErrorHandlingService.HandleErrorAsync(ex, "Failed to save project.");
		}
	}

	/// <summary>
	/// Handles the "Save As" menu action. Opens the project browser in
	/// <see cref="ProjectBrowserMode.SaveAs"/> mode and saves the project under the chosen name.
	/// Does nothing if no project is open.
	/// </summary>
	private async Task SaveAsProject()
	{
		if (!Workspace.IsProjectOpen)
		{
			return;
		}

		try
		{
			var options = new DialogOptions { CloseOnEscapeKey = true, MaxWidth = MaxWidth.Medium, FullWidth = true };
			var parameters = new DialogParameters<ProjectBrowserDialog>
			{
				{ x => x.Mode, ProjectBrowserMode.SaveAs }
			};
			var dialog = await DialogService.ShowAsync<ProjectBrowserDialog>("Save Project As", parameters, options);
			var result = await dialog.Result;

			if (result is null || result.Canceled || result.Data is not ProjectBrowserResult browserResult)
			{
				return;
			}

			// An empty ProjectId means the user chose a new name — SaveAsAsync creates a new entry.
			// A non-empty ID means the user selected an existing project to overwrite.
			var existingId = string.IsNullOrEmpty(browserResult.ProjectId) ? null : browserResult.ProjectId;
			await Workspace.SaveAsAsync(browserResult.ProjectName, existingId);
			Logger.LogInformation("Project saved as '{ProjectName}'.", browserResult.ProjectName);
			Snackbar.Add("Project saved successfully.", Severity.Success);
		}
		catch (Exception ex)
		{
			Logger.LogError(ex, "Failed to save project as new file.");
			await ErrorHandlingService.HandleErrorAsync(ex, "Failed to save project.");
		}
	}

	/// <summary>
	/// Handles the "Close" menu action. Optionally prompts for confirmation of unsaved changes,
	/// then closes the current project and navigates home.
	/// </summary>
	private async Task CloseProject()
	{
		if (Workspace.HasUnsavedChanges)
		{
			bool confirm = await ShowUnsavedChangesDialog("Current project has unsaved changes. Continue without saving?");

			if (!confirm)
			{
				return;
			}
		}

		Workspace.Close();
		Logger.LogInformation("Project closed.");
		Snackbar.Add("Project closed.", Severity.Info);
		NavigationManager.NavigateTo("/");
	}

	/// <summary>
	/// Unsubscribes from <see cref="ProjectWorkspace.WorkspaceChanged"/> to prevent
	/// callbacks from reaching a disposed component.
	/// </summary>
	public void Dispose()
	{
		Workspace.WorkspaceChanged -= OnWorkspaceChanged;
	}
}