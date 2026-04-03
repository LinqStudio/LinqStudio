using LinqStudio.Abstractions.Models;
using LinqStudio.Blazor.Services;
using LinqStudio.Core.Models;
using LinqStudio.Core.Resources;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace LinqStudio.Blazor.Components.Dialogs;

public partial class EditProjectDialog : ComponentBase
{
	[CascadingParameter]
	private IMudDialogInstance MudDialog { get; set; } = null!;

	[Parameter, EditorRequired]
	public ServerConnection Connection { get; set; } = null!;

	private string? _connectionString;
	private DatabaseType _databaseType = DatabaseType.Mssql;
	private int _timeoutSeconds = 10;
	private bool _isTesting = false;

	protected override void OnInitialized()
	{
		_connectionString = Connection.ConnectionString;
		_databaseType = Connection.DatabaseType;
	}

	private void Cancel() => MudDialog.Cancel();

	private async Task Save()
	{
		if (string.IsNullOrWhiteSpace(_connectionString))
		{
			Snackbar.Add(SharedResource.ConnectionSettings_Message_ValidationFailed, Severity.Error);
			return;
		}

		Connection.UpdateConnection(_databaseType, _connectionString);
		MudDialog.Close(DialogResult.Ok(Connection));
	}

	private async Task ValidateConnection()
	{
		if (string.IsNullOrWhiteSpace(_connectionString))
		{
			Snackbar.Add(SharedResource.ConnectionSettings_Message_ValidationFailed, Severity.Error);
			return;
		}

		_isTesting = true;
		StateHasChanged();

		try
		{
			await Connection.TestConnectionAsync(_databaseType, _connectionString, _timeoutSeconds);
			Snackbar.Add(SharedResource.ConnectionSettings_Message_ValidationSuccess, Severity.Success);
		}
		catch (OperationCanceledException)
		{
			Snackbar.Add(SharedResource.ConnectionSettings_Message_ValidationFailed + " (Timeout)", Severity.Error);
		}
		catch (Exception ex)
		{
			await ErrorHandlingService.HandleErrorAsync(ex, SharedResource.ConnectionSettings_Message_ValidationFailed);
		}
		finally
		{
			_isTesting = false;
			StateHasChanged();
		}
	}

}
