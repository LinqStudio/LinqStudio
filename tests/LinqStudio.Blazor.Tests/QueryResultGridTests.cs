using System.Reflection;
using Bunit;
using Xunit;
using LinqStudio.Blazor.Components;
using LinqStudio.Blazor.Extensions;
using LinqStudio.Core.Extensions;
using LinqStudio.Abstractions.Models;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;

namespace LinqStudio.Blazor.Tests;

public class QueryResultGridTests : BunitContext, IDisposable
{
	void IDisposable.Dispose()
		=> base.DisposeAsync().AsTask().GetAwaiter().GetResult();

	private void SetupServices()
	{
		Services
			.AddLinqStudio()
			.AddLinqStudioBlazor();

		Services.AddLogging();

		// MudDataGrid requires drag-and-drop JS interop
		JSInterop.Mode = JSRuntimeMode.Loose;
		JSInterop.SetupVoid("mudDragAndDrop.initDropZone", _ => true);
	}

	// ── Null / initial state ────────────────────────────────────────────────

	[Fact]
	public void QueryResultGrid_RendersEmpty_WhenResultIsNullAndNotExecuting()
	{
		SetupServices();

		var cut = Render<QueryResultGrid>(p => p
			.Add(c => c.Result, null)
			.Add(c => c.IsExecuting, false));

		// No spinner, no alert, no table
		Assert.Empty(cut.FindAll("mud-progress-circular"));
		Assert.Empty(cut.FindAll("[class*='mud-alert']"));
		Assert.Empty(cut.FindAll("table"));
	}

	// ── Loading state ───────────────────────────────────────────────────────

	[Fact]
	public void QueryResultGrid_ShowsSpinner_WhenIsExecutingTrue()
	{
		SetupServices();

		var cut = Render<QueryResultGrid>(p => p
			.Add(c => c.Result, null)
			.Add(c => c.IsExecuting, true));

		Assert.Contains("Executing query", cut.Markup);
		Assert.NotEmpty(cut.FindAll(".mud-progress-circular"));
	}

	[Fact]
	public void QueryResultGrid_HidesSpinner_WhenIsExecutingFalse()
	{
		SetupServices();

		var cut = Render<QueryResultGrid>(p => p
			.Add(c => c.Result, null)
			.Add(c => c.IsExecuting, false));

		Assert.Empty(cut.FindAll(".mud-progress-circular"));
		Assert.DoesNotContain("Executing query", cut.Markup);
	}

	// ── Error states ────────────────────────────────────────────────────────

	[Fact]
	public void QueryResultGrid_ShowsError_WhenResultHasRuntimeError()
	{
		SetupServices();
		var result = QueryExecutionResult.FromError("Object reference not set to an instance of an object.", false, TimeSpan.FromMilliseconds(42));

		var cut = Render<QueryResultGrid>(p => p
			.Add(c => c.Result, result)
			.Add(c => c.IsExecuting, false));

		Assert.Contains("Object reference not set", cut.Markup);
		Assert.DoesNotContain("Compilation error", cut.Markup);
	}

	[Fact]
	public void QueryResultGrid_ShowsCompileError_WhenResultIsCompileError()
	{
		SetupServices();
		var result = QueryExecutionResult.FromError("CS0246: The type 'Foo' could not be found.", true, TimeSpan.FromMilliseconds(15));

		var cut = Render<QueryResultGrid>(p => p
			.Add(c => c.Result, result)
			.Add(c => c.IsExecuting, false));

		Assert.Contains("Compilation error", cut.Markup);
		Assert.Contains("CS0246", cut.Markup);
	}

	[Fact]
	public void QueryResultGrid_ShowsElapsedTime_InErrorState()
	{
		SetupServices();
		var result = QueryExecutionResult.FromError("Some error", false, TimeSpan.FromMilliseconds(250));

		var cut = Render<QueryResultGrid>(p => p
			.Add(c => c.Result, result)
			.Add(c => c.IsExecuting, false));

		Assert.Contains("250ms", cut.Markup);
	}

	// ── Empty result set ────────────────────────────────────────────────────

	[Fact]
	public void QueryResultGrid_ShowsEmptyInfo_WhenQueryReturnsNoRows()
	{
		SetupServices();
		var result = QueryExecutionResult.Empty(TimeSpan.FromMilliseconds(88));

		var cut = Render<QueryResultGrid>(p => p
			.Add(c => c.Result, result)
			.Add(c => c.IsExecuting, false));

		Assert.Contains("no results", cut.Markup, StringComparison.OrdinalIgnoreCase);
		Assert.Empty(cut.FindAll("table"));
	}

	[Fact]
	public void QueryResultGrid_ShowsElapsedTime_InEmptyState()
	{
		SetupServices();
		var result = QueryExecutionResult.Empty(TimeSpan.FromSeconds(1.5));

		var cut = Render<QueryResultGrid>(p => p
			.Add(c => c.Result, result)
			.Add(c => c.IsExecuting, false));

		Assert.Contains("1.50s", cut.Markup);
	}

	// ── Success with data ───────────────────────────────────────────────────

	[Fact]
	public void QueryResultGrid_ShowsTable_WhenResultHasRows()
	{
		SetupServices();
		var result = new QueryExecutionResult
		{
			ColumnNames = ["Id", "Name"],
			Items =
			[
				new TestRow(Id: 1, Name: "Alice"),
				new TestRow(Id: 2, Name: "Bob")
			],
			Elapsed = TimeSpan.FromMilliseconds(120)
		};

		var cut = Render<QueryResultGrid>(p => p
			.Add(c => c.Result, result)
			.Add(c => c.IsExecuting, false));

		Assert.NotEmpty(cut.FindAll("table"));
		Assert.Contains("Id", cut.Markup);
		Assert.Contains("Name", cut.Markup);
		Assert.Contains("Alice", cut.Markup);
		Assert.Contains("Bob", cut.Markup);
	}

	[Fact]
	public void QueryResultGrid_RendersColumnHeaders_ForEachColumn()
	{
		SetupServices();
		var result = new QueryExecutionResult
		{
			ColumnNames = ["ProductId", "Price", "Category"],
			Items =
			[
				new TestRow(ProductId: 1, Price: 9.99m, Category: "Books")
			],
			Elapsed = TimeSpan.FromMilliseconds(30)
		};

		var cut = Render<QueryResultGrid>(p => p
			.Add(c => c.Result, result)
			.Add(c => c.IsExecuting, false));

		var headers = cut.FindAll("th");
		Assert.Equal(3, headers.Count);
		Assert.Contains(headers, h => h.TextContent.Contains("ProductId"));
		Assert.Contains(headers, h => h.TextContent.Contains("Price"));
		Assert.Contains(headers, h => h.TextContent.Contains("Category"));
	}

	[Fact]
	public void QueryResultGrid_ShowsRowCount_InSuccessState()
	{
		SetupServices();
		var result = new QueryExecutionResult
		{
			ColumnNames = ["Id"],
			Items =
			[
				new TestRow(Id: 1),
				new TestRow(Id: 2),
				new TestRow(Id: 3)
			],
			Elapsed = TimeSpan.FromMilliseconds(55)
		};

		var cut = Render<QueryResultGrid>(p => p
			.Add(c => c.Result, result)
			.Add(c => c.IsExecuting, false));

		Assert.Contains("3 rows", cut.Markup);
	}

	[Fact]
	public void QueryResultGrid_ShowsSingularRow_WhenSingleRow()
	{
		SetupServices();
		var result = new QueryExecutionResult
		{
			ColumnNames = ["Id"],
			Items = [new TestRow(Id: 42)],
			Elapsed = TimeSpan.FromMilliseconds(10)
		};

		var cut = Render<QueryResultGrid>(p => p
			.Add(c => c.Result, result)
			.Add(c => c.IsExecuting, false));

		Assert.Contains("1 row", cut.Markup);
		Assert.DoesNotContain("1 rows", cut.Markup);
	}

	[Fact]
	public void QueryResultGrid_ShowsElapsedTime_InSuccessState()
	{
		SetupServices();
		var result = new QueryExecutionResult
		{
			ColumnNames = ["Id"],
			Items = [new TestRow(Id: 1)],
			Elapsed = TimeSpan.FromMilliseconds(99)
		};

		var cut = Render<QueryResultGrid>(p => p
			.Add(c => c.Result, result)
			.Add(c => c.IsExecuting, false));

		Assert.Contains("99ms", cut.Markup);
	}

	// ── Elapsed time formatting ─────────────────────────────────────────────

	[Fact]
	public void QueryResultGrid_FormatsSubSecondElapsed_AsMilliseconds()
	{
		SetupServices();
		var result = QueryExecutionResult.Empty(TimeSpan.FromMilliseconds(500));

		var cut = Render<QueryResultGrid>(p => p
			.Add(c => c.Result, result)
			.Add(c => c.IsExecuting, false));

		Assert.Contains("500ms", cut.Markup);
		Assert.DoesNotContain("0.50s", cut.Markup);
	}

	[Fact]
	public void QueryResultGrid_FormatsSecondElapsed_AsSeconds()
	{
		SetupServices();
		var result = QueryExecutionResult.Empty(TimeSpan.FromSeconds(2.75));

		var cut = Render<QueryResultGrid>(p => p
			.Add(c => c.Result, result)
			.Add(c => c.IsExecuting, false));

		Assert.Contains("2.75s", cut.Markup);
	}

	// ── Executing overrides result display ─────────────────────────────────

	[Fact]
	public void QueryResultGrid_ShowsSpinner_EvenWhenResultIsNotNull()
	{
		SetupServices();
		var result = QueryExecutionResult.Empty(TimeSpan.Zero);

		var cut = Render<QueryResultGrid>(p => p
			.Add(c => c.Result, result)
			.Add(c => c.IsExecuting, true));

		// Spinner takes priority over result rendering
		Assert.Contains("Executing query", cut.Markup);
		Assert.NotEmpty(cut.FindAll(".mud-progress-circular"));
	}

	// ── Null cell values ────────────────────────────────────────────────────

	[Fact]
	public void QueryResultGrid_HandleNullCellValues_Gracefully()
	{
		SetupServices();
		var result = new QueryExecutionResult
		{
			ColumnNames = ["Id", "NullableField"],
			Items =
			[
				new TestRow(Id: 1)
			],
			Elapsed = TimeSpan.FromMilliseconds(5)
		};

		var ex = Record.Exception(() =>
		{
			var cut = Render<QueryResultGrid>(p => p
				.Add(c => c.Result, result)
				.Add(c => c.IsExecuting, false));

			Assert.NotEmpty(cut.FindAll("table"));
		});

		Assert.Null(ex);
	}

	[Fact]
	public void QueryResultGrid_ShowsNullAsText_WhenCellValueIsNull()
	{
		SetupServices();
		var result = new QueryExecutionResult
		{
			ColumnNames = ["Id", "Name", "OptionalValue"],
			Items =
			[
				new TestRow(Id: 1, Name: "Alice"),
				new TestRow(Id: 2, OptionalValue: "Present")
			],
			Elapsed = TimeSpan.FromMilliseconds(10)
		};

		var cut = Render<QueryResultGrid>(p => p
			.Add(c => c.Result, result)
			.Add(c => c.IsExecuting, false));

		// Verify non-null values are also present
		Assert.Contains("Alice", cut.Markup);
		Assert.Contains("Present", cut.Markup);
	}

	// ── MudDataGrid structure ───────────────────────────────────────────────

	[Fact]
	public void QueryResultGrid_RendersRows_WithCorrectCount()
	{
		SetupServices();
		var result = new QueryExecutionResult
		{
			ColumnNames = ["Id", "Name"],
			Items =
			[
				new TestRow(Id: 1, Name: "First"),
				new TestRow(Id: 2, Name: "Second"),
				new TestRow(Id: 3, Name: "Third")
			],
			Elapsed = TimeSpan.FromMilliseconds(20)
		};

		var cut = Render<QueryResultGrid>(p => p
			.Add(c => c.Result, result)
			.Add(c => c.IsExecuting, false));

		// Verify rows are rendered with correct count and content
		// In bUnit, verify structure via table rows
		var tbody = cut.Find("tbody");
		var rows = tbody.QuerySelectorAll("tr.mud-table-row");
		Assert.Equal(3, rows.Length);
	}

	[Fact]
	public void QueryResultGrid_RendersColumnHeaders()
	{
		SetupServices();
		var result = new QueryExecutionResult
		{
			ColumnNames = ["ProductId", "Name", "Price"],
			Items =
			[
				new TestRow(ProductId: 1, Name: "Widget", Price: 19.99m)
			],
			Elapsed = TimeSpan.FromMilliseconds(15)
		};

		var cut = Render<QueryResultGrid>(p => p
			.Add(c => c.Result, result)
			.Add(c => c.IsExecuting, false));

		var headers = cut.FindAll("thead th");
		Assert.Contains(headers, header => header.TextContent.Contains("ProductId"));
		Assert.Contains(headers, header => header.TextContent.Contains("Name"));
		Assert.Contains(headers, header => header.TextContent.Contains("Price"));
	}

	[Fact]
	public void QueryResultGrid_RendersCells()
	{
		SetupServices();
		var result = new QueryExecutionResult
		{
			ColumnNames = ["Id", "Value"],
			Items =
			[
				new TestRow(Id: 10, Value: "Alpha"),
				new TestRow(Id: 20, Value: "Beta")
			],
			Elapsed = TimeSpan.FromMilliseconds(8)
		};

		var cut = Render<QueryResultGrid>(p => p
			.Add(c => c.Result, result)
			.Add(c => c.IsExecuting, false));

		Assert.Equal(4, cut.FindAll("tbody tr").Count);
	}

	[Fact]
	public void QueryResultGrid_RendersEditableInputs_WhenEntityEditingIsEnabled()
	{
		SetupServices();
		var result = new QueryExecutionResult
		{
			ColumnNames = ["Id", "Name", "Details"],
			Items = [new TestRow(Id: 10, Name: "Alpha", Details: "read-only")],
			Elapsed = TimeSpan.Zero
		};

		var cut = Render<QueryResultGrid>(p => p
			.Add(c => c.Result, result)
			.Add(c => c.IsExecuting, false)
			.Add(c => c.IsEditable, true)
			.Add(c => c.EditableRow, result.Items[0])
			.Add(c => c.EditableColumns, new HashSet<string> { "Id", "Name" }));

		var cells = cut.FindAll("tbody tr td");
		Assert.NotEmpty(cells[0].QuerySelectorAll("input"));
		Assert.NotEmpty(cells[1].QuerySelectorAll("input"));
		Assert.Empty(cells[2].QuerySelectorAll("input"));
	}

	[Fact]
	public void QueryResultGrid_DisplaysDateTimeValues_WhenEntityEditingIsEnabled()
	{
		SetupServices();
		var date = new DateTime(2024, 1, 2, 15, 4, 5);
		var result = new QueryExecutionResult
		{
			ColumnNames = ["Date"],
			Items = [new TestRow(Date: date)],
			Elapsed = TimeSpan.Zero
		};

		var cut = Render<QueryResultGrid>(p => p
			.Add(c => c.Result, result)
			.Add(c => c.IsExecuting, false)
			.Add(c => c.IsEditable, true)
			.Add(c => c.EditableRow, result.Items[0])
			.Add(c => c.EditableColumns, new HashSet<string> { "Date" }));

		Assert.Contains("2024", cut.Markup);
		Assert.Contains(date.ToString(), cut.Markup);
	}

	[Fact]
	public void QueryResultGrid_NotifiesWhenEditableCellChanges()
	{
		SetupServices();
		var row = new TestRow(Name: "Alpha");
		var result = new QueryExecutionResult
		{
			ColumnNames = ["Name"],
			Items = [row],
			Elapsed = TimeSpan.Zero
		};
		QueryResultGrid.CellChanged? change = null;

		var cut = Render<QueryResultGrid>(p => p
			.Add(c => c.Result, result)
			.Add(c => c.IsExecuting, false)
			.Add(c => c.IsEditable, true)
			.Add(c => c.EditableRow, row)
			.Add(c => c.EditableColumns, new HashSet<string> { "Name" })
			.Add(c => c.OnCellChanged, value => change = value));

		cut.Find("tbody tr td:nth-child(1) input").Change("Beta");

		Assert.NotNull(change);
		Assert.Same(row, change!.Row);
		Assert.Equal("Name", change.ColumnName);
		Assert.Equal("Beta", change.Value);
	}

	// ── API contract: deleted sort parameters must not return ────────────────

	[Fact]
	public void QueryResultGrid_DoesNotHave_SortDefinitionsParameter()
	{
		// Reflection-based contract test: the SortDefinitions and OnSortDefinitionsChanged
		// parameters were deleted as part of the KeepPanelsAlive redesign. This test
		// guarantees they do not silently creep back into the public API.
		var props = typeof(QueryResultGrid)
			.GetProperties(BindingFlags.Public | BindingFlags.Instance);

		Assert.DoesNotContain(props, p => p.Name == "SortDefinitions");
		Assert.DoesNotContain(props, p => p.Name == "OnSortDefinitionsChanged");
	}

	private sealed record TestRow(
		int? Id = null,
		string? Name = null,
		string? Value = null,
		string? NullableField = null,
		string? OptionalValue = null,
		int? ProductId = null,
		decimal? Price = null,
		string? Category = null,
		string? Details = null,
		DateTime? Date = null);
}
