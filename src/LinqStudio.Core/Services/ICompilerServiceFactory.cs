using LinqStudio.Core.Models;

namespace LinqStudio.Core.Services;

/// <summary>
/// Factory contract for creating and fully initializing <see cref="CompilerService"/> instances.
/// </summary>
/// <remarks>
/// Creating a <see cref="CompilerService"/> involves initializing a Roslyn
/// <c>AdhocWorkspace</c>, loading EF Core model files as in-memory documents,
/// and building the in-memory compilation — an async, potentially expensive operation.
/// Callers should retain the returned instance for the lifetime of a user session or
/// page rather than calling the factory repeatedly.
/// </remarks>
public interface ICompilerServiceFactory
{
    /// <summary>
    /// Creates and initializes a <see cref="CompilerService"/> backed by the built-in
    /// demo model (a single <c>Person</c> entity and an in-memory <c>TestDbContext</c>).
    /// </summary>
    /// <returns>
    /// A fully initialized <see cref="CompilerService"/> ready to serve completions
    /// against the demo schema.
    /// </returns>
    Task<CompilerService> CreateAsync();

    /// <summary>
    /// Creates and initializes a <see cref="CompilerService"/> from the given project's
    /// live database schema. Falls back to <see cref="CreateAsync"/> (demo model) when
    /// the project has no database generator configured or when no
    /// <see cref="LinqStudio.Abstractions.IDbContextGenerator"/> is registered in DI.
    /// </summary>
    /// <param name="project">The project whose database schema drives EF Core code generation.</param>
    /// <param name="cancellationToken">Token to cancel the schema generation step.</param>
    /// <returns>
    /// A fully initialized <see cref="CompilerService"/> reflecting the project's schema,
    /// or the demo-model service if no database is configured.
    /// </returns>
    Task<CompilerService> CreateFromProjectAsync(Project project, CancellationToken cancellationToken = default);
}
