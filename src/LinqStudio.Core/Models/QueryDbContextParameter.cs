namespace LinqStudio.Core.Models;

public sealed record QueryDbContextParameter(
	string ContextTypeName,
	string Namespace,
	string ParameterName);
