using LinqStudio.Abstractions.Models;
using LinqStudio.Blazor.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using MudBlazor;

namespace LinqStudio.Blazor.Components.Layout;

public partial class DatabaseTreeView : ComponentBase, IDisposable
{
	[Inject] private ILogger<DatabaseTreeView> Logger { get; set; } = null!;
	[Inject] private ProjectWorkspace Workspace { get; set; } = null!;

	private List<DatabaseTableName> _tables = [];
	private Dictionary<string, DatabaseTableDetail> _tableDetailsCache = new();
	private Dictionary<string, bool> _expandedStates = new();
	private HashSet<string> _loadingTables = [];
	private Dictionary<string, string> _tableDetailErrors = new();
	private bool _isLoading = false;
	private string? _loadError;

	// Track connection identity to avoid re-querying DB on unrelated workspace changes (e.g. query saves)
	private string? _trackedConnectionString;
	private DatabaseType? _trackedDatabaseType;

	protected override void OnInitialized()
	{
		Workspace.WorkspaceChanged += OnWorkspaceChanged;
		_trackedConnectionString = Workspace.CurrentProject?.ConnectionString;
		_trackedDatabaseType = Workspace.CurrentProject?.DatabaseType;
	}

	protected override async Task OnParametersSetAsync()
	{
		if (Workspace.IsProjectOpen && _tables.Count == 0)
		{
			await LoadTablesAsync();
		}
	}

	private void OnWorkspaceChanged(object? sender, EventArgs e)
	{
		var newConnectionString = Workspace.CurrentProject?.ConnectionString;
		var newDatabaseType = Workspace.CurrentProject?.DatabaseType;
		var wasOpen = _trackedConnectionString != null || _trackedDatabaseType != null;
		var isNowOpen = Workspace.IsProjectOpen;

		// Only reload tables when the DB connection changes or project is opened/closed.
		// Query saves and other workspace events must not trigger a DB round-trip.
		bool connectionChanged = newConnectionString != _trackedConnectionString
			|| newDatabaseType != _trackedDatabaseType;
		bool openStateChanged = wasOpen != isNowOpen;

		if (!connectionChanged && !openStateChanged)
			return;

		_trackedConnectionString = newConnectionString;
		_trackedDatabaseType = newDatabaseType;

		_tables.Clear();
		_expandedStates.Clear();
		_tableDetailsCache.Clear();
		_tableDetailErrors.Clear();
		_loadingTables.Clear();
		_loadError = null;

		InvokeAsync(async () =>
		{
			StateHasChanged();

			if (isNowOpen && Workspace.CurrentProject?.QueryGenerator != null)
			{
				await LoadTablesAsync();
			}
		});
	}

	private async Task LoadTablesAsync()
	{
		if (Workspace.CurrentProject?.QueryGenerator == null)
		{
			return;
		}

		_isLoading = true;
		_loadError = null;
		StateHasChanged();

		try
		{
			var tables = await Workspace.CurrentProject.QueryGenerator.GetTablesAsync();
			_tables = [.. tables];
			Logger.LogInformation("Loaded {TableCount} tables from database.", _tables.Count);

			_expandedStates.Clear();
			_tableDetailsCache.Clear();
			// Initialize expanded states for all tables
			foreach (var table in _tables)
			{
				_expandedStates[table.FullName] = false;
			}
		}
		catch (Exception ex)
		{
			Logger.LogError(ex, "Failed to load database tables.");
			_loadError = ex.Message;
		}
		finally
		{
			_isLoading = false;
			StateHasChanged();
		}
	}

	private async Task OnTableExpandedChanged(DatabaseTableName table, bool expanded)
	{
		_expandedStates[table.FullName] = expanded;
		if (expanded && !_tableDetailsCache.ContainsKey(table.FullName))
		{
			await LoadTableDetailsAsync(table);
		}
	}

	private async Task LoadTableDetailsAsync(DatabaseTableName table)
	{
		if (Workspace.CurrentProject?.QueryGenerator == null)
		{
			return;
		}

		// Mark as loading
		_loadingTables.Add(table.FullName);
		_tableDetailErrors.Remove(table.FullName);
		StateHasChanged();

		try
		{
			var tableDetail = await Workspace.CurrentProject.QueryGenerator.GetTableAsync(table);
			_tableDetailsCache[table.FullName] = tableDetail;
			Logger.LogInformation("Loaded {ColumnCount} columns for table '{TableName}'.", tableDetail.Columns.Count, table.FullName);
		}
		catch (Exception ex)
		{
			Logger.LogError(ex, "Failed to load columns for table '{TableName}'.", table.FullName);
			_tableDetailErrors[table.FullName] = ex.Message;
			_expandedStates[table.FullName] = false;
		}
		finally
		{
			_loadingTables.Remove(table.FullName);
			StateHasChanged();
		}
	}

	private async Task RefreshTables()
	{
		_tableDetailsCache.Clear();
		_tableDetailErrors.Clear();
		_loadingTables.Clear();
		_tables.Clear();
		_expandedStates.Clear();
		_loadError = null;

		await LoadTablesAsync();
	}

	private string GetColumnIcon(TableColumn column)
	{
		if (column.IsPrimaryKey)
		{
			return Icons.Material.Filled.Key;
		}

		if (column.IsIdentity)
		{
			return Icons.Material.Filled.Bolt;
		}

		return Icons.Material.Outlined.ViewColumn;
	}

	private Color GetColumnIconColor(TableColumn column)
	{
		if (column.IsPrimaryKey)
		{
			return Color.Warning; // Gold/amber color
		}

		return Color.Default;
	}

	private static readonly HashSet<string> _fixedSizeTypes = new(StringComparer.OrdinalIgnoreCase)
		{ "int", "bigint", "smallint", "tinyint", "bit" };

	private string FormatColumnType(TableColumn column)
	{
		var typeStr = column.DataType;

		if (!_fixedSizeTypes.Contains(typeStr))
		{
			if (column.MaxLength.HasValue && column.MaxLength.Value > 0)
			{
				typeStr = $"{typeStr}({column.MaxLength.Value})";
			}
			else if (column.Precision.HasValue && column.Scale.HasValue)
			{
				typeStr = $"{typeStr}({column.Precision.Value},{column.Scale.Value})";
			}
			else if (column.Precision.HasValue)
			{
				typeStr = $"{typeStr}({column.Precision.Value})";
			}
		}

		if (column.IsNullable)
		{
			typeStr += "?";
		}

		return typeStr;
	}

	public void Dispose()
	{
		Workspace.WorkspaceChanged -= OnWorkspaceChanged;
	}
}
