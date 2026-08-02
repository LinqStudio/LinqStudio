using System.Text;

namespace LinqStudio.Core.CodeGeneration;

public static class CodeGenerationNaming
{
	public static string ToPascalCase(string name)
	{
		if (string.IsNullOrEmpty(name))
			return name;

		var parts = name.Split('_', StringSplitOptions.RemoveEmptyEntries);
		var builder = new StringBuilder();
		foreach (var part in parts)
		{
			if (part.Length == 0)
				continue;

			builder.Append(char.ToUpperInvariant(part[0]));
			builder.Append(part[1..]);
		}

		return builder.Length > 0 ? builder.ToString() : name;
	}

	public static string ExtractTableName(string fullTableName)
	{
		var dotIndex = fullTableName.LastIndexOf('.');
		return dotIndex >= 0 ? fullTableName[(dotIndex + 1)..] : fullTableName;
	}

	public static string Singularize(string name)
	{
		if (string.IsNullOrEmpty(name))
			return name;
		if (name.EndsWith("ies", StringComparison.Ordinal) && name.Length > 3)
			return name[..^3] + "y";
		if (name.EndsWith("ses", StringComparison.Ordinal) && name.Length > 3)
			return name[..^2];
		if (name.EndsWith("s", StringComparison.Ordinal) && name.Length > 1)
			return name[..^1];
		return name;
	}

	public static string Pluralize(string name)
	{
		if (string.IsNullOrEmpty(name))
			return name;
		if (name.EndsWith("y", StringComparison.Ordinal) && name.Length > 1)
			return name[..^1] + "ies";
		if (name.EndsWith("s", StringComparison.Ordinal))
			return name;
		return name + "s";
	}
}
