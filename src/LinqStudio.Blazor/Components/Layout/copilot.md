# Layout components

The layout establishes the application shell: a top identity bar, navigation and database explorer zones in the left drawer, a bounded document workspace, and a persistent status strip. `MainLayout.razor.css` uses MudBlazor palette variables so the boundaries remain consistent in light and dark themes without changing editor or result-panel behavior. Shared semantic color, spacing, control-height, and focus tokens are defined in `wwwroot/app.css` and consumed by shell, explorer, editor, result, and relationship surfaces.

`DatabaseTreeView` exposes the database generator's supported table/column hierarchy with a filter field, stable selection and expansion state, inline refresh/loading/error feedback, and existing query/relationship context actions. Refreshes reuse stable node keys so an active table remains selected when the schema list changes.
