using Microsoft.AspNetCore.Components;
using System.Linq.Expressions;
using System.Reflection;

namespace LinqStudio.Blazor.Components;

public partial class DynamicPropertyColumn<TProperty> : ComponentBase
{
	[Parameter, EditorRequired]
	public string ColumnName { get; set; } = string.Empty;

	[Parameter, EditorRequired]
	public PropertyInfo PropertyInfo { get; set; } = null!;

	[Parameter]
	public bool IsEditable { get; set; }

	private Expression<Func<object, TProperty>> Property { get; set; } = null!;

	private bool IsDateTimeProperty
		=> typeof(TProperty) == typeof(DateTime)
			|| Nullable.GetUnderlyingType(typeof(TProperty)) == typeof(DateTime);

	protected override void OnParametersSet()
	{
		// Generate a fake `Property="x => x.PropertyName"` expression tree
		var item = Expression.Parameter(typeof(object), "x");
		Property = Expression.Lambda<Func<object, TProperty>>(
			Expression.Property(
				Expression.Convert(item, PropertyInfo.DeclaringType!),
				PropertyInfo),
			item);
	}

	private TProperty GetValue(object item)
		=> (TProperty)(PropertyInfo.GetValue(item) ?? default(TProperty)!);

	private Task OnValueChanged(object item, TProperty value)
	{
		PropertyInfo.SetValue(item, value);
		return Task.CompletedTask;
	}
}
