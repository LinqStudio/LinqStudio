namespace LinqStudio.Abstractions.Models;

/// <summary>
/// Holds the generated C# source files produced by <see cref="Abstractions.IDbContextGenerator"/>.
/// </summary>
/// <param name="ModelFiles">Filename → source code for each entity model class.</param>
/// <param name="DbContextCode">Source code for the generated DbContext class.</param>
/// <param name="ContextTypeName">Simple class name of the generated DbContext (e.g. "AppDbContext").</param>
/// <param name="Namespace">Namespace used across all generated files.</param>
public record DbContextGeneratorResult(
	Dictionary<string, string> ModelFiles,
	string DbContextCode,
	string ContextTypeName,
	string Namespace
);
