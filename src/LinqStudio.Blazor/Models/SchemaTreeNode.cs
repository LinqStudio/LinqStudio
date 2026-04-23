using LinqStudio.Abstractions.Models;
using MudBlazor;

namespace LinqStudio.Blazor.Models;

/// <summary>
/// Discriminates the role of a <see cref="SchemaTreeNode"/> in the schema tree.
/// </summary>
public enum SchemaTreeNodeType
{
	/// <summary>Root node — one per database connection.</summary>
	Connection,

	/// <summary>"Tables" grouping folder under a connection.</summary>
	TablesFolder,

	/// <summary>Individual table node.</summary>
	Table,

	/// <summary>Leaf column node inside a table.</summary>
	Column,
}

/// <summary>
/// View-model for a single node in the SSMS-style schema tree.
/// Used as the typed item for <c>MudTreeView&lt;SchemaTreeNode&gt;</c>.
/// </summary>
public class SchemaTreeNode
{
	/// <summary>The kind of object this node represents.</summary>
	public required SchemaTreeNodeType NodeType { get; init; }

	/// <summary>Text displayed in the tree row.</summary>
	public required string Label { get; init; }

	/// <summary>MudBlazor icon string for this node.</summary>
	public required string Icon { get; init; }

	/// <summary>Color applied to <see cref="Icon"/>.</summary>
	public Color IconColor { get; init; } = Color.Default;

	/// <summary>
	/// Child nodes. Populated lazily (table children = columns, connection children = folders).
	/// Note: the <c>init</c> accessor prevents re-assigning the list reference after construction;
	/// mutating the existing list (Add/Clear) is still permitted and is how lazy loading works.
	/// </summary>
	public List<SchemaTreeNode> Children { get; init; } = [];

	// ── Optional payloads — only one is set depending on NodeType ──────────

	/// <summary>Set for <see cref="SchemaTreeNodeType.Connection"/> nodes.</summary>
	public ConnectionInfo? ConnectionInfo { get; init; }

	/// <summary>Set for <see cref="SchemaTreeNodeType.Table"/> nodes. Used to call GetTableAsync().</summary>
	public DatabaseTableName? TableName { get; init; }

	/// <summary>Set for <see cref="SchemaTreeNodeType.Column"/> nodes. Carries display metadata.</summary>
	public TableColumn? ColumnDetail { get; init; }

	/// <summary>
	/// For <see cref="SchemaTreeNodeType.Column"/> nodes: the pre-formatted data type string
	/// (e.g., "varchar(100)?"). Computed once when the node is created.
	/// </summary>
	public string? ColumnTypeDisplay { get; init; }

	// ── Loading state ───────────────────────────────────────────────────────

	/// <summary>
	/// True while async column children are being fetched for this node.
	/// The template renders a spinner placeholder in the children area when this is true.
	/// </summary>
	public bool IsLoading { get; set; }
}
