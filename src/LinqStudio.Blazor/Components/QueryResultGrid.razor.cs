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

	[Parameter]
	public bool IsEditable { get; set; }

	[Parameter]
	public IReadOnlySet<string> EditableColumns { get; set; } = new HashSet<string>(StringComparer.Ordinal);

	[Parameter]
	public EventCallback<object> OnRowSelected { get; set; }

	[Inject]
	private IClipboardService ClipboardService { get; set; } = null!;

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

	private IReadOnlyList<object> GridItems
		=> Result?.Items ?? [];

	private bool HasEditableRuntimeItems
		=> GridItems.Count > 0 && GetRuntimeProperty(Result!.ColumnNames.FirstOrDefault() ?? string.Empty) is not null;

	private System.Reflection.PropertyInfo? GetRuntimeProperty(string columnName)
		=> GridItems.Count > 0
			? GridItems[0].GetType().GetProperty(columnName)
			: null;

	private Dictionary<string, object> GetRuntimeColumnParameters(
		string columnName,
		System.Reflection.PropertyInfo property)
	{
		var parameters = new Dictionary<string, object>
		{
			["ColumnName"] = columnName,
			["IsEditable"] = IsEditable
				&& EditableColumns.Contains(columnName)
				&& SupportsEdit(property.PropertyType),
			["PropertyInfo"] = property
		};

		return parameters;
	}

	private static bool SupportsEdit(Type propertyType)
	{
		var type = Nullable.GetUnderlyingType(propertyType) ?? propertyType;
		return type == typeof(string)
			|| type == typeof(byte)
			|| type == typeof(sbyte)
			|| type == typeof(short)
			|| type == typeof(ushort)
			|| type == typeof(int)
			|| type == typeof(uint)
			|| type == typeof(long)
			|| type == typeof(ulong)
			|| type == typeof(float)
			|| type == typeof(double)
			|| type == typeof(decimal)
			|| type == typeof(DateTime);
	}

	private object? GetCellValue(object item, string columnName)
	{
		var property = GetRuntimeProperty(columnName);
		return property is not null
			? property.GetValue(item)
			: columnName == "Value" ? item : null;
	}

	private int GetRowIndex(object row)
	{
		if (Result is null) return -1;
		for (int i = 0; i < GridItems.Count; i++)
		{
			if (ReferenceEquals(GridItems[i], row))
				return i;
		}
		return -1;
	}

	private string GetRowClass(object row, int index)
	{
		return _selectedRows.Contains(index) ? "row-selected" : "";
	}

	private async Task OnMudRowClick(DataGridRowClickEventArgs<object> args)
	{
		await OnRowClick(args.Item, args.MouseEventArgs);
	}

	private async Task OnRowClick(object row, MouseEventArgs e)
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
		await OnRowSelected.InvokeAsync(row);
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
			var row = GridItems[rowIndex];
			var values = Result.ColumnNames.Select(col =>
			{
				var cellValue = GetCellValue(row, col);
				return cellValue?.ToString() ?? "NULL";
			});
			tsv.AppendLine(string.Join("\t", values));
		}

		await ClipboardService.CopyToClipboardAsync(tsv.ToString());
	}
}

#nullable restore
