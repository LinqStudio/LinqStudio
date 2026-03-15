#nullable enable

using LinqStudio.Abstractions.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using MudBlazor;

namespace LinqStudio.Blazor.Components;

public partial class QueryResultGrid : ComponentBase
{
	[Parameter]
	public QueryExecutionResult? Result { get; set; }

	[Parameter]
	public bool IsExecuting { get; set; }

	[Parameter]
	public Dictionary<string, SortDefinition<IReadOnlyDictionary<string, object?>>> SortDefinitions { get; set; } = new();

	[Parameter]
	public EventCallback<Dictionary<string, SortDefinition<IReadOnlyDictionary<string, object?>>>> OnSortDefinitionsChanged { get; set; }

	[Inject]
	private IJSRuntime JSRuntime { get; set; } = null!;

	private MudDataGrid<IReadOnlyDictionary<string, object?>>? _dataGrid;
	private HashSet<int> _selectedRows = new();
	private int _lastClickedRowIndex = -1;
	private QueryExecutionResult? _previousResult;
	private Dictionary<string, SortDefinition<IReadOnlyDictionary<string, object?>>>? _lastKnownSortDefinitions;

	protected override void OnParametersSet()
	{
		base.OnParametersSet();

		// Reset selection when Result changes
		if (Result != _previousResult)
		{
			_selectedRows.Clear();
			_lastClickedRowIndex = -1;
			_previousResult = Result;
		}
	}

	protected override async Task OnAfterRenderAsync(bool firstRender)
	{
		await base.OnAfterRenderAsync(firstRender);

		// Add data-testid attributes to rows for E2E testing
		// MudDataGrid with Virtualize needs a delay for rows to be rendered in DOM
		if (Result?.Rows.Count > 0)
		{
			try
			{
				// Small delay to allow virtualized rows to render
				await Task.Delay(100);
				await JSRuntime.InvokeVoidAsync("addDataTestIdsToRows", "[data-testid='query-result-grid']");
			}
			catch
			{
				// Silently ignore if JS function not available or fails
			}
		}

		// Snapshot current sort state from grid and propagate changes to parent
		if (_dataGrid is not null && OnSortDefinitionsChanged.HasDelegate)
		{
			var currentSort = _dataGrid.SortDefinitions;
			if (currentSort is not null && !AreSortDefinitionsEqual(currentSort, _lastKnownSortDefinitions))
			{
				_lastKnownSortDefinitions = new Dictionary<string, SortDefinition<IReadOnlyDictionary<string, object?>>>(currentSort);
				await OnSortDefinitionsChangedInternal(currentSort);
			}
		}
	}

	private async Task OnSortDefinitionsChangedInternal(Dictionary<string, SortDefinition<IReadOnlyDictionary<string, object?>>> defs)
	{
		SortDefinitions = defs;
		await OnSortDefinitionsChanged.InvokeAsync(defs);
	}

	private static bool AreSortDefinitionsEqual(
		Dictionary<string, SortDefinition<IReadOnlyDictionary<string, object?>>> a,
		Dictionary<string, SortDefinition<IReadOnlyDictionary<string, object?>>>? b)
	{
		if (b is null) return false;
		if (a.Count != b.Count) return false;
		foreach (var key in a.Keys)
		{
			if (!b.TryGetValue(key, out var bDef)) return false;
			var aDef = a[key];
			if (aDef.Descending != bDef.Descending || aDef.Index != bDef.Index) return false;
		}
		return true;
	}

	private string FormatElapsedTime(TimeSpan elapsed)
	{
		if (elapsed.TotalSeconds < 1)
		{
			return $"{elapsed.TotalMilliseconds:F0}ms";
		}

		return $"{elapsed.TotalSeconds:F2}s";
	}

	private int GetRowIndex(IReadOnlyDictionary<string, object?> row)
	{
		if (Result is null) return -1;
		for (int i = 0; i < Result.Rows.Count; i++)
		{
			if (ReferenceEquals(Result.Rows[i], row))
				return i;
		}
		return -1;
	}

	private string GetRowClass(IReadOnlyDictionary<string, object?> row, int index)
	{
		return _selectedRows.Contains(index) ? "row-selected" : "";
	}

	private void OnMudRowClick(DataGridRowClickEventArgs<IReadOnlyDictionary<string, object?>> args)
	{
		OnRowClick(args.Item, args.MouseEventArgs);
	}

	private void OnRowClick(IReadOnlyDictionary<string, object?> row, MouseEventArgs e)
	{
		var rowIndex = GetRowIndex(row);
		if (rowIndex == -1)
			return;

		if (e.CtrlKey || e.MetaKey)
		{
			if (_selectedRows.Contains(rowIndex))
				_selectedRows.Remove(rowIndex);
			else
				_selectedRows.Add(rowIndex);
		}
		else if (e.ShiftKey && _lastClickedRowIndex >= 0)
		{
			_selectedRows.Clear();
			var start = Math.Min(_lastClickedRowIndex, rowIndex);
			var end = Math.Max(_lastClickedRowIndex, rowIndex);
			for (int i = start; i <= end; i++)
				_selectedRows.Add(i);
		}
		else
		{
			_selectedRows.Clear();
			_selectedRows.Add(rowIndex);
		}

		_lastClickedRowIndex = rowIndex;
		StateHasChanged();
	}

	private void OnKeyDown(KeyboardEventArgs e)
	{
		if ((e.CtrlKey || e.MetaKey) && e.Key == "c")
		{
			_ = CopySelectionToClipboard();
		}
	}

	private async Task CopySelectionToClipboard()
	{
		if (Result is null || _selectedRows.Count == 0) return;

		var tsv = new System.Text.StringBuilder();
		tsv.AppendLine(string.Join("\t", Result.ColumnNames));

		foreach (var rowIndex in _selectedRows.OrderBy(i => i))
		{
			var row = Result.Rows[rowIndex];
			var values = Result.ColumnNames.Select(col =>
			{
				var cellValue = row.GetValueOrDefault(col);
				return cellValue?.ToString() ?? "NULL";
			});
			tsv.AppendLine(string.Join("\t", values));
		}

		try
		{
			await JSRuntime.InvokeAsync<bool>("copyToClipboard", tsv.ToString());
		}
		catch
		{
			// Clipboard API might fail in some browsers/contexts
		}
	}
}

#nullable restore
