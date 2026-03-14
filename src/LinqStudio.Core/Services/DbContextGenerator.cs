using System.Text;
using LinqStudio.Abstractions;
using LinqStudio.Abstractions.Models;

namespace LinqStudio.Core.Services;

/// <summary>
/// Reads live database schema via an <see cref="IDatabaseQueryGenerator"/> and produces
/// C# model classes + a <c>GeneratedDbContext</c> suitable for Roslyn IntelliSense.
/// </summary>
public class DbContextGenerator : IDbContextGenerator
{
	private const string TargetNamespace = "GeneratedModels";
	private const string ContextTypeName = "GeneratedDbContext";

	public async Task<DbContextGeneratorResult> GenerateAsync(
		IDatabaseQueryGenerator generator,
		CancellationToken cancellationToken = default)
	{
		var tables = await generator.GetTablesAsync(cancellationToken);

		var tableDetails = new List<DatabaseTableDetail>(tables.Count);
		foreach (var table in tables)
		{
			var detail = await generator.GetTableAsync(table, cancellationToken);
			tableDetails.Add(detail);
		}

		// FullName (schema.table or table) -> PascalCase class name, case-insensitive key
		// Using FullName avoids key collisions when two tables share the same Name across schemas.
		var classNameByTableName = tableDetails.ToDictionary(
			t => t.FullName,
			t => ToPascalCase(t.Name),
			StringComparer.OrdinalIgnoreCase);

		// Short-name lookup used for FK resolution (FK references rarely include schema prefix).
		// If two tables share a name across schemas, the first one wins for FK nav generation.
		var classNameByShortName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		foreach (var t in tableDetails)
		{
			classNameByShortName.TryAdd(t.Name, ToPascalCase(t.Name));
		}

		// parentTableShortName -> list of child class names (for collection nav properties)
		var parentCollections = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
		foreach (var table in tableDetails)
		{
			var childClassName = classNameByTableName[table.FullName];
			foreach (var fk in table.ForeignKeys)
			{
				var refTableName = ExtractTableName(fk.ReferencedTable);
				if (!parentCollections.TryGetValue(refTableName, out var list))
				{
					list = [];
					parentCollections[refTableName] = list;
				}
				list.Add(childClassName);
			}
		}

		var modelFiles = new Dictionary<string, string>(tableDetails.Count);
		foreach (var table in tableDetails)
		{
			var className = classNameByTableName[table.FullName];
			var code = GenerateModel(className, table, classNameByShortName, parentCollections);
			modelFiles[$"{className}.cs"] = code;
		}

		var dbContextCode = GenerateDbContext(tableDetails, classNameByTableName);
		return new DbContextGeneratorResult(modelFiles, dbContextCode, ContextTypeName, TargetNamespace);
	}

	private static string GenerateModel(
		string className,
		DatabaseTableDetail table,
		Dictionary<string, string> classNameByTableName,
		Dictionary<string, List<string>> parentCollections)
	{
		var sb = new StringBuilder();
		sb.AppendLine("using System;");
		sb.AppendLine("using System.Collections.Generic;");
		sb.AppendLine("using System.ComponentModel.DataAnnotations;");
		sb.AppendLine("using System.ComponentModel.DataAnnotations.Schema;");
		sb.AppendLine();
		sb.AppendLine($"namespace {TargetNamespace};");
		sb.AppendLine();
		sb.AppendLine($"public class {className}");
		sb.AppendLine("{");

		foreach (var col in table.Columns)
		{
			var propName = ToPascalCase(col.Name);
			var csType = GetCSharpTypeName(col.GenericType, col.IsNullable);
			bool isStringLike = IsStringLike(col.GenericType);

			if (col.IsPrimaryKey)
			{
				sb.AppendLine("    [Key]");
				if (col.IsIdentity)
					sb.AppendLine("    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]");
			}

			if (isStringLike && !col.IsNullable)
				sb.AppendLine("    [Required]");

			if (col.MaxLength.HasValue && col.MaxLength.Value != -1)
				sb.AppendLine($"    [MaxLength({col.MaxLength.Value})]");

			var initializer = GetInitializer(col.GenericType, col.IsNullable);
			sb.AppendLine($"    public {csType} {propName} {{ get; set; }}{initializer}");
			sb.AppendLine();
		}

		// Reference navigation properties (FK — child side)
		var usedNavNames = new HashSet<string>(StringComparer.Ordinal);
		foreach (var fk in table.ForeignKeys)
		{
			var refTableName = ExtractTableName(fk.ReferencedTable);
			if (!classNameByTableName.TryGetValue(refTableName, out var refClassName))
				continue;

			var navName = Singularize(refClassName);
			if (!usedNavNames.Add(navName))
			{
				// Disambiguate using the FK column name (strip trailing "Id"/"ID")
				var colBase = fk.ColumnName.EndsWith("Id", StringComparison.OrdinalIgnoreCase)
					? fk.ColumnName[..^2]
					: fk.ColumnName;
				navName = ToPascalCase(colBase) + Singularize(refClassName);
				usedNavNames.Add(navName);
			}

			sb.AppendLine($"    public virtual {refClassName}? {navName} {{ get; set; }}");
			sb.AppendLine();
		}

		// Collection navigation properties (FK — parent side)
		if (parentCollections.TryGetValue(table.Name, out var collections))
		{
			var usedCollectionNames = new HashSet<string>(StringComparer.Ordinal);
			foreach (var childClassName in collections)
			{
				var collectionName = Pluralize(childClassName);
				if (!usedCollectionNames.Add(collectionName))
				{
					collectionName = childClassName + "Collection";
					usedCollectionNames.Add(collectionName);
				}

				sb.AppendLine($"    public virtual ICollection<{childClassName}> {collectionName} {{ get; set; }} = [];");
				sb.AppendLine();
			}
		}

		sb.AppendLine("}");
		return sb.ToString();
	}

	private static string GenerateDbContext(
		List<DatabaseTableDetail> tableDetails,
		Dictionary<string, string> classNameByTableName)
	{
		var sb = new StringBuilder();
		sb.AppendLine("using System;");
		sb.AppendLine("using System.Collections.Generic;");
		sb.AppendLine("using System.ComponentModel.DataAnnotations;");
		sb.AppendLine("using System.ComponentModel.DataAnnotations.Schema;");
		sb.AppendLine("using Microsoft.EntityFrameworkCore;");
		sb.AppendLine($"using {TargetNamespace};");
		sb.AppendLine();
		sb.AppendLine($"namespace {TargetNamespace};");
		sb.AppendLine();
		sb.AppendLine($"public class {ContextTypeName} : DbContext");
		sb.AppendLine("{");

		foreach (var table in tableDetails)
		{
			var className = classNameByTableName[table.FullName];
			sb.AppendLine($"    public DbSet<{className}> {className} {{ get; set; }} = null!;");
		}

		sb.AppendLine();
		sb.AppendLine("    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)");
		sb.AppendLine("    {");
		sb.AppendLine("        // Intentionally in-memory / stubbed for compilation-only scenarios");
		sb.AppendLine("        optionsBuilder.UseInMemoryDatabase(\"LinqStudioGeneratedDb\");");
		sb.AppendLine("    }");
		sb.AppendLine("}");
		return sb.ToString();
	}

	/// <summary>
	/// Converts a snake_case or already-PascalCase identifier to PascalCase.
	/// Splits on underscores and capitalises the first letter of each segment.
	/// </summary>
	private static string ToPascalCase(string name)
	{
		if (string.IsNullOrEmpty(name))
			return name;

		var parts = name.Split('_', StringSplitOptions.RemoveEmptyEntries);
		var sb = new StringBuilder();
		foreach (var part in parts)
		{
			if (part.Length == 0) continue;
			sb.Append(char.ToUpperInvariant(part[0]));
			sb.Append(part[1..]);
		}
		return sb.Length > 0 ? sb.ToString() : name;
	}

	/// <summary>Strips a schema prefix from a table reference, e.g. "dbo.Orders" → "Orders".</summary>
	private static string ExtractTableName(string fullTableName)
	{
		var dotIndex = fullTableName.LastIndexOf('.');
		return dotIndex >= 0 ? fullTableName[(dotIndex + 1)..] : fullTableName;
	}

	/// <summary>Basic singularisation: "Customers" → "Customer", "Categories" → "Category".</summary>
	private static string Singularize(string name)
	{
		if (string.IsNullOrEmpty(name)) return name;
		if (name.EndsWith("ies", StringComparison.Ordinal) && name.Length > 3)
			return name[..^3] + "y";
		if (name.EndsWith("ses", StringComparison.Ordinal) && name.Length > 3)
			return name[..^2];
		if (name.EndsWith("s", StringComparison.Ordinal) && name.Length > 1)
			return name[..^1];
		return name;
	}

	/// <summary>Basic pluralisation: "Order" → "Orders", "Category" → "Categories".</summary>
	private static string Pluralize(string name)
	{
		if (string.IsNullOrEmpty(name)) return name;
		if (name.EndsWith("y", StringComparison.Ordinal) && name.Length > 1)
			return name[..^1] + "ies";
		if (name.EndsWith("s", StringComparison.Ordinal))
			return name;
		return name + "s";
	}

	private static bool IsStringLike(DbColumnType type) =>
		type is DbColumnType.String or DbColumnType.Xml or DbColumnType.Json;

	private static bool IsValueType(DbColumnType type) =>
		type is DbColumnType.Boolean or DbColumnType.SByte or DbColumnType.Byte
			or DbColumnType.Int16 or DbColumnType.UInt16
			or DbColumnType.Int32 or DbColumnType.UInt32
			or DbColumnType.Int64 or DbColumnType.UInt64
			or DbColumnType.Float or DbColumnType.Double or DbColumnType.Decimal
			or DbColumnType.DateTime or DbColumnType.TimeSpan
			or DbColumnType.DateTimeOffset or DbColumnType.Guid;

	private static string GetCSharpTypeName(DbColumnType type, bool isNullable)
	{
		var baseType = type switch
		{
			DbColumnType.Boolean => "bool",
			DbColumnType.SByte => "sbyte",
			DbColumnType.Byte => "byte",
			DbColumnType.Int16 => "short",
			DbColumnType.UInt16 => "ushort",
			DbColumnType.Int32 => "int",
			DbColumnType.UInt32 => "uint",
			DbColumnType.Int64 => "long",
			DbColumnType.UInt64 => "ulong",
			DbColumnType.Float => "float",
			DbColumnType.Double => "double",
			DbColumnType.Decimal => "decimal",
			DbColumnType.String => "string",
			DbColumnType.DateTime => "DateTime",
			DbColumnType.TimeSpan => "TimeSpan",
			DbColumnType.DateTimeOffset => "DateTimeOffset",
			DbColumnType.Guid => "Guid",
			DbColumnType.Binary => "byte[]",
			DbColumnType.Xml => "string",
			DbColumnType.Json => "string",
			_ => "object",
		};
		return isNullable ? baseType + "?" : baseType;
	}

	private static string GetInitializer(DbColumnType type, bool isNullable)
	{
		if (isNullable) return string.Empty;
		if (IsStringLike(type)) return " = string.Empty;";
		if (type == DbColumnType.Binary) return " = [];";
		if (!IsValueType(type)) return " = null!;"; // object / Unknown
		return string.Empty; // value types are non-nullable by default
	}
}
