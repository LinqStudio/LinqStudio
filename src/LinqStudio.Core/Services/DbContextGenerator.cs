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
	private readonly GeneratedSchemaBuilder _schemaBuilder = new();
	private readonly EntityModelCodeGenerator _modelGenerator = new();
	private readonly DbContextCodeGenerator _contextGenerator = new();

	public async Task<DbContextGeneratorResult> GenerateAsync(
		IDatabaseQueryGenerator generator,
		string databaseName,
		CancellationToken cancellationToken = default)
		=> await GenerateAsync(generator, databaseName, [], cancellationToken);

	public async Task<DbContextGeneratorResult> GenerateAsync(
		IDatabaseQueryGenerator generator,
		string databaseName,
		IReadOnlyList<ICustomRelationship> customRelationships,
		CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(databaseName);

		var contextTypeName = CodeGenerationNaming.GetDbContextTypeName(databaseName);
		var targetNamespace = $"GeneratedModels.{contextTypeName}";
		var schema = await _schemaBuilder.BuildAsync(
			generator,
			databaseName,
			customRelationships,
			cancellationToken);
		var modelFiles = schema.Tables.ToDictionary(
			table => $"{schema.ClassNameByTableName[table.FullName]}.cs",
			table => _modelGenerator.Generate(table, schema, targetNamespace));
		var dbContextCode = _contextGenerator.Generate(schema, targetNamespace, contextTypeName);

		return new DbContextGeneratorResult(
			modelFiles,
			dbContextCode,
			contextTypeName,
			targetNamespace);
	}
}
