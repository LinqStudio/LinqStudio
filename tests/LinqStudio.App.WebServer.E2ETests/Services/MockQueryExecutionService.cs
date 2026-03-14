using LinqStudio.Abstractions.Models;
using LinqStudio.Core.Models;
using LinqStudio.Core.Services;

namespace LinqStudio.App.WebServer.E2ETests.Services;

/// <summary>
/// Mock implementation of IQueryExecutionService for E2E tests.
/// Provides a configurable delay to allow Blazor's loading state to be visible to Playwright,
/// and a configurable result to test different UI states without a real database.
/// </summary>
public class MockQueryExecutionService : IQueryExecutionService
{
	private QueryExecutionResult? _nextResult;
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
		}
	}

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
}
