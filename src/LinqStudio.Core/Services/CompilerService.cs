using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Completion;
using Microsoft.CodeAnalysis.Text;
using System.Reflection;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LinqStudio.Core.Services;

public class CompilerService : IDisposable
{
    private readonly SemaphoreSlim _lock = new(1, 1);
    private bool _disposed;
    private readonly AdhocWorkspace _workspace;
    private readonly ProjectId _projectId;
    private Solution _solution;
    private readonly string _contextTypeName;
    private readonly string _projectNamespace;
    private const string _beforeUserQuery = "return";
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

        // Ensure the C# parser includes documentation comments so XML docs are available on symbols
        try
        {
            var parseOptions = new Microsoft.CodeAnalysis.CSharp.CSharpParseOptions(documentationMode: Microsoft.CodeAnalysis.DocumentationMode.Diagnose);
            _solution = _solution.WithProjectParseOptions(_projectId, parseOptions);
        }
        catch { }
    }

    #region Init / Add files

    public async Task Initialize(Dictionary<string, string> tableModelFiles, string dbContextCode)
    {
        await _lock.WaitAsync();
        try
        {
            foreach ((var tableName, var modelCode) in tableModelFiles)
            {
                var documentName = tableName + ".cs";
                AddOrUpdateFile(documentName, modelCode);
            }
            AddOrUpdateFile("DbContext.cs", dbContextCode);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task AddUserQuery(string content)
    {
        await _lock.WaitAsync();
        try
        {
            var wrapped = WrapUserQuery(content);
            AddOrUpdateFile("UserQuery.cs", wrapped);
        }
        finally
        {
            _lock.Release();
        }
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
        if(!userQuery.TrimEnd().EndsWith(';'))
            userQuery += ";";
        
        return $$"""
using System;
using System.Linq;
using System.Threading.Tasks;

namespace {{_projectNamespace}};

public class QueryContainer
{
    public async Task<IQueryable<object>> Query({{_contextTypeName}} context)
    {
        {{_beforeUserQuery}} {{userQuery}}
        {{_afterUserQuery}}
    }
}
""";
    }

    public async Task<IReadOnlyList<(CompletionItem Item, string? Description)>> GetCompletionsAsync(string userQueryContent, int cursorPosition)
    {
        // serialize access so multiple concurrent Monaco callbacks don't mutate the workspace concurrently
        await _lock.WaitAsync();
        try
        {
            var wrapped = WrapUserQuery(userQueryContent);
        // Adjust cursor position to account for the wrapper
        var thisHere = "__THIS_HERE__";
        var prefix = WrapUserQuery(thisHere);
        var wrappedCursorPosition = prefix.IndexOf(thisHere);
            var document = AddOrUpdateFile("UserQuery.cs", wrapped);

        var completionService = CompletionService.GetService(document);
        if (completionService == null)
            return [];

        // clamp cursorPosition to a safe range inside the user's content
        var safeCursor = Math.Clamp(cursorPosition, 0, (userQueryContent ?? string.Empty).Length);
        var completionList = await completionService.GetCompletionsAsync(document, wrappedCursorPosition + safeCursor);
        if (completionList == null)
            return [];

        var results = new List<(CompletionItem, string? Description)>();
        foreach (var completion in completionList.ItemsList)
        {
            var description = await completionService.GetDescriptionAsync(document, completion);
            results.Add((completion, description?.Text));
        }

            return results;
        }
        finally
        {
            _lock.Release();
        }
    }

    // Simple hover information result returned to callers
    public record HoverInfo(string? Markdown, int StartOffset, int Length);

    public async Task<HoverInfo?> GetHoverAsync(string userQueryContent, int cursorPosition)
    {
        await _lock.WaitAsync();
        try
        {
            var wrapped = WrapUserQuery(userQueryContent);

        // determine the start of the user query inside the wrapped document
        var thisHere = "__THIS_HERE__";
        var prefix = WrapUserQuery(thisHere);
        var wrappedCursorPosition = prefix.IndexOf(thisHere);

            var document = AddOrUpdateFile("UserQuery.cs", wrapped);

        var root = await document.GetSyntaxRootAsync();
        if (root == null)
            return null;

        var semanticModel = await document.GetSemanticModelAsync();
        if (semanticModel == null)
            return null;

        // clamp cursorPosition so invalid values won't crash Roslyn
        var safeCursor = Math.Clamp(cursorPosition, 0, (userQueryContent ?? string.Empty).Length);
        var absolutePos = wrappedCursorPosition + safeCursor;

        if (absolutePos < 0 || absolutePos > root.FullSpan.End)
            return null;

        var token = root.FindToken(absolutePos);

        if (token == default)
            return null;

        // debug output (temporary)
        // (debug prints removed)

        // Find candidates in a prioritized order that are likely to represent the thing the user expects
        // when hovering: identifier/simple-name, member-access name, invocation's callee, then generic expressions.
        var ancestors = token.Parent?.AncestorsAndSelf() ?? Enumerable.Empty<SyntaxNode>();

        SyntaxNode? candidateNode = ancestors.OfType<SimpleNameSyntax>().FirstOrDefault()
            ?? ancestors.OfType<MemberAccessExpressionSyntax>().FirstOrDefault()
            ?? ancestors.OfType<InvocationExpressionSyntax>().FirstOrDefault()
            ?? ancestors.OfType<ExpressionSyntax>().FirstOrDefault()
            ?? token.Parent;

        if (candidateNode == null)
            return null;

        // candidateNode determined

        // Helper to try resolve symbol info (prefer Symbol, then CandidateSymbols)
        static ISymbol? ResolveSymbol(SemanticModel sm, SyntaxNode n)
        {
            var info = sm.GetSymbolInfo(n);
            // minimal symbol resolution helper (no debug writes)
            return info.Symbol ?? info.CandidateSymbols.FirstOrDefault();
        }

        ISymbol? symbol = ResolveSymbol(semanticModel, candidateNode);
        SyntaxNode resolvedNode = candidateNode;

        // If the user hovered inside an invocation argument (for example a lambda) we still want the
        // invoked method as the hover target (e.g., Where). Try to detect invocation ancestor and resolve
        // the callee specifically if our initial symbol is either a lambda/anonymous or not useful.
        bool IsAnonymousOrLambda(ISymbol? s)
        {
            if (s == null) return true;
            // anonymous function or compiler-generated symbol often have empty or special names
            if (s.Kind == SymbolKind.Method && string.IsNullOrWhiteSpace(s.Name))
                return true;
            // fallback: some compiler symbols will show as "Lambda" when displayed – treat those as not ideal
            var name = s.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
            return string.Equals(name, "Lambda", StringComparison.OrdinalIgnoreCase);
        }

        // If we are inside an invocation's argument (e.g. in a lambda or param) prefer resolving
        // the invoked method even if the local symbol exists (user expects to see the method signature)
        var invocationAncestor = candidateNode.AncestorsAndSelf().OfType<InvocationExpressionSyntax>().FirstOrDefault();
        bool insideInvocationArg = invocationAncestor != null && invocationAncestor.ArgumentList != null && invocationAncestor.ArgumentList.Span.Contains(candidateNode.Span);

        if (symbol == null || IsAnonymousOrLambda(symbol) || insideInvocationArg)
        {
            // if candidateNode is an invocation try resolving its expression (callee)
            // (invocationAncestor already computed above)
            if (invocationAncestor != null)
            {
                // try resolving the invocation expression itself first (this often yields the method symbol for calls)
                symbol = ResolveSymbol(semanticModel, invocationAncestor) ?? symbol;
                if (symbol != null)
                {
                    resolvedNode = invocationAncestor;
                }

                var callee = invocationAncestor.Expression;
                // If callee is a member access, prefer resolving the member name
                if (callee is MemberAccessExpressionSyntax ma)
                {
                    var nameNode = (SyntaxNode)ma.Name;
                    symbol = ResolveSymbol(semanticModel, nameNode) ?? ResolveSymbol(semanticModel, ma) ?? symbol;
                    resolvedNode = nameNode;
                }
                else
                {
                    symbol = ResolveSymbol(semanticModel, callee) ?? symbol;
                    resolvedNode = callee;
                }
            }
        }

        // If we still haven't found a symbol, try an extension-method / compilation search fallback
        if (symbol == null)
        {
            // Try to resolve common extension method patterns (e.g. context.People.Where(...)) by
            // searching the compilation for methods with the same name and checking whether
            // the type of the receiver can be implicitly converted to the first parameter type.
            string candidateName = token.ValueText;
            // prefer the simple name if available
            if (candidateNode is SimpleNameSyntax sn)
                candidateName = sn.Identifier.ValueText;
            else if (candidateNode is MemberAccessExpressionSyntax ma)
                candidateName = ma.Name is SimpleNameSyntax sns ? sns.Identifier.ValueText : ma.Name.ToString();

            // determine a receiver expression (context.People in context.People.Where)
            SyntaxNode? receiverNode = null;
            if (candidateNode is SimpleNameSyntax s)
            {
                if (s.Parent is MemberAccessExpressionSyntax m2 && m2.Name == s)
                    receiverNode = m2.Expression;
                else if (s.Parent is InvocationExpressionSyntax inv && inv.Expression is MemberAccessExpressionSyntax m3 && m3.Name == s)
                    receiverNode = m3.Expression;
            }
            else if (candidateNode is MemberAccessExpressionSyntax ma2)
            {
                receiverNode = ma2.Expression;
            }

            if (receiverNode != null)
            {
                var receiverType = semanticModel.GetTypeInfo(receiverNode).Type ?? semanticModel.GetTypeInfo(receiverNode).ConvertedType;
                if (receiverType != null)
                {
                    // search compilation for methods with the same name
                    var methods = semanticModel.Compilation.GetSymbolsWithName(n => n == candidateName, SymbolFilter.Member)
                        .OfType<IMethodSymbol>()
                        .ToArray();

                    // If none found globally, attempt to inspect System.Linq.Queryable directly (where LINQ extension methods live)
                    if (methods.Length == 0)
                    {
                        try
                        {
                            var qtype = semanticModel.Compilation.GetTypeByMetadataName("System.Linq.Queryable");
                            if (qtype != null)
                            {
                                methods = qtype.GetMembers(candidateName).OfType<IMethodSymbol>().ToArray();
                            }
                        }
                        catch { }
                    }

                    // (no debug output)

                    // filter candidates by checking if the receiver type can be converted to the first parameter
                    var matches = new List<IMethodSymbol>();
                    foreach (var m in methods)
                    {
                        if (m.Parameters.Length == 0)
                            continue;

                        var firstParam = m.Parameters[0].Type;
                        try
                        {
                            // Some Roslyn runtimes don't expose ClassifyConversion on Compilation in this context.
                            // Use a conservative compatibility check: exact match, implemented interfaces and common LINQ interfaces.
                            bool compatible = false;
                            if (SymbolEqualityComparer.Default.Equals(receiverType, firstParam))
                                compatible = true;
                            else if (firstParam is INamedTypeSymbol paramNamed)
                            {
                                // check if receiver implements the same generic definition
                                if (receiverType.AllInterfaces.Any(i => SymbolEqualityComparer.Default.Equals(i.OriginalDefinition, paramNamed.OriginalDefinition)))
                                    compatible = true;
                                // quick heuristic for IQueryable/IEnumerable families
                                if (!compatible && (paramNamed.Name.Contains("IQueryable") || paramNamed.Name.Contains("IEnumerable")))
                                {
                                    if (receiverType.Name.Contains("IQueryable") || receiverType.AllInterfaces.Any(i => i.Name.Contains("IQueryable") || i.Name.Contains("IEnumerable")))
                                        compatible = true;
                                }
                            }

                            if (compatible)
                                matches.Add(m);
                        }
                        catch { }
                    }

                    // prefer extension methods/static LINQ helpers if present
                    var chosen = matches.OrderByDescending(x => x.IsExtensionMethod ? 1 : 0).FirstOrDefault();
                    if (chosen != null)
                    {
                        symbol = chosen;
                        // keep the resolved node as the name (so the hover range is just the identifier)
                        resolvedNode = candidateNode;
                    }
                }
            }

            // as a last resort, attempt a lookup at the hover position which may discover extension methods
            if (symbol == null)
            {
                var lookups = semanticModel.LookupSymbols(absolutePos, name: token.ValueText).OfType<IMethodSymbol>().ToArray();
                if (lookups.Length > 0)
                {
                    symbol = lookups.First();
                    resolvedNode = candidateNode;
                }
            }

            // If still not found, fallback to type info for the candidate node (keeps old behavior)
        }
        
        if (symbol == null)
        {
            var typeInfo = semanticModel.GetTypeInfo(candidateNode);
            symbol = typeInfo.Type ?? typeInfo.ConvertedType;
        }

        if (symbol == null)
            return null;

        string display;
        switch (symbol)
        {
            case Microsoft.CodeAnalysis.IMethodSymbol m:
                display = m.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
                break;
            case Microsoft.CodeAnalysis.IPropertySymbol p:
                display = $"{p.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)} {p.Name}";
                break;
            default:
                display = symbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
                break;
        }

        // prefer documentation if available (XML); include it as-is as a secondary section
        var docXml = symbol.GetDocumentationCommentXml();
        var markdown = display;
        if (!string.IsNullOrWhiteSpace(docXml))
        {
            markdown += "\n\n" + docXml;
        }

        var span = resolvedNode.Span;
        var userStart = span.Start - wrappedCursorPosition;
        var length = span.Length;

        if (userStart < 0)
            return null;

            return new HoverInfo(markdown, userStart, length);
        }
        finally
        {
            _lock.Release();
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        try
        {
            _workspace?.Dispose();
        }
        catch { }

        try
        {
            _lock?.Dispose();
        }
        catch { }
    }
}
