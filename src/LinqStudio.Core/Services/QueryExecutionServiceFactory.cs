using LinqStudio.Abstractions;
using LinqStudio.Core.Interfaces;
using LinqStudio.Core.Settings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LinqStudio.Core.Services;

/// <summary>
/// Creates per-editor instances of <see cref="QueryExecutionService"/>.
/// </summary>
/// <param name="roslynWorkspaceService">Service used to create the per-query Roslyn workspace.</param>
/// <param name="projectCompilationService">Scoped cache for the generated project model.</param>
/// <param name="settings">Live query execution settings.</param>
/// <param name="logger">Optional logger for query execution.</param>
public sealed class QueryExecutionServiceFactory(
	RoslynWorkspaceService roslynWorkspaceService,
	ProjectCompilationService projectCompilationService,
	IOptionsMonitor<QueryExecutionSettings> settings,
	ILogger<QueryExecutionService>? logger = null) : IQueryExecutionServiceFactory
{
	public IQueryExecutionService Create()
		=> new QueryExecutionService(roslynWorkspaceService, projectCompilationService, settings, logger);
}
