# Layout components

The layout establishes the application shell: a top identity bar, navigation and database explorer zones in the left drawer, a bounded document workspace, and a persistent status strip. `MainLayout.razor.css` uses MudBlazor palette variables so the boundaries remain consistent in light and dark themes without changing editor or result-panel behavior. Shared semantic color, spacing, control-height, and focus tokens are defined in `wwwroot/app.css` and consumed by shell, explorer, editor, result, and relationship surfaces.
