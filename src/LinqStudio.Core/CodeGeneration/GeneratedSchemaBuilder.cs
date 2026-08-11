using LinqStudio.Abstractions;
using LinqStudio.Abstractions.Models;
using LinqStudio.Core.Models;

namespace LinqStudio.Core.CodeGeneration;

internal sealed class GeneratedSchemaBuilder
{
	public async Task<GeneratedSchema> BuildAsync(
		IDatabaseQueryGenerator generator,
		string databaseName,
		IReadOnlyList<ICustomRelationship> customRelationships,
		CancellationToken cancellationToken = default)
	{
		var tables = await generator.GetTablesAsync(databaseName, cancellationToken);
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
					targetTableName,
					foreignKey.ColumnName,
					foreignKey.ReferencedColumn,
					KeyPairs: [new GeneratedKeyPair(foreignKey.ColumnName, foreignKey.ReferencedColumn)]));
			}
		}

		foreach (var customRelationship in customRelationships)
		{
			var principalTableName = ResolveTableName(
				customRelationship.PrincipalTable,
				classNameByTableName,
				tableByShortName);
			var dependentTableName = ResolveTableName(
				customRelationship.DependentTable,
				classNameByTableName,
				tableByShortName);

			if (principalTableName is null || dependentTableName is null)
				continue;

			if (!Enum.IsDefined(typeof(RelationshipCardinality), customRelationship.Cardinality))
				continue;

			var keyPairs = (customRelationship.KeyPairs ?? [])
				.Where(pair => !string.IsNullOrWhiteSpace(pair.PrincipalColumn)
					&& !string.IsNullOrWhiteSpace(pair.DependentColumn))
				.Select(pair => new GeneratedKeyPair(pair.DependentColumn, pair.PrincipalColumn))
				.ToList();

			// A project relationship is authoritative for the same table pair and key
			// mapping, so it replaces the inferred physical relationship.
			relationships.RemoveAll(relationship =>
				!relationship.IsCustom
				&& relationship.SourceTableName.Equals(dependentTableName, StringComparison.OrdinalIgnoreCase)
				&& relationship.TargetTableName.Equals(principalTableName, StringComparison.OrdinalIgnoreCase)
				&& (keyPairs.Count == 0
					|| relationship.KeyPairs is null
					|| relationship.KeyPairs.SequenceEqual(keyPairs)));

			relationships.Add(new GeneratedRelationship(
				$"Custom_{Guid.NewGuid():N}",
				dependentTableName,
				principalTableName,
				keyPairs.FirstOrDefault()?.SourceColumnName ?? string.Empty,
				keyPairs.FirstOrDefault()?.TargetColumnName ?? string.Empty,
				(RelationshipCardinality)customRelationship.Cardinality,
				customRelationship.DependentNavigation,
				customRelationship.PrincipalNavigation,
				customRelationship.IsRequired,
				keyPairs,
				IsCustom: true,
				DeleteBehavior: Enum.IsDefined(typeof(RelationshipDeleteBehavior), customRelationship.DeleteBehavior)
					? (RelationshipDeleteBehavior)customRelationship.DeleteBehavior
					: RelationshipDeleteBehavior.NoAction));
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
