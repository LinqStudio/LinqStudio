using LinqStudio.Abstractions;
using LinqStudio.Abstractions.Models;
using LinqStudio.Core.Models;

namespace LinqStudio.Core.CodeGeneration;

internal sealed class GeneratedSchemaBuilder
{
	public async Task<GeneratedSchema> BuildAsync(
		IDatabaseQueryGenerator generator,
		CancellationToken cancellationToken = default)
	{
		var tables = await generator.GetTablesAsync(cancellationToken);
		var tableDetails = new List<DatabaseTableDetail>(tables.Count);

		foreach (var table in tables)
			tableDetails.Add(await generator.GetTableAsync(table, cancellationToken));

		var classNameByTableName = tableDetails.ToDictionary(
			table => table.FullName,
			table => CodeGenerationNaming.ToPascalCase(table.Name),
			StringComparer.OrdinalIgnoreCase);

		var tableByShortName = tableDetails
			.GroupBy(table => table.Name, StringComparer.OrdinalIgnoreCase)
			.ToDictionary(
				group => group.Key,
				group => group.Count() == 1 ? group.Single().FullName : null,
				StringComparer.OrdinalIgnoreCase);

		var relationships = new List<GeneratedRelationship>();
		foreach (var table in tableDetails)
		{
			foreach (var foreignKey in table.ForeignKeys)
			{
				var targetTableName = ResolveTableName(
					foreignKey.ReferencedTable,
					classNameByTableName,
					tableByShortName);

				if (targetTableName is null)
					continue;

				relationships.Add(new GeneratedRelationship(
					foreignKey.Name,
					table.FullName,
					foreignKey.ColumnName,
					targetTableName,
					foreignKey.ReferencedColumn));
			}
		}

		return new GeneratedSchema(tableDetails, classNameByTableName, relationships);
	}

	private static string? ResolveTableName(
		string referencedTable,
		IReadOnlyDictionary<string, string> classNameByTableName,
		IReadOnlyDictionary<string, string?> tableByShortName)
	{
		if (classNameByTableName.ContainsKey(referencedTable))
			return referencedTable;

		var shortName = CodeGenerationNaming.ExtractTableName(referencedTable);
		if (!tableByShortName.TryGetValue(shortName, out var resolvedTableName))
			return null;

		return resolvedTableName;
	}
}
