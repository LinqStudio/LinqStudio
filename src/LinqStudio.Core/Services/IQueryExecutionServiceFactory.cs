namespace LinqStudio.Core.Services;

/// <summary>
/// Creates an isolated query execution service for a single editor panel.
/// </summary>
public interface IQueryExecutionServiceFactory
{
	/// <summary>
	/// Creates a query execution service whose generated assembly and DbContext
	/// can remain alive until the next execution or disposal.
	/// </summary>
	IQueryExecutionService Create();
}
