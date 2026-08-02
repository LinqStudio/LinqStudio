using LinqStudio.Abstractions.Models;
using LinqStudio.Core.CodeGeneration;

namespace LinqStudio.Blazor.Components.Dialogs;

internal static class RelationshipKeyPairDetector
{
	public static IReadOnlyList<RelationshipKeyPair> Detect(
		DatabaseTableDetail dependent,
		DatabaseTableDetail principal)
	{
		var foreignKeys = dependent.ForeignKeys
			.Where(foreignKey => IsSameTable(foreignKey.ReferencedTable, principal))
			.Where(foreignKey =>
				dependent.Columns.Any(column => column.Name.Equals(foreignKey.ColumnName, StringComparison.OrdinalIgnoreCase))
				&& principal.Columns.Any(column => column.Name.Equals(foreignKey.ReferencedColumn, StringComparison.OrdinalIgnoreCase)))
			.Select(foreignKey => new RelationshipKeyPair
			{
				DependentColumn = dependent.Columns.First(column =>
					column.Name.Equals(foreignKey.ColumnName, StringComparison.OrdinalIgnoreCase)).Name,
				PrincipalColumn = principal.Columns.First(column =>
					column.Name.Equals(foreignKey.ReferencedColumn, StringComparison.OrdinalIgnoreCase)).Name,
			})
			.ToList();

		if (foreignKeys.Count > 0)
			return foreignKeys;

		var principalKeys = principal.Columns
			.Where(column => column.IsPrimaryKey)
			.ToList();

		if (principalKeys.Count == 0)
		{
			var idColumn = principal.Columns.FirstOrDefault(column =>
				column.Name.Equals("Id", StringComparison.OrdinalIgnoreCase));
			if (idColumn is not null)
				principalKeys.Add(idColumn);
		}

		var principalTypeName = CodeGenerationNaming.Singularize(
			CodeGenerationNaming.ToPascalCase(CodeGenerationNaming.ExtractTableName(principal.FullName)));

		var result = new List<RelationshipKeyPair>();
		foreach (var principalKey in principalKeys)
		{
			var dependentColumn = FindDependentColumn(dependent, principalTypeName, principalKey);
			if (dependentColumn is null)
				return [];

			result.Add(new RelationshipKeyPair
			{
				DependentColumn = dependentColumn.Name,
				PrincipalColumn = principalKey.Name,
			});
		}

		return result;
	}

	private static TableColumn? FindDependentColumn(
		DatabaseTableDetail dependent,
		string principalTypeName,
		TableColumn principalKey)
	{
		var expectedNames = new[]
		{
			$"{principalTypeName}{CodeGenerationNaming.ToPascalCase(principalKey.Name)}",
			CodeGenerationNaming.ToPascalCase(principalKey.Name),
		};

		return dependent.Columns
			.Where(column => column.GenericType == principalKey.GenericType)
			.OrderBy(column => Array.FindIndex(
				expectedNames,
				expectedName => column.Name.Equals(expectedName, StringComparison.OrdinalIgnoreCase)))
			.FirstOrDefault(column => expectedNames.Any(
				expectedName => column.Name.Equals(expectedName, StringComparison.OrdinalIgnoreCase)));
	}

	private static bool IsSameTable(string referencedTable, DatabaseTableDetail principal)
		=> referencedTable.Equals(principal.FullName, StringComparison.OrdinalIgnoreCase)
			|| CodeGenerationNaming.ExtractTableName(referencedTable)
				.Equals(principal.Name, StringComparison.OrdinalIgnoreCase);
}
