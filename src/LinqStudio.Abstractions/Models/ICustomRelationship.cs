namespace LinqStudio.Abstractions.Models;

/// <summary>
/// Describes a project-defined relationship without coupling the abstractions assembly
/// to the Core project model.
/// </summary>
public interface ICustomRelationship
{
	string PrincipalTable { get; }
	string DependentTable { get; }
	int Cardinality { get; }
	bool IsRequired { get; }
	string PrincipalNavigation { get; }
	string DependentNavigation { get; }
	int DeleteBehavior { get; }
	IReadOnlyList<ICustomRelationshipKeyPair> KeyPairs { get; }
}

public interface ICustomRelationshipKeyPair
{
	string PrincipalColumn { get; }
	string DependentColumn { get; }
}
