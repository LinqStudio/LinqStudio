using LinqStudio.Abstractions;
using LinqStudio.Abstractions.Models;

namespace LinqStudio.Core.Services;

/// <summary>
/// Placeholder implementation of <see cref="IDbContextGenerator"/> used until a real
/// code-generation backend is registered.  Throws if actually invoked.
/// </summary>
internal sealed class NullDbContextGenerator : IDbContextGenerator
{
	public Task<DbContextGeneratorResult> GenerateAsync(IDatabaseQueryGenerator generator, CancellationToken cancellationToken = default)
		=> throw new NotSupportedException(
			"No IDbContextGenerator implementation has been registered. " +
			"Register a real implementation before calling RefreshSchemaAsync with a live database connection.");
}
