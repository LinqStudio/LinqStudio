# Layout Components

## MainLayout

`MainLayout.razor` is the primary application shell. It owns:
- **AppBar**: project lifecycle menu (`nav-project` testid), Open/Save/SaveAs icon buttons, Settings, dark mode toggle.
- **Drawer**: navigation links (Home, Editor) + `ConnectionTreeView`.
- **Startup dialog**: On first render, if no project is open, automatically shows the "Open Project" browser dialog.

Project actions previously in `NavMenu.razor.cs` (New, Open, Save, Save As, Close, Properties) are now inlined in `MainLayout.razor`'s `@code {}` block. Testids (`nav-project`, `nav-project-new`, `nav-project-open`, etc.) are preserved on `<MudButton data-testid="nav-project">` in the app bar for E2E test compatibility.

## ConnectionTreeView

Unified component replacing the legacy `NavMenu` + `DatabaseTreeView`. Located in the drawer.

**Tree structure:**
```
Servers  (MudMenu: "New Connection")
└── 127.0.0.1,1433 (MSSQL)  (MudMenu: "Connection Properties", "Disconnect")
    └── AdventureWorks  (MudMenu: "New Query")
        └── Person  (table)
            └── Columns
                ├── Id  int PK
                └── Name  nvarchar(50)?
```

**Key design decisions:**
- **Eager column loading**: `LoadTablesForConnectionAsync()` loads all tables AND their column details in one pass on project open. Columns needed upfront for EF Core model generation.
- **Per-connection state**: `_loadingStates`, `_loadErrors`, `_tableDetailsCache` keyed by `ServerConnection.Id`.
- **WorkspaceChanged handler**: Removes state for deleted connections and loads new ones automatically.
- **New Query**: Creates a `SavedQuery` with `ConnectionId` set to the selected connection. Navigates to `/editor/{queryId}`.
- **ChildContent** is required when combining `<Content>` + child `<MudTreeViewItem>` in the same item.

**Testids preserved for E2E compat:** `db-tree-placeholder` (no project / no connections), `db-tree-loading`, `table-{name}`, `column-{table}-{col}`.

## Removed components
- `NavMenu.razor` / `NavMenu.razor.cs` — project actions moved to `MainLayout`; nav links inlined in drawer.
- `DatabaseTreeView.razor` / `DatabaseTreeView.razor.cs` — replaced by `ConnectionTreeView`.
