using System.Runtime.CompilerServices;

// Allow the Blazor test project to access internal members (e.g., RefreshTablesFolderAsync,
// RefreshTableNodeAsync on DatabaseTreeView) without making them fully public.
[assembly: InternalsVisibleTo("LinqStudio.Blazor.Tests")]
