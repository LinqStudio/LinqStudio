using LinqStudio.Abstractions.Models;

namespace LinqStudio.Core.Models;

internal sealed record GeneratedSchema(
	IReadOnlyList<DatabaseTableDetail> Tables,
	IReadOnlyDictionary<string, string> ClassNameByTableName,
	IReadOnlyList<GeneratedRelationship> Relationships);

internal sealed record GeneratedRelationship(
	string Name,
	string SourceTableName,
	string SourceColumnName,
	string TargetTableName,
	string TargetColumnName);
