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

	private bool IsDateOnlyProperty
		=> typeof(TProperty) == typeof(DateOnly)
			|| Nullable.GetUnderlyingType(typeof(TProperty)) == typeof(DateOnly);

	private bool IsDateTimeProperty
		=> typeof(TProperty) == typeof(DateTime)
			|| Nullable.GetUnderlyingType(typeof(TProperty)) == typeof(DateTime);

	private bool IsTimeSpanProperty
		=> typeof(TProperty) == typeof(TimeSpan)
			|| Nullable.GetUnderlyingType(typeof(TProperty)) == typeof(TimeSpan);

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

	private DateTime? GetDateOnlyValue(object item)
		=> PropertyInfo.GetValue(item) is DateOnly value
			? value.ToDateTime(TimeOnly.MinValue)
			: null;

	private DateTime? GetDateValue(object item)
		=> PropertyInfo.GetValue(item) is DateTime value ? value : null;

	private TimeSpan? GetTimeValue(object item)
		=> PropertyInfo.GetValue(item) switch
		{
			DateTime value => value.TimeOfDay,
			TimeSpan value => value,
			_ => null
		};

	private void OnDateOnlyChanged(object item, DateTime? date)
	{
		if (date is null)
		{
			if (Nullable.GetUnderlyingType(typeof(TProperty)) is not null)
				PropertyInfo.SetValue(item, null);
			return;
		}

		SetValue(item, DateOnly.FromDateTime(date.Value));
	}

	private void OnDateChanged(object item, DateTime? date)
	{
		if (date is null)
		{
			if (Nullable.GetUnderlyingType(typeof(TProperty)) is not null)
				PropertyInfo.SetValue(item, null);
			return;
		}

		var currentTime = GetDateValue(item)?.TimeOfDay ?? TimeSpan.Zero;
		SetValue(item, date.Value.Date.Add(currentTime));
	}

	private void OnTimeChanged(object item, TimeSpan? time)
	{
		if (time is null)
		{
			if (Nullable.GetUnderlyingType(typeof(TProperty)) is not null)
				PropertyInfo.SetValue(item, null);
			return;
		}

		var currentDate = GetDateValue(item)?.Date ?? DateTime.Today;
		SetValue(item, currentDate.Add(time.Value));
	}

	private void OnTimeSpanChanged(object item, TimeSpan? time)
	{
		if (time is null)
		{
			if (Nullable.GetUnderlyingType(typeof(TProperty)) is not null)
				PropertyInfo.SetValue(item, null);
			return;
		}

		SetValue(item, time.Value);
	}

	private void SetValue(object item, object value)
		=> PropertyInfo.SetValue(item, value);
}
