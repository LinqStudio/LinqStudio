using LinqStudio.Abstractions.Models;

namespace LinqStudio.Core.Models;

internal sealed record GeneratedSchema(
	IReadOnlyList<DatabaseTableDetail> Tables,
	IReadOnlyDictionary<string, string> ClassNameByTableName,
	IReadOnlyList<GeneratedRelationship> Relationships);

internal sealed record GeneratedRelationship(
	string Name,
	string SourceTableName,
	string TargetTableName,
	string SourceColumnName,
	string TargetColumnName,
	RelationshipCardinality Cardinality = RelationshipCardinality.OneToMany,
	string? SourceNavigationName = null,
	string? TargetNavigationName = null,
	bool IsRequired = false,
	IReadOnlyList<GeneratedKeyPair>? KeyPairs = null,
	bool IsCustom = false,
	RelationshipDeleteBehavior DeleteBehavior = RelationshipDeleteBehavior.NoAction);

internal sealed record GeneratedKeyPair(string SourceColumnName, string TargetColumnName);
