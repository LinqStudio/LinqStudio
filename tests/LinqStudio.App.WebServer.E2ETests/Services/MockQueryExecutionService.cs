using LinqStudio.Abstractions.Models;
using LinqStudio.Core.Models;
using LinqStudio.Core.Services;

namespace LinqStudio.App.WebServer.E2ETests.Services;

/// <summary>
/// Mock implementation of IQueryExecutionService for E2E tests.
/// Provides a configurable delay to allow Blazor's loading state to be visible to Playwright,
/// and a configurable result to test different UI states without a real database.
/// </summary>
public class MockQueryExecutionService : IQueryExecutionService, IQueryExecutionServiceFactory
{
	private QueryExecutionResult? _nextResult;
	private QueryExecutionResult? _entityResult;
	private IReadOnlySet<string> _editableColumns = new HashSet<string>(StringComparer.Ordinal);
	private readonly object _lock = new();

	/// <summary>
	/// Delay before returning the result. Set long enough (≥300ms) for Playwright to catch
	/// the loading state before execution completes. Default is 600ms.
	/// </summary>
	public TimeSpan SimulatedDelay { get; set; } = TimeSpan.FromMilliseconds(600);

	/// <summary>
	/// Pre-configure what the next call to ExecuteQueryAsync should return.
	/// If not set, returns a generic "no database" error result.
	/// Consumed once (reset after use).
	/// </summary>
	public void SetNextResult(QueryExecutionResult result)
	{
		lock (_lock)
		{
			_nextResult = result;
			_entityResult = null;
			_editableColumns = new HashSet<string>(StringComparer.Ordinal);
		}
	}

	public void SetNextEntityResult(
		QueryExecutionResult result,
		IReadOnlySet<string> editableColumns)
	{
		lock (_lock)
		{
			_nextResult = result;
			_entityResult = result;
			_editableColumns = editableColumns;
			SaveChangesCallCount = 0;
		}
	}

	public int SaveChangesCallCount { get; private set; }

	public void ResetSaveChangesCallCount()
		=> SaveChangesCallCount = 0;

	public async Task<QueryExecutionResult> ExecuteQueryAsync(
		string userQuery,
		Project project,
		CancellationToken cancellationToken = default)
	{
		// Real async delay so Blazor can render the IsExecuting=true state before we return.
		// This is critical: without a true async yield, Blazor batches the state change
		// and the loading indicator is never visible to Playwright.
		await Task.Delay(SimulatedDelay, cancellationToken);

		lock (_lock)
		{
			if (_nextResult is not null)
			{
				var result = _nextResult;
				_nextResult = null;
				return result;
			}
		}

		return QueryExecutionResult.FromError(
			"No database configured (test environment)",
			isCompileError: false,
			elapsed: SimulatedDelay);
	}

	public IQueryExecutionService Create() => this;

	public bool IsEntityResult(QueryExecutionResult result)
		=> ReferenceEquals(result, _entityResult);

	public IReadOnlySet<string> GetEditableColumns(QueryExecutionResult result)
		=> ReferenceEquals(result, _entityResult)
			? _editableColumns
			: new HashSet<string>();

	public void UpdateEntityProperty(
		object entity,
		string propertyName,
		string? value)
	{
		throw new InvalidOperationException("Entity editing is not supported by the mock service.");
	}

	public Task SaveChangesAsync(CancellationToken cancellationToken = default)
	{
		SaveChangesCallCount++;
		return Task.CompletedTask;
	}

	public void Dispose()
	{
	}

	public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
