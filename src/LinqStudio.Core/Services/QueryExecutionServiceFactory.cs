using LinqStudio.Abstractions;
using LinqStudio.Core.Interfaces;
using LinqStudio.Core.Settings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LinqStudio.Core.Services;

/// <summary>
/// Creates per-editor instances of <see cref="QueryExecutionService"/>.
/// </summary>
public sealed class QueryExecutionServiceFactory(
	IDbContextGenerator generator,
	RoslynWorkspaceService roslynWorkspaceService,
	IOptionsMonitor<QueryExecutionSettings> settings,
	ILogger<QueryExecutionService>? logger = null) : IQueryExecutionServiceFactory
{
	public IQueryExecutionService Create()
		=> new QueryExecutionService(generator, roslynWorkspaceService, settings, logger);
}
