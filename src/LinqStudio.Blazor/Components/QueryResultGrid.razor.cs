#nullable enable

using LinqStudio.Abstractions.Models;
using LinqStudio.Blazor.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using MudBlazor;

namespace LinqStudio.Blazor.Components;

public partial class QueryResultGrid : ComponentBase
{
	[Parameter]
	public QueryExecutionResult? Result { get; set; }

	[Parameter]
	public bool IsExecuting { get; set; }

	[Inject]
	private IClipboardService ClipboardService { get; set; } = null!;

	private MudDataGrid<IReadOnlyDictionary<string, object?>>? _dataGrid;
	private HashSet<int> _selectedRows = new();
	private int _lastClickedRowIndex = -1;
	private QueryExecutionResult? _previousResult;

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

		await ClipboardService.CopyToClipboardAsync(tsv.ToString());
	}
}

#nullable restore
