using Microsoft.AspNetCore.Components;
using MudBlazor;
using System.Linq.Expressions;
using System.Reflection;

namespace LinqStudio.Blazor.Components;

/// <summary>
/// A runtime-typed wrapper around MudBlazor's standard PropertyColumn.
/// </summary>
public class DynamicPropertyColumn<TProperty> : PropertyColumn<object, TProperty>
{
	[Parameter, EditorRequired]
	public string ColumnName { get; set; } = string.Empty;

	[Parameter, EditorRequired]
	public PropertyInfo PropertyInfo { get; set; } = null!;

	[Parameter, EditorRequired]
	public EventCallback<QueryResultGrid.CellChanged> OnCellChanged { get; set; }

	[Parameter]
	public bool IsEditable { get; set; }

	protected override void OnParametersSet()
	{
		Title = ColumnName;
		Editable = IsEditable;

		var item = Expression.Parameter(typeof(object), "x");
		Property = Expression.Lambda<Func<object, TProperty>>(
			Expression.Property(
				Expression.Convert(item, PropertyInfo.DeclaringType!),
				PropertyInfo),
			item);

		base.OnParametersSet();
	}

	protected override void SetProperty(object? item, object? value)
	{
		base.SetProperty(item, value);
		if (item is not null)
			_ = OnCellChanged.InvokeAsync(new QueryResultGrid.CellChanged(item, ColumnName, value?.ToString()));
	}
}
