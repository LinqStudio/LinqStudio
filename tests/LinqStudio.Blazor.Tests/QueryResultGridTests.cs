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

public class QueryResultGridTests : BunitContext
{
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
			Rows =
			[
				new Dictionary<string, object?> { ["Id"] = 1, ["Name"] = "Alice" },
				new Dictionary<string, object?> { ["Id"] = 2, ["Name"] = "Bob" }
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
			Rows =
			[
				new Dictionary<string, object?> { ["ProductId"] = 1, ["Price"] = 9.99m, ["Category"] = "Books" }
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
			Rows =
			[
				new Dictionary<string, object?> { ["Id"] = 1 },
				new Dictionary<string, object?> { ["Id"] = 2 },
				new Dictionary<string, object?> { ["Id"] = 3 }
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
			Rows = [new Dictionary<string, object?> { ["Id"] = 42 }],
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
			Rows = [new Dictionary<string, object?> { ["Id"] = 1 }],
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
			Rows =
			[
				new Dictionary<string, object?> { ["Id"] = 1, ["NullableField"] = null }
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
			Rows =
			[
				new Dictionary<string, object?> { ["Id"] = 1, ["Name"] = "Alice", ["OptionalValue"] = null },
				new Dictionary<string, object?> { ["Id"] = 2, ["Name"] = null, ["OptionalValue"] = "Present" }
			],
			Elapsed = TimeSpan.FromMilliseconds(10)
		};

		var cut = Render<QueryResultGrid>(p => p
			.Add(c => c.Result, result)
			.Add(c => c.IsExecuting, false));

		// Verify "NULL" text appears in markup for null values
		Assert.Contains("NULL", cut.Markup);
		// Verify non-null values are also present
		Assert.Contains("Alice", cut.Markup);
		Assert.Contains("Present", cut.Markup);
	}

	// ── MudDataGrid structure ───────────────────────────────────────────────

	[Fact]
	public void QueryResultGrid_RendersRows_WithCorrectTestIds()
	{
		SetupServices();
		var result = new QueryExecutionResult
		{
			ColumnNames = ["Id", "Name"],
			Rows =
			[
				new Dictionary<string, object?> { ["Id"] = 1, ["Name"] = "First" },
				new Dictionary<string, object?> { ["Id"] = 2, ["Name"] = "Second" },
				new Dictionary<string, object?> { ["Id"] = 3, ["Name"] = "Third" }
			],
			Elapsed = TimeSpan.FromMilliseconds(20)
		};

		var cut = Render<QueryResultGrid>(p => p
			.Add(c => c.Result, result)
			.Add(c => c.IsExecuting, false));

		// Verify rows are rendered (data-testid is added by JS in E2E, not in bUnit)
		// In bUnit, verify structure via table rows
		var tbody = cut.Find("tbody");
		var rows = tbody.QuerySelectorAll("tr.mud-table-row");
		Assert.Equal(3, rows.Length);

		// Verify row content
		Assert.Contains("First", rows[0].TextContent);
		Assert.Contains("Second", rows[1].TextContent);
		Assert.Contains("Third", rows[2].TextContent);
	}

	[Fact]
	public void QueryResultGrid_RendersColumnHeaders_WithCorrectTestIds()
	{
		SetupServices();
		var result = new QueryExecutionResult
		{
			ColumnNames = ["ProductId", "Name", "Price"],
			Rows =
			[
				new Dictionary<string, object?> { ["ProductId"] = 1, ["Name"] = "Widget", ["Price"] = 19.99m }
			],
			Elapsed = TimeSpan.FromMilliseconds(15)
		};

		var cut = Render<QueryResultGrid>(p => p
			.Add(c => c.Result, result)
			.Add(c => c.IsExecuting, false));

		// Verify column headers have data-testid attributes
		var headerProductId = cut.Find("[data-testid='column-header-ProductId']");
		Assert.NotNull(headerProductId);
		Assert.Contains("ProductId", headerProductId.TextContent);

		var headerName = cut.Find("[data-testid='column-header-Name']");
		Assert.NotNull(headerName);
		Assert.Contains("Name", headerName.TextContent);

		var headerPrice = cut.Find("[data-testid='column-header-Price']");
		Assert.NotNull(headerPrice);
		Assert.Contains("Price", headerPrice.TextContent);
	}

	[Fact]
	public void QueryResultGrid_RendersCells_WithCorrectTestIds()
	{
		SetupServices();
		var result = new QueryExecutionResult
		{
			ColumnNames = ["Id", "Value"],
			Rows =
			[
				new Dictionary<string, object?> { ["Id"] = 10, ["Value"] = "Alpha" },
				new Dictionary<string, object?> { ["Id"] = 20, ["Value"] = "Beta" }
			],
			Elapsed = TimeSpan.FromMilliseconds(8)
		};

		var cut = Render<QueryResultGrid>(p => p
			.Add(c => c.Result, result)
			.Add(c => c.IsExecuting, false));

		// Verify cells have data-testid="cell-{rowIndex}-{columnName}"
		var cell00 = cut.Find("[data-testid='cell-0-Id']");
		Assert.NotNull(cell00);
		Assert.Contains("10", cell00.TextContent);

		var cell01 = cut.Find("[data-testid='cell-0-Value']");
		Assert.NotNull(cell01);
		Assert.Contains("Alpha", cell01.TextContent);

		var cell10 = cut.Find("[data-testid='cell-1-Id']");
		Assert.NotNull(cell10);
		Assert.Contains("20", cell10.TextContent);

		var cell11 = cut.Find("[data-testid='cell-1-Value']");
		Assert.NotNull(cell11);
		Assert.Contains("Beta", cell11.TextContent);
	}
}
