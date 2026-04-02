using LinqStudio.Core.Models;
using LinqStudio.Core.Extensions;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace LinqStudio.Core.Services;

/// <summary>
/// Service responsible for query file I/O operations.
/// Handles loading, saving, and managing individual query files (.linq.query).
/// </summary>
public class QueryService
{
	private const string QueryFileExtension = ".linq.query";
	private readonly ILogger<QueryService>? _logger;

	/// <summary>
	/// Initializes a new instance of <see cref="QueryService"/>.
	/// </summary>
	/// <param name="logger">Optional logger for file-operation diagnostics. Warnings are emitted per failed file rather than aborting a bulk load.</param>
	public QueryService(ILogger<QueryService>? logger = null)
	{
		_logger = logger;
	}

	/// <summary>
	/// Gets the directory path for queries associated with a project.
	/// </summary>
	/// <param name="projectFilePath">Path to the project file (e.g. <c>MyProject.linq</c>).</param>
	/// <returns>
	/// Path to the queries directory (e.g. <c>MyProject.linq.queries/</c>).
	/// </returns>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="projectFilePath"/> is <see langword="null"/> or empty.
	/// </exception>
	public string GetQueriesDirectory(string projectFilePath)
	{
		if (string.IsNullOrEmpty(projectFilePath))
		{
			throw new ArgumentException("Project file path cannot be null or empty.", nameof(projectFilePath));
		}

		// Create queries directory based on project file
		// Example: MyProject.linq -> MyProject.linq.queries/
		return $"{projectFilePath}.queries";
	}

	/// <summary>
	/// Gets the file path for a specific query.
	/// </summary>
	/// <param name="projectFilePath">Path to the project file.</param>
	/// <param name="queryId">Query identifier.</param>
	/// <returns>Absolute or relative path to the query file (<c>{queryId}.linq.query</c>).</returns>
	public string GetQueryFilePath(string projectFilePath, Guid queryId)
	{
		var queriesDir = GetQueriesDirectory(projectFilePath);
		return Path.Combine(queriesDir, $"{queryId}{QueryFileExtension}");
	}

	/// <summary>
	/// Loads all queries for a project from its queries directory.
	/// </summary>
	/// <param name="projectFilePath">Path to the project file.</param>
	/// <returns>
	/// List of deserialized <see cref="SavedQuery"/> objects. Returns an empty list when the
	/// queries directory does not yet exist. Individual files that fail to deserialize are
	/// skipped with a warning log rather than aborting the entire load.
	/// </returns>
	public async Task<List<SavedQuery>> LoadQueriesAsync(string projectFilePath)
	{
		var queriesDir = GetQueriesDirectory(projectFilePath);

		if (!Directory.Exists(queriesDir))
		{
			return [];
		}

		var queries = new List<SavedQuery>();
		var queryFiles = Directory.GetFiles(queriesDir, $"*{QueryFileExtension}");

		foreach (var filePath in queryFiles)
		{
			try
			{
				await using var stream = File.OpenRead(filePath);
				var query = await JsonSerializer.DeserializeAsync<SavedQuery>(stream, JsonSerializerOptions.Default);

				if (query is not null)
				{
					queries.Add(query);
				}
			}
			catch (Exception ex)
			{
				// Log error but continue loading other queries
				_logger?.LogWarning(ex, "Failed to load query from {FilePath}", filePath);
			}
		}

		return queries;
	}

	/// <summary>
	/// Saves a query to its individual file inside the project's queries directory.
	/// </summary>
	/// <param name="projectFilePath">Path to the project file.</param>
	/// <param name="query">Query to save.</param>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="query"/> is <see langword="null"/>.</exception>
	/// <exception cref="InvalidOperationException">Thrown when <paramref name="query"/> has a <see cref="SavedQuery.Id"/> of <see cref="Guid.Empty"/>.</exception>
	/// <remarks>
	/// <c>Directory.CreateDirectory</c> is idempotent — it creates missing intermediate
	/// directories and is a no-op when the directory already exists, replacing the old
	/// TOCTOU-prone <c>if (!Directory.Exists) Directory.CreateDirectory</c> guard.
	/// Uses an atomic write-then-replace pattern (temp file + <see cref="File.Move"/>) so
	/// a crash during serialization never corrupts the existing query file.
	/// </remarks>
	public async Task SaveQueryAsync(string projectFilePath, SavedQuery query)
	{
		ArgumentNullException.ThrowIfNull(query);

		if (query.Id == Guid.Empty)
		{
			throw new InvalidOperationException("Cannot save query with invalid ID (Guid.Empty).");
		}

		var queriesDir = GetQueriesDirectory(projectFilePath);

		Directory.CreateDirectory(queriesDir);

		var queryFilePath = GetQueryFilePath(projectFilePath, query.Id);

		// Write to temporary file first (atomic save pattern)
		// Use a unique name to avoid conflicts with concurrent saves
		var tempFilePath = $"{queryFilePath}.{Guid.NewGuid():N}.tmp";
		try
		{
			await using (var stream = File.Create(tempFilePath))
			{
				await JsonSerializer.SerializeAsync(stream, query, JsonSerializerOptions.Indented);
			}

			// Only replace original if serialization succeeded
			File.Move(tempFilePath, queryFilePath, overwrite: true);
		}
		catch
		{
			// Clean up temp file on failure
			if (File.Exists(tempFilePath))
			{
				try
				{
					File.Delete(tempFilePath);
				}
				catch
				{
					// Ignore cleanup failures
				}
			}
			// Re-throw original exception
			throw;
		}
	}

	/// <summary>
	/// Deletes a query file from the project's queries directory.
	/// </summary>
	/// <param name="projectFilePath">Path to the project file.</param>
	/// <param name="queryId">Identifier of the query to delete. No-op if the file does not exist.</param>
	public void DeleteQuery(string projectFilePath, Guid queryId)
	{
		var queryFilePath = GetQueryFilePath(projectFilePath, queryId);

		if (File.Exists(queryFilePath))
		{
			File.Delete(queryFilePath);
		}
	}

	/// <summary>
	/// Deletes all query files for a project.
	/// </summary>
	/// <param name="projectFilePath">Path to the project file.</param>
	public void DeleteAllQueries(string projectFilePath)
	{
		var queriesDir = GetQueriesDirectory(projectFilePath);

		if (Directory.Exists(queriesDir))
		{
			Directory.Delete(queriesDir, recursive: true);
		}
	}

	/// <summary>
	/// Checks if a query file exists.
	/// </summary>
	/// <param name="projectFilePath">Path to the project file.</param>
	/// <param name="queryId">Query identifier.</param>
	/// <returns>True if the query file exists.</returns>
	public bool QueryExists(string projectFilePath, Guid queryId)
	{
		var queryFilePath = GetQueryFilePath(projectFilePath, queryId);
		return File.Exists(queryFilePath);
	}

	/// <summary>
	/// Loads a query from a specific file path (standalone / open-from-disk mode).
	/// </summary>
	/// <param name="filePath">Path to the query file.</param>
	/// <returns>
	/// The deserialized <see cref="SavedQuery"/> with <see cref="SavedQuery.FilePath"/> set,
	/// or <see langword="null"/> if the file does not exist or cannot be deserialized.
	/// </returns>
	public async Task<SavedQuery?> LoadQueryFromFileAsync(string filePath)
	{
		if (!File.Exists(filePath))
		{
			return null;
		}

		try
		{
			await using var stream = File.OpenRead(filePath);
			var query = await JsonSerializer.DeserializeAsync<SavedQuery>(stream, JsonSerializerOptions.Default);

			if (query is not null)
			{
				// Store the file path in the query
				query.FilePath = filePath;
			}

			return query;
		}
		catch (Exception ex)
		{
			_logger?.LogWarning(ex, "Failed to load query from {FilePath}", filePath);
			return null;
		}
	}

	/// <summary>
	/// Saves a query to a specific file path (standalone / open-from-disk mode).
	/// </summary>
	/// <param name="filePath">Absolute or relative path where the query file should be written.</param>
	/// <param name="query">Query to save.</param>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="query"/> is <see langword="null"/>.</exception>
	/// <exception cref="InvalidOperationException">Thrown when <paramref name="query"/> has a <see cref="SavedQuery.Id"/> of <see cref="Guid.Empty"/>.</exception>
	/// <remarks>
	/// <c>Directory.CreateDirectory</c> is idempotent — it creates any missing intermediate
	/// directories and is a no-op when they already exist, replacing the old TOCTOU-prone
	/// <c>if (!Directory.Exists) Directory.CreateDirectory</c> guard.
	/// Uses an atomic write-then-replace pattern (temp file + <see cref="File.Move"/>) so
	/// a crash during serialization never corrupts the target file. On success, sets
	/// <see cref="SavedQuery.FilePath"/> to <paramref name="filePath"/>.
	/// </remarks>
	public async Task SaveQueryToFileAsync(string filePath, SavedQuery query)
	{
		ArgumentNullException.ThrowIfNull(query);

		if (query.Id == Guid.Empty)
		{
			throw new InvalidOperationException("Cannot save query with invalid ID (Guid.Empty).");
		}

		var directory = Path.GetDirectoryName(filePath);
		if (!string.IsNullOrEmpty(directory))
			Directory.CreateDirectory(directory);

		// Write to temporary file first (atomic save pattern)
		// Use a unique name to avoid conflicts with concurrent saves
		var tempFilePath = $"{filePath}.{Guid.NewGuid():N}.tmp";
		try
		{
			await using (var stream = File.Create(tempFilePath))
			{
				await JsonSerializer.SerializeAsync(stream, query, JsonSerializerOptions.Indented);
			}

			// Only replace original if serialization succeeded
			File.Move(tempFilePath, filePath, overwrite: true);
			
			// Update the query's file path
			query.FilePath = filePath;
		}
		catch
		{
			// Clean up temp file on failure
			if (File.Exists(tempFilePath))
			{
				try
				{
					File.Delete(tempFilePath);
				}
				catch
				{
					// Ignore cleanup failures
				}
			}
			// Re-throw original exception
			throw;
		}
	}
}
