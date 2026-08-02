using LinqStudio.Abstractions.Models;

namespace LinqStudio.Abstractions;

/// <summary>
/// Generates EF Core DbContext and model C# source code from a live database schema.
/// </summary>
public interface IDbContextGenerator
{
	/// <summary>
	/// Introspects the database via <paramref name="generator"/> and produces C# source files
	/// for the DbContext and all entity models.
	/// </summary>
	Task<DbContextGeneratorResult> GenerateAsync(IDatabaseQueryGenerator generator, CancellationToken cancellationToken = default);

	/// <summary>
	/// Generates source code using project-defined relationships in addition to physical
	/// database relationships. The default implementation preserves compatibility with
	/// existing generators that only understand database metadata.
	/// </summary>
	Task<DbContextGeneratorResult> GenerateAsync(
		IDatabaseQueryGenerator generator,
		IReadOnlyList<ICustomRelationship> customRelationships,
		CancellationToken cancellationToken = default)
		=> GenerateAsync(generator, cancellationToken);
}
