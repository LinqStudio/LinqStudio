namespace LinqStudio.App.WebServer.E2ETests.Helpers;

/// <summary>
/// Centralized selectors used by the Playwright tests. Keep test IDs here so UI
/// selector changes have one obvious place to update.
/// </summary>
internal static class E2ESelectors
{
	public const string ProjectMenu = "nav-project";
	public const string ProjectNew = "nav-project-new";
	public const string ProjectOpen = "nav-project-open";
	public const string ProjectProperties = "nav-project-properties";
	public const string ProjectSave = "nav-project-save";
	public const string ProjectSaveAs = "nav-project-save-as";
	public const string ProjectClose = "nav-project-close";

	public const string ProjectBrowserDialog = "project-browser-dialog";
	public const string ProjectListItem = "project-list-item";
	public const string ProjectBrowserOpenButton = "project-browser-open-btn";
	public const string ProjectBrowserSaveButton = "project-browser-save-btn";
	public const string ProjectNameInput = "project-name-input";
	public const string ProjectConnectionStringField = "project-connection-string-field";
	public const string EditProjectDialog = "edit-project-dialog";
	public const string EditProjectSaveButton = "edit-project-save-btn";

	public const string UnsavedChangesDialog = "unsaved-changes-dialog";
	public const string UnsavedChangesCancelButton = "unsaved-changes-cancel-btn";
	public const string UnsavedChangesConfirmButton = "unsaved-changes-confirm-btn";
	public const string NoQueryAlert = "no-query-alert";

	public const string ExecuteQueryButton = "execute-query-btn";
	public const string StopQueryButton = "stop-query-btn";
	public const string TimeoutSelect = "timeout-select";
	public const string QueryResultContainer = "query-result-container";
	public const string QueryCloseButton = "query-close-btn";
	public const string QueryUnsavedIndicator = "query-unsaved-indicator";
	public const string QueryExecutionBar = "query-execution-bar";
	public const string EditorPage = "editor-page";
	public const string MonacoEditorContainer = "monaco-editor-container";
	public const string EditorResultsSplitter = "editor-results-splitter";

	public const string DatabaseTreeView = "db-tree-view";
	public const string DatabaseTreePlaceholder = "db-tree-placeholder";
	public const string DatabaseTreeRefresh = "db-tree-refresh";
	public const string DatabaseTreeConnection = "db-tree-connection";
	public const string DatabaseTreeConnectionBody = "db-tree-connection-body";
	public const string DatabaseTreeConnectionNewQuery = "db-tree-connection-new-query";
	public const string DatabaseTreeTablesFolder = "db-tree-tables-folder";
	public const string DatabaseTreeTablesFolderRefresh = "db-tree-tables-folder-refresh";

	public const string SelectionCount = "selection-count";
	public const string CSharpTabPanel = "csharp-tab-panel";
	public const string SqlTabPanel = "sql-tab-panel";
	public const string ResultsTabs = "results-tabs";

	public const string MudTab = ".mud-tab";
	public const string MudTableRoot = ".mud-table-root";
	public const string MudTable = ".mud-table";
	public const string MudAlert = ".mud-alert";
	public const string MudProgressCircular = ".mud-progress-circular";
	public const string MudListItem = ".mud-list-item";
	public const string MudSnackbar = ".mud-snackbar";
	public const string MudTimePickerClock = ".mud-picker-time-clock-mask";
	public const string MudTimePickerHour = ".mud-picker-stick-inner[data-stick-value='6']";
	public const string MudTimePickerMinute = ".mud-time-picker-minute";
	public const string QueryResultGridContainer = ".query-result-grid-container";
	public const string QueryResultOrError = ".mud-table, .mud-alert";
	public const string QueryExecutingText = "text=Executing query...";
	public const string QueryColumnHeader = "thead th";
	public const string QueryDisabledIndicator = "[aria-disabled='true'], .mud-disabled, input[disabled]";
	public const string QueryTimeoutOption = ".mud-list-item";
	public const string GridCellInput = "input";
	public const string RowSelected = ".row-selected";
	public const string MonacoEditor = ".monaco-editor";
	public const string MonacoInputArea = "textarea.inputarea";
	public const string MonacoViewLines = ".view-lines";
	public const string MonacoViewLine = ".view-lines .view-line";
	public const string MonacoSuggestRows = ".suggest-widget.visible .monaco-list-row";
	public const string MonacoSuggestRowsAny = ".suggest-widget .monaco-list-row";
	public const string MonacoHoverContent = ".monaco-hover .hover-contents";
	public const string MonacoToken = "span";
	public const string TabPanel = "[role='tabpanel']";
	public const string TabPanelAncestor = "xpath=ancestor::*[@role='tabpanel'][1]";

	public static string Table(string fullName) => $"table-{fullName}";

	public static string Column(string tableName, string columnName) => $"column-{tableName}-{columnName}";

	public static string TableRefresh(string fullName) => $"db-tree-table-refresh-{fullName}";
}
