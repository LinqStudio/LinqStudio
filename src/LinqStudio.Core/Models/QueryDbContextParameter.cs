namespace LinqStudio.Core.Models;

/// <summary>
/// Describes one generated DbContext as a parameter in the compiled query wrapper.
/// </summary>
public sealed record QueryDbContextParameter(
	string ContextTypeName,
	string Namespace,
	string ParameterName);
