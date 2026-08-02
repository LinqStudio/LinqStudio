namespace LinqStudio.Core.Models;

public enum RelationshipCardinality
{
	OneToOne,
	OneToMany,
	ManyToOne,
	ManyToMany,
}

public enum RelationshipDeleteBehavior
{
	ClientSetNull,
	Cascade,
	Restrict,
	NoAction,
}

public sealed class RelationshipKeyPair
{
	public string PrincipalColumn { get; set; } = string.Empty;
	public string DependentColumn { get; set; } = string.Empty;
}

public sealed class CustomRelationship
{
	public Guid Id { get; set; } = Guid.NewGuid();
	public string PrincipalTable { get; set; } = string.Empty;
	public string DependentTable { get; set; } = string.Empty;
	public RelationshipCardinality Cardinality { get; set; } = RelationshipCardinality.OneToMany;
	public bool IsRequired { get; set; }
	public string PrincipalNavigation { get; set; } = string.Empty;
	public string DependentNavigation { get; set; } = string.Empty;
	public RelationshipDeleteBehavior DeleteBehavior { get; set; } = RelationshipDeleteBehavior.NoAction;
	public List<RelationshipKeyPair> KeyPairs { get; set; } = [];
}
