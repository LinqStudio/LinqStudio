using LinqStudio.Core.Extensions;
using LinqStudio.Core.Models;
using System.Text.Json;

namespace LinqStudio.Core.Services;

/// <summary>
/// Service responsible for query file I/O operations.
/// Handles loading, saving, and managing individual query files (.linq.query).
/// </summary>
public class QueryService
{
	private const string QueryFileExtension = ".linq.query";

	/// <summary>
	/// Gets the directory path for queries associated with a project.
	/// </summary>
	/// <param name="projectFilePath">Path to the project file.</param>
	/// <returns>Path to the queries directory.</returns>
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
	/// <returns>Path to the query file.</returns>
	public string GetQueryFilePath(string projectFilePath, Guid queryId)
	{
		var queriesDir = GetQueriesDirectory(projectFilePath);
		return Path.Combine(queriesDir, $"{queryId}{QueryFileExtension}");
	}

	/// <summary>
	/// Loads all queries for a project.
	/// </summary>
	/// <param name="projectFilePath">Path to the project file.</param>
	/// <returns>List of saved queries.</returns>
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
				Console.WriteLine($"Warning: Failed to load query from {filePath}: {ex.Message}");
			}
		}

		return queries;
	}

	/// <summary>
	/// Saves a query to its individual file.
	/// </summary>
	/// <param name="projectFilePath">Path to the project file.</param>
	/// <param name="query">Query to save.</param>
	public async Task SaveQueryAsync(string projectFilePath, SavedQuery query)
	{
		ArgumentNullException.ThrowIfNull(query);

		if (query.Id == Guid.Empty)
		{
			throw new InvalidOperationException("Cannot save query with invalid ID (Guid.Empty).");
		}

		var queriesDir = GetQueriesDirectory(projectFilePath);

		// Ensure queries directory exists
		if (!Directory.Exists(queriesDir))
		{
			Directory.CreateDirectory(queriesDir);
		}

		var queryFilePath = GetQueryFilePath(projectFilePath, query.Id);

		// Write to temporary file first (atomic save pattern)
		var tempFilePath = $"{queryFilePath}.tmp";
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
	/// Deletes a query file.
	/// </summary>
	/// <param name="projectFilePath">Path to the project file.</param>
	/// <param name="queryId">Query identifier to delete.</param>
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
	/// Loads a query from a specific file path (standalone mode).
	/// </summary>
	/// <param name="filePath">Path to the query file.</param>
	/// <returns>The loaded query.</returns>
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
			Console.WriteLine($"Warning: Failed to load query from {filePath}: {ex.Message}");
			return null;
		}
	}

	/// <summary>
	/// Saves a query to a specific file path (standalone mode).
	/// </summary>
	/// <param name="filePath">Path where to save the query file.</param>
	/// <param name="query">Query to save.</param>
	public async Task SaveQueryToFileAsync(string filePath, SavedQuery query)
	{
		ArgumentNullException.ThrowIfNull(query);

		if (query.Id == Guid.Empty)
		{
			throw new InvalidOperationException("Cannot save query with invalid ID (Guid.Empty).");
		}

		// Ensure directory exists
		var directory = Path.GetDirectoryName(filePath);
		if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
		{
			Directory.CreateDirectory(directory);
		}

		// Write to temporary file first (atomic save pattern)
		var tempFilePath = $"{filePath}.tmp";
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
