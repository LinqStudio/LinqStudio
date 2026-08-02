using LinqStudio.Abstractions;
using LinqStudio.Abstractions.Models;
using LinqStudio.Core.CodeGeneration;
using LinqStudio.Core.Models;

namespace LinqStudio.Core.Services;

/// <summary>
/// Reads database schema metadata and produces C# model and DbContext source code.
/// </summary>
public sealed class DbContextGenerator : IDbContextGenerator
{
	private const string TargetNamespace = "GeneratedModels";
	private const string ContextTypeName = "GeneratedDbContext";

	private readonly GeneratedSchemaBuilder _schemaBuilder = new();
	private readonly EntityModelCodeGenerator _modelGenerator = new();
	private readonly DbContextCodeGenerator _contextGenerator = new();

	public async Task<DbContextGeneratorResult> GenerateAsync(
		IDatabaseQueryGenerator generator,
		CancellationToken cancellationToken = default)
		=> await GenerateAsync(generator, [], cancellationToken);

	public async Task<DbContextGeneratorResult> GenerateAsync(
		IDatabaseQueryGenerator generator,
		IReadOnlyList<ICustomRelationship> customRelationships,
		CancellationToken cancellationToken = default)
	{
		var schema = await _schemaBuilder.BuildAsync(generator, customRelationships, cancellationToken);
		var modelFiles = schema.Tables.ToDictionary(
			table => $"{schema.ClassNameByTableName[table.FullName]}.cs",
			table => _modelGenerator.Generate(table, schema));
		var dbContextCode = _contextGenerator.Generate(schema);

		return new DbContextGeneratorResult(
			modelFiles,
			dbContextCode,
			ContextTypeName,
			TargetNamespace);
	}
}
