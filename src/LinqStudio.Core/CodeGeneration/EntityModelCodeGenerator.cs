using System.Text;
using LinqStudio.Abstractions.Models;
using LinqStudio.Core.Models;

namespace LinqStudio.Core.CodeGeneration;

internal sealed class EntityModelCodeGenerator
{
	private const string TargetNamespace = "GeneratedModels";

	public string Generate(
		DatabaseTableDetail table,
		GeneratedSchema schema)
	{
		var className = schema.ClassNameByTableName[table.FullName];
		var builder = new StringBuilder();
		builder.AppendLine("using System;");
		builder.AppendLine("using System.Collections.Generic;");
		builder.AppendLine("using System.ComponentModel.DataAnnotations;");
		builder.AppendLine("using System.ComponentModel.DataAnnotations.Schema;");
		builder.AppendLine();
		builder.AppendLine($"namespace {TargetNamespace};");
		builder.AppendLine();
		builder.AppendLine($"public class {className}");
		builder.AppendLine("{");

		AppendColumns(builder, table);
		AppendReferenceNavigations(builder, table, schema);
		AppendCollectionNavigations(builder, table, schema);

		builder.AppendLine("}");
		return builder.ToString();
	}

	private static void AppendColumns(StringBuilder builder, DatabaseTableDetail table)
	{
		foreach (var column in table.Columns)
		{
			var propertyName = CodeGenerationNaming.ToPascalCase(column.Name);
			var csharpType = GetCSharpTypeName(column.GenericType, column.IsNullable);

			if (column.IsPrimaryKey && column.IsIdentity)
				builder.AppendLine("    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]");

			if (IsStringLike(column.GenericType) && !column.IsNullable)
				builder.AppendLine("    [Required]");

			if (column.MaxLength.HasValue && column.MaxLength.Value != -1)
				builder.AppendLine($"    [MaxLength({column.MaxLength.Value})]");

			builder.AppendLine(
				$"    public {csharpType} {propertyName} {{ get; set; }}{GetInitializer(column.GenericType, column.IsNullable)}");
			builder.AppendLine();
		}
	}

	private static void AppendReferenceNavigations(
		StringBuilder builder,
		DatabaseTableDetail table,
		GeneratedSchema schema)
	{
		var usedNames = new HashSet<string>(StringComparer.Ordinal);
		foreach (var relationship in schema.Relationships.Where(x => x.SourceTableName.Equals(table.FullName, StringComparison.OrdinalIgnoreCase)))
		{
			var targetClassName = schema.ClassNameByTableName[relationship.TargetTableName];
			var navigationName = CodeGenerationNaming.Singularize(targetClassName);
			if (!usedNames.Add(navigationName))
			{
				var columnBase = relationship.SourceColumnName.EndsWith("Id", StringComparison.OrdinalIgnoreCase)
					? relationship.SourceColumnName[..^2]
					: relationship.SourceColumnName;
				navigationName = CodeGenerationNaming.ToPascalCase(columnBase) + CodeGenerationNaming.Singularize(targetClassName);
				usedNames.Add(navigationName);
			}

			builder.AppendLine($"    public virtual {targetClassName}? {navigationName} {{ get; set; }}");
			builder.AppendLine();
		}
	}

	private static void AppendCollectionNavigations(
		StringBuilder builder,
		DatabaseTableDetail table,
		GeneratedSchema schema)
	{
		var usedNames = new HashSet<string>(StringComparer.Ordinal);
		foreach (var relationship in schema.Relationships.Where(x => x.TargetTableName.Equals(table.FullName, StringComparison.OrdinalIgnoreCase)))
		{
			var childClassName = schema.ClassNameByTableName[relationship.SourceTableName];
			var collectionName = CodeGenerationNaming.Pluralize(childClassName);
			if (!usedNames.Add(collectionName))
			{
				collectionName = childClassName + "Collection";
				usedNames.Add(collectionName);
			}

			builder.AppendLine($"    public virtual ICollection<{childClassName}> {collectionName} {{ get; set; }} = [];");
			builder.AppendLine();
		}
	}

	private static bool IsStringLike(DbColumnType type) =>
		type is DbColumnType.String or DbColumnType.Xml or DbColumnType.Json;

	private static bool IsValueType(DbColumnType type) =>
		type is DbColumnType.Boolean or DbColumnType.SByte or DbColumnType.Byte
			or DbColumnType.Int16 or DbColumnType.UInt16
			or DbColumnType.Int32 or DbColumnType.UInt32
			or DbColumnType.Int64 or DbColumnType.UInt64
			or DbColumnType.Float or DbColumnType.Double or DbColumnType.Decimal
			or DbColumnType.DateOnly or DbColumnType.DateTime or DbColumnType.TimeSpan
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
			DbColumnType.DateOnly => "DateOnly",
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
		if (isNullable)
			return string.Empty;
		if (IsStringLike(type))
			return " = string.Empty;";
		if (type == DbColumnType.Binary)
			return " = [];";
		if (!IsValueType(type))
			return " = null!;";
		return string.Empty;
	}
}
