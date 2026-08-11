using LinqStudio.Abstractions.Models;
using LinqStudio.Core.Models;
using System.Text;

namespace LinqStudio.Core.CodeGeneration;

internal sealed class DbContextCodeGenerator
{
	public string Generate(GeneratedSchema schema, string targetNamespace, string contextTypeName)
	{
		var builder = new StringBuilder();
		builder.AppendLine("using System;");
		builder.AppendLine("using System.Collections.Generic;");
		builder.AppendLine("using System.ComponentModel.DataAnnotations;");
		builder.AppendLine("using System.ComponentModel.DataAnnotations.Schema;");
		builder.AppendLine("using Microsoft.EntityFrameworkCore;");
		builder.AppendLine($"using {targetNamespace};");
		builder.AppendLine();
		builder.AppendLine($"namespace {targetNamespace};");
		builder.AppendLine();
		builder.AppendLine($"public class {contextTypeName} : DbContext");
		builder.AppendLine("{");

		foreach (var table in schema.Tables)
		{
			var className = schema.ClassNameByTableName[table.FullName];
			builder.AppendLine($"    public DbSet<{className}> {className} {{ get; set; }} = null!;");
		}

		builder.AppendLine();
		builder.AppendLine("    // Parameterless constructor for IntelliSense compilation; also used as base class for runtime instantiation via the options constructor");
		builder.AppendLine($"    public {contextTypeName}() {{ }}");
		builder.AppendLine();
		builder.AppendLine("    // Standard EF Core constructor used for real query execution");
		builder.AppendLine($"    public {contextTypeName}(DbContextOptions options) : base(options) {{ }}");
		builder.AppendLine();
		builder.AppendLine("    protected override void OnModelCreating(ModelBuilder modelBuilder)");
		builder.AppendLine("    {");

		AppendKeys(builder, schema);
		AppendTableMappings(builder, schema);
		AppendColumnMappings(builder, schema);
		AppendCustomRelationships(builder, schema);
		AppendDateOnlyConversions(builder, schema);
		builder.AppendLine("    }");
		builder.AppendLine("}");
		return builder.ToString();
	}

	private static void AppendCustomRelationships(StringBuilder builder, GeneratedSchema schema)
	{
		foreach (var relationship in schema.Relationships.Where(relationship => relationship.IsCustom))
		{
			var sourceClassName = schema.ClassNameByTableName[relationship.SourceTableName];
			var targetClassName = schema.ClassNameByTableName[relationship.TargetTableName];
			var sourceNavigationName = GetNavigationName(
				relationship.SourceNavigationName,
				GetSourceNavigationFallback(relationship, targetClassName));
			var targetNavigationName = GetNavigationName(
				relationship.TargetNavigationName,
				GetTargetNavigationFallback(relationship, sourceClassName));

			var configuration = relationship.Cardinality switch
			{
				RelationshipCardinality.OneToMany =>
					$"modelBuilder.Entity<{sourceClassName}>().HasOne(e => e.{sourceNavigationName}).WithMany(e => e.{targetNavigationName})",
				RelationshipCardinality.OneToOne =>
					$"modelBuilder.Entity<{sourceClassName}>().HasOne(e => e.{sourceNavigationName}).WithOne(e => e.{targetNavigationName})",
				RelationshipCardinality.ManyToOne =>
					$"modelBuilder.Entity<{sourceClassName}>().HasMany(e => e.{sourceNavigationName}).WithOne(e => e.{targetNavigationName})",
				RelationshipCardinality.ManyToMany =>
					$"modelBuilder.Entity<{sourceClassName}>().HasMany(e => e.{sourceNavigationName}).WithMany(e => e.{targetNavigationName})",
				_ => throw new ArgumentOutOfRangeException(),
			};

			if (relationship.Cardinality != RelationshipCardinality.ManyToMany
				&& relationship.KeyPairs is { Count: > 0 })
			{
				var foreignKeyPairs = relationship.Cardinality == RelationshipCardinality.ManyToOne
					? relationship.KeyPairs.Select(pair => new GeneratedKeyPair(pair.TargetColumnName, pair.SourceColumnName))
					: relationship.KeyPairs;

				configuration +=
					$".HasForeignKey({BuildKeyExpression(foreignKeyPairs, useSourceColumn: true)})"
					+ $".HasPrincipalKey({BuildKeyExpression(foreignKeyPairs, useSourceColumn: false)})";
			}

			if (relationship.Cardinality != RelationshipCardinality.ManyToMany)
				configuration += $".OnDelete(DeleteBehavior.{relationship.DeleteBehavior})";

			builder.AppendLine($"        {configuration};");
		}
	}

	private static string GetNavigationName(string? configuredName, string fallback) =>
		string.IsNullOrWhiteSpace(configuredName)
			? fallback
			: CodeGenerationNaming.ToPascalCase(configuredName);

	private static string GetSourceNavigationFallback(
		GeneratedRelationship relationship,
		string relatedClassName) =>
		relationship.Cardinality is RelationshipCardinality.ManyToOne or RelationshipCardinality.ManyToMany
			? CodeGenerationNaming.Pluralize(relatedClassName)
			: CodeGenerationNaming.Singularize(relatedClassName);

	private static string GetTargetNavigationFallback(
		GeneratedRelationship relationship,
		string relatedClassName) =>
		relationship.Cardinality is RelationshipCardinality.OneToMany or RelationshipCardinality.ManyToMany
			? CodeGenerationNaming.Pluralize(relatedClassName)
			: CodeGenerationNaming.Singularize(relatedClassName);

	private static string BuildKeyExpression(
		IEnumerable<GeneratedKeyPair> keyPairs,
		bool useSourceColumn)
	{
		var columns = keyPairs
			.Select(pair => useSourceColumn ? pair.SourceColumnName : pair.TargetColumnName)
			.ToList();
		if (columns.Count == 1)
			return $"e => e.{CodeGenerationNaming.ToPascalCase(columns[0])}";

		var properties = string.Join(", ", columns.Select(column =>
			$"e.{CodeGenerationNaming.ToPascalCase(column)}"));
		return $"e => new {{ {properties} }}";
	}

	private static void AppendKeys(StringBuilder builder, GeneratedSchema schema)
	{
		foreach (var table in schema.Tables)
		{
			var className = schema.ClassNameByTableName[table.FullName];
			var primaryKeyColumns = table.Columns.Where(column => column.IsPrimaryKey).ToList();
			if (primaryKeyColumns.Count == 0)
			{
				builder.AppendLine($"        modelBuilder.Entity<{className}>().HasNoKey();");
			}
			else if (primaryKeyColumns.Count == 1)
			{
				builder.AppendLine(
					$"        modelBuilder.Entity<{className}>().HasKey(e => e.{CodeGenerationNaming.ToPascalCase(primaryKeyColumns[0].Name)});");
			}
			else
			{
				var properties = string.Join(", ", primaryKeyColumns.Select(column =>
					$"e.{CodeGenerationNaming.ToPascalCase(column.Name)}"));
				builder.AppendLine(
					$"        modelBuilder.Entity<{className}>().HasKey(e => new {{ {properties} }});");
			}
		}
	}

	private static void AppendTableMappings(StringBuilder builder, GeneratedSchema schema)
	{
		foreach (var table in schema.Tables)
		{
			var className = schema.ClassNameByTableName[table.FullName];
			builder.AppendLine(
				$"        modelBuilder.Entity<{className}>().ToTable(\"{EscapeString(table.Name)}\");");
		}
	}

	private static string EscapeString(string value) =>
		value.Replace("\\", "\\\\", StringComparison.Ordinal)
			.Replace("\"", "\\\"", StringComparison.Ordinal);

	private static void AppendColumnMappings(StringBuilder builder, GeneratedSchema schema)
	{
		foreach (var table in schema.Tables)
		{
			var className = schema.ClassNameByTableName[table.FullName];
			foreach (var column in table.Columns)
			{
				var propertyName = CodeGenerationNaming.ToPascalCase(column.Name);
				builder.AppendLine(
					$"        modelBuilder.Entity<{className}>().Property(e => e.{propertyName}).HasColumnName(\"{EscapeString(column.Name)}\");");
			}
		}
	}

	private static void AppendDateOnlyConversions(StringBuilder builder, GeneratedSchema schema)
	{
		foreach (var table in schema.Tables)
		{
			var className = schema.ClassNameByTableName[table.FullName];
			foreach (var column in table.Columns.Where(column => column.GenericType == DbColumnType.DateOnly))
			{
				var propertyName = CodeGenerationNaming.ToPascalCase(column.Name);
				var conversion = column.IsNullable
					? "HasConversion(v => v.HasValue ? v.Value.ToDateTime(TimeOnly.MinValue) : (DateTime?)null, v => v.HasValue ? DateOnly.FromDateTime(v.Value) : (DateOnly?)null)"
					: "HasConversion(v => v.ToDateTime(TimeOnly.MinValue), v => DateOnly.FromDateTime(v))";
				builder.AppendLine(
					$"        modelBuilder.Entity<{className}>().Property(e => e.{propertyName}).HasColumnType(\"date\").{conversion};");
			}
		}
	}
}
