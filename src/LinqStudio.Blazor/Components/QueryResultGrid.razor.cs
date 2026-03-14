using LinqStudio.Abstractions.Models;
using Microsoft.AspNetCore.Components;

namespace LinqStudio.Blazor.Components;

public partial class QueryResultGrid : ComponentBase
{
	[Parameter]
	public QueryExecutionResult? Result { get; set; }

	[Parameter]
	public bool IsExecuting { get; set; }

	private string FormatElapsedTime(TimeSpan elapsed)
	{
		if (elapsed.TotalSeconds < 1)
		{
			return $"{elapsed.TotalMilliseconds:F0}ms";
		}
		
		return $"{elapsed.TotalSeconds:F2}s";
	}
}
