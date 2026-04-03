using LinqStudio.Abstractions;
using LinqStudio.Abstractions.Models;
using LinqStudio.Core.Models;
using LinqStudio.Core.Services;
using LinqStudio.Core.Settings;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace LinqStudio.Core.Tests;

public class QueryExecutionServiceTests
{
	#region QueryExecutionResult Static Factory Tests

	[Fact]
	public void QueryExecutionResult_Empty_HasNoRowsNoColumns()
	{
		// Arrange
		var elapsed = TimeSpan.FromSeconds(1);

		// Act
		var result = QueryExecutionResult.Empty(elapsed);

		// Assert
		Assert.NotNull(result);
		Assert.Empty(result.Rows);
		Assert.Empty(result.ColumnNames);
		Assert.True(result.Success);
		Assert.Equal(elapsed, result.Elapsed);
		Assert.Null(result.ErrorMessage);
		Assert.False(result.IsCompileError);
	}

	[Fact]
	public void QueryExecutionResult_FromError_IsCompileError()
	{
		// Arrange
		var errorMessage = "syntax error";
		var elapsed = TimeSpan.Zero;

		// Act
		var result = QueryExecutionResult.FromError(errorMessage, isCompileError: true, elapsed);

		// Assert
		Assert.NotNull(result);
		Assert.False(result.Success);
		Assert.True(result.IsCompileError);
		Assert.Equal(errorMessage, result.ErrorMessage);
		Assert.Equal(elapsed, result.Elapsed);
		Assert.Empty(result.Rows);
		Assert.Empty(result.ColumnNames);
	}

	[Fact]
	public void QueryExecutionResult_FromError_IsRuntimeError()
	{
		// Arrange
		var errorMessage = "null reference";
		var elapsed = TimeSpan.FromMilliseconds(123);

		// Act
		var result = QueryExecutionResult.FromError(errorMessage, isCompileError: false, elapsed);

		// Assert
		Assert.NotNull(result);
		Assert.False(result.Success);
		Assert.False(result.IsCompileError);
		Assert.Equal(errorMessage, result.ErrorMessage);
		Assert.Equal(elapsed, result.Elapsed);
		Assert.Empty(result.Rows);
		Assert.Empty(result.ColumnNames);
	}

	[Fact]
	public void QueryExecutionResult_FromError_PreservesComplexErrorMessage()
	{
		// Arrange
		var errorMessage = "Error on line 5: Expected ';' but found '}'.\nSyntax error detected.";
		var elapsed = TimeSpan.FromMilliseconds(50);

		// Act
		var result = QueryExecutionResult.FromError(errorMessage, isCompileError: true, elapsed);

		// Assert
		Assert.Equal(errorMessage, result.ErrorMessage);
		Assert.True(result.IsCompileError);
	}

	[Fact]
	public void QueryExecutionResult_Success_PropertyIsTrueWhenNoError()
	{
		// Arrange & Act
		var result = QueryExecutionResult.Empty(TimeSpan.FromSeconds(2));

		// Assert
		Assert.True(result.Success);
		Assert.Null(result.ErrorMessage);
	}

	[Fact]
	public void QueryExecutionResult_Success_PropertyIsFalseWhenErrorExists()
	{
		// Arrange & Act
		var result = QueryExecutionResult.FromError("error", isCompileError: false, TimeSpan.Zero);

		// Assert
		Assert.False(result.Success);
		Assert.NotNull(result.ErrorMessage);
	}

	#endregion

	#region ExecuteQueryAsync Tests (Public Method - Requires Project Parameter)

	[Fact]
	public async Task ExecuteQueryAsync_WithNoConnectionString_ReturnsErrorResult()
	{
		// Arrange
		var mockGenerator = new MockDbContextGenerator();
		var settings = CreateSettings();
		var service = new QueryExecutionService(mockGenerator, new RoslynWorkspaceService(), settings);
		var query = "context.Users.ToList()";
		var connection = new ServerConnection { ConnectionString = null };

		// Act
		var result = await service.ExecuteQueryAsync(query, connection);

		// Assert
		Assert.NotNull(result);
		Assert.False(result.Success);
		Assert.False(result.IsCompileError); // Runtime error, not compile error
		Assert.NotNull(result.ErrorMessage);
		Assert.Contains("No database connection configured", result.ErrorMessage);
		Assert.True(result.Elapsed >= TimeSpan.Zero);
	}

	[Fact]
	public async Task ExecuteQueryAsync_WithEmptyConnectionString_ReturnsErrorResult()
	{
		// Arrange
		var mockGenerator = new MockDbContextGenerator();
		var settings = CreateSettings();
		var service = new QueryExecutionService(mockGenerator, new RoslynWorkspaceService(), settings);
		var query = "context.Users.ToList()";
		var connection = new ServerConnection { ConnectionString = "" };

		// Act
		var result = await service.ExecuteQueryAsync(query, connection);

		// Assert
		Assert.NotNull(result);
		Assert.False(result.Success);
		Assert.NotNull(result.ErrorMessage);
		Assert.Contains("No database connection configured", result.ErrorMessage);
	}

	[Fact]
	public async Task ExecuteQueryAsync_WithCancellation_ReturnsErrorResult()
	{
		// Arrange
		var mockGenerator = new MockDbContextGenerator();
		var settings = CreateSettings();
		var service = new QueryExecutionService(mockGenerator, new RoslynWorkspaceService(), settings);
		var connection = new ServerConnection { ConnectionString = "Server=test" };
		using var cts = new CancellationTokenSource();
		cts.Cancel();

		// Act
		var result = await service.ExecuteQueryAsync("query", connection, cts.Token);

		// Assert - Returns error result when cancelled
		Assert.NotNull(result);
		Assert.False(result.Success);
		Assert.NotNull(result.ErrorMessage);
	}

	#endregion

	#region Integration Notes

	// Note: Full integration testing of QueryExecutionService.ExecuteQueryAsync() requires:
	// 1. Real DbContext generation (IDbContextGenerator implementation)
	// 2. Database connection (SQLite in-memory or Testcontainers)
	// 3. Roslyn compilation infrastructure
	// 4. EF Core model generation
	//
	// These tests would belong in an integration test suite, not unit tests.
	// Key scenarios to test in integration tests:
	// - ExecuteQueryAsync_WithValidQuery_ReturnsResults
	// - ExecuteQueryAsync_WithSyntaxError_ReturnsCompileError
	// - ExecuteQueryAsync_WithRuntimeError_ReturnsRuntimeError
	// - ExecuteQueryAsync_WithTimeout_CancelsQuery
	// - ExecuteQueryAsync_WithEmptyResults_ReturnsEmptyResult

	#endregion

	#region Helper Methods

	private static IOptionsMonitor<QueryExecutionSettings> CreateSettings(int timeoutSeconds = 30)
	{
		var settings = new QueryExecutionSettings { TimeoutSeconds = timeoutSeconds };
		return new OptionsMonitorWrapper(settings);
	}

	#endregion

	#region Test Helper Classes

	/// <summary>
	/// Mock IDbContextGenerator for testing QueryExecutionService constructor and DI.
	/// </summary>
	private class MockDbContextGenerator : IDbContextGenerator
	{
		public Task<DbContextGeneratorResult> GenerateAsync(IDatabaseQueryGenerator generator, CancellationToken cancellationToken = default)
		{
			// Not used in these unit tests - only constructor validation
			throw new NotImplementedException("Mock generator for constructor tests only");
		}
	}

	/// <summary>
	/// Simple IOptionsMonitor implementation for testing.
	/// </summary>
	private class OptionsMonitorWrapper : IOptionsMonitor<QueryExecutionSettings>
	{
		private readonly QueryExecutionSettings _settings;

		public OptionsMonitorWrapper(QueryExecutionSettings settings)
		{
			_settings = settings;
		}

		public QueryExecutionSettings CurrentValue => _settings;

		public QueryExecutionSettings Get(string? name) => _settings;

		public IDisposable? OnChange(Action<QueryExecutionSettings, string?> listener) => null;
	}

	#endregion
}

