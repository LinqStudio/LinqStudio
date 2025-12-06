using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Completion;
using Microsoft.CodeAnalysis.Text;
using System.Reflection;

namespace LinqStudio.Core.Services;

public class CompilerService
{
	private readonly AdhocWorkspace _workspace;
	private readonly ProjectId _projectId;
	private Solution _solution;
	private readonly string _contextTypeName;
	private readonly string _projectNamespace;
	private const string _beforeUserQuery = ""; // Hardcoded, can be changed as needed
	private const string _afterUserQuery = "";  // Hardcoded, can be changed as needed

	public CompilerService(string contextTypeName, string projectNamespace)
	{
		_workspace = new AdhocWorkspace();
		var solutionInfo = SolutionInfo.Create(SolutionId.CreateNewId(), VersionStamp.Create());
		_solution = _workspace.AddSolution(solutionInfo);
		_projectId = ProjectId.CreateNewId();
		var projectInfo = ProjectInfo.Create(
			_projectId,
			VersionStamp.Create(),
			"EFCoreModelsProject",
			"EFCoreModelsProject",
			LanguageNames.CSharp
		);
		_solution = _solution.AddProject(projectInfo);
		_contextTypeName = contextTypeName;
		_projectNamespace = projectNamespace;

		// Add EF Core references and basic assemblies
		var efCoreAssemblies = new[]
		{
			"Microsoft.EntityFrameworkCore",
			"Microsoft.EntityFrameworkCore.Relational",
			"Microsoft.EntityFrameworkCore.SqlServer",
			"System.Linq",
			"System.Linq.Queryable"
		};
		var references = new List<MetadataReference>();
		foreach (var asmName in efCoreAssemblies)
		{
			var asm = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == asmName);
			if (asm == null)
			{
				try
				{
					asm = Assembly.Load(asmName);
				}
				catch { }
			}
			if (asm != null)
			{
				references.Add(MetadataReference.CreateFromFile(asm.Location));
			}
		}

		// add all left over assemblies from current domain
		foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
		{
			try
			{
				if (!asm.IsDynamic && !string.IsNullOrEmpty(asm.Location) && !efCoreAssemblies.Contains(asm.GetName().Name))
				{
					references.Add(MetadataReference.CreateFromFile(asm.Location));
				}
			}
			catch { }
		}

		_solution = _solution.WithProjectMetadataReferences(_projectId, references);
	}

	#region Init / Add files

	public void Initialize(Dictionary<string, string> tableModelFiles, string dbContextCode)
	{
		foreach ((var tableName, var modelCode) in tableModelFiles)
		{
			var documentName = tableName + ".cs";
			AddOrUpdateFile(documentName, modelCode);
		}
		AddOrUpdateFile("DbContext.cs", dbContextCode);
	}

	public void AddUserQuery(string content)
	{
		var wrapped = WrapUserQuery(content);
		AddOrUpdateFile("UserQuery.cs", wrapped);
	}

	private Document AddOrUpdateFile(string name, string content)
	{
		var project = _solution.GetProject(_projectId);
		var document = project?.Documents.FirstOrDefault(d => d.Name == name);
		if (document != null)
		{
			_solution = _solution.WithDocumentText(document.Id, SourceText.From(content));
			return _solution.GetDocument(document.Id)!;
		}
		else
		{
			var documentId = DocumentId.CreateNewId(_projectId);
			_solution = _solution.AddDocument(documentId, name, SourceText.From(content));
			return _solution.GetDocument(documentId)!;
		}
	}

	#endregion

	private string WrapUserQuery(string userQuery)
	{
		return $$"""
using System;
using System.Linq;
using System.Threading.Tasks;

namespace {{_projectNamespace}};

public class QueryContainer
{
    public async Task<IQueryable<object>> Query({{_contextTypeName}} context)
    {
        {{_beforeUserQuery}}
        {{userQuery}}
        {{_afterUserQuery}}
    }
}
""";
	}

	public async Task<IReadOnlyList<string>> GetCompletionsAsync(string userQueryContent, int cursorPosition)
	{
		var wrapped = WrapUserQuery(userQueryContent);
		// Adjust cursor position to account for the wrapper
		var thisHere = "__THIS_HERE__";
		var prefix = WrapUserQuery(thisHere);
		var wrappedCursorPosition = prefix.IndexOf(thisHere) + _beforeUserQuery.Length;
		var document = AddOrUpdateFile("UserQuery.cs", wrapped);

		var completionService = CompletionService.GetService(document);
		if (completionService == null)
			return [];

		var completionList = await completionService.GetCompletionsAsync(document, wrappedCursorPosition + cursorPosition);
		if (completionList == null)
			return [];

		return [.. completionList.ItemsList.Select(item => item.DisplayText)];
	}
}
