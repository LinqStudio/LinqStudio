using System.Text;
using LinqStudio.Abstractions.Models;
using LinqStudio.Core.Models;

namespace LinqStudio.Core.CodeGeneration;

internal sealed class DbContextCodeGenerator
{
	private const string TargetNamespace = "GeneratedModels";
	private const string ContextTypeName = "GeneratedDbContext";

	public string Generate(GeneratedSchema schema)
	{
		var builder = new StringBuilder();
		builder.AppendLine("using System;");
		builder.AppendLine("using System.Collections.Generic;");
		builder.AppendLine("using System.ComponentModel.DataAnnotations;");
		builder.AppendLine("using System.ComponentModel.DataAnnotations.Schema;");
		builder.AppendLine("using Microsoft.EntityFrameworkCore;");
		builder.AppendLine($"using {TargetNamespace};");
		builder.AppendLine();
		builder.AppendLine($"namespace {TargetNamespace};");
		builder.AppendLine();
		builder.AppendLine($"public class {ContextTypeName} : DbContext");
		builder.AppendLine("{");

		foreach (var table in schema.Tables)
		{
			var className = schema.ClassNameByTableName[table.FullName];
			builder.AppendLine($"    public DbSet<{className}> {className} {{ get; set; }} = null!;");
		}

		builder.AppendLine();
		builder.AppendLine("    // Parameterless constructor for IntelliSense compilation; also used as base class for runtime instantiation via the options constructor");
		builder.AppendLine($"    public {ContextTypeName}() {{ }}");
		builder.AppendLine();
		builder.AppendLine("    // Standard EF Core constructor used for real query execution");
		builder.AppendLine($"    public {ContextTypeName}(DbContextOptions options) : base(options) {{ }}");
		builder.AppendLine();
		builder.AppendLine("    protected override void OnModelCreating(ModelBuilder modelBuilder)");
		builder.AppendLine("    {");

		AppendKeys(builder, schema);
		AppendDateOnlyConversions(builder, schema);
		builder.AppendLine("    }");
		builder.AppendLine("}");
		return builder.ToString();
	}

	private static void AppendKeys(StringBuilder builder, GeneratedSchema schema)
	{
		foreach (var table in schema.Tables)
		{
			var className = schema.ClassNameByTableName[table.FullName];
			var primaryKeyColumns = table.Columns.Where(column => column.IsPrimaryKey).ToList();
			if (primaryKeyColumns.Count == 0)
				continue;

			if (primaryKeyColumns.Count == 1)
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
