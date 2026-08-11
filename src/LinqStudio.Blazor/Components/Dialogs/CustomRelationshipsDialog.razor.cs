using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using BlazorMonaco;
using BlazorMonaco.Editor;
using LinqStudio.Abstractions.Models;
using LinqStudio.Abstractions;
using LinqStudio.Core.Models;
using LinqStudio.Core.CodeGeneration;
using LinqStudio.Core.Resources;
using LinqStudio.Blazor.Services;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace LinqStudio.Blazor.Components.Dialogs;

public partial class CustomRelationshipsDialog : ComponentBase, IDisposable
{
	[CascadingParameter]
	private IMudDialogInstance MudDialog { get; set; } = null!;

	[Parameter, EditorRequired]
	public Project Project { get; set; } = null!;

	[Parameter, EditorRequired]
	public string DatabaseName { get; set; } = string.Empty;

	[Inject]
	private ErrorHandlingService ErrorHandlingService { get; set; } = null!;

	[Inject]
	private IDbContextGenerator DbContextGenerator { get; set; } = null!;

	private readonly List<DatabaseTableDetail> _tables = [];
	private List<CustomRelationship> _relationships = [];
	private IReadOnlyDictionary<string, string> _generatedFiles = new Dictionary<string, string>();
	private CustomRelationship _editRelationship = new();
	private DatabaseTableDetail? _selectedModel;
	private string? _onConfigureCode;
	private string? _selectedGeneratedFile;
	private bool _isLoading = true;
	private bool _generatedCodeLoading;
	private bool _generatedCodeLoaded;
	private bool _generatedCodeEditorReady;
	private bool _disposed;
	private int _activeTab;
	private bool _keyPairsWereAutoSuggested;
	private StandaloneCodeEditor? _generatedCodeEditor;

	private string GeneratedCode =>
		_selectedGeneratedFile is not null
		&& _generatedFiles.TryGetValue(_selectedGeneratedFile, out var code)
			? code
			: string.Empty;

	private string SuggestedPrincipalNavigation =>
		_editRelationship.Cardinality is RelationshipCardinality.OneToMany or RelationshipCardinality.ManyToMany
			? CodeGenerationNaming.Pluralize(GetTypeName(_editRelationship.DependentTable))
			: CodeGenerationNaming.Singularize(GetTypeName(_editRelationship.DependentTable));

	private string SuggestedDependentNavigation =>
		_editRelationship.Cardinality is RelationshipCardinality.ManyToOne or RelationshipCardinality.ManyToMany
			? CodeGenerationNaming.Pluralize(GetTypeName(_editRelationship.PrincipalTable))
			: CodeGenerationNaming.Singularize(GetTypeName(_editRelationship.PrincipalTable));

	private IReadOnlyList<CustomRelationship> _selectedRelationships =>
		_selectedModel is null
			? []
			: _relationships.Where(x => x.DependentTable == _selectedModel.FullName || x.PrincipalTable == _selectedModel.FullName).ToList();

	private string FinalOnConfigureCode
	{
		get
		{
			var builder = new StringBuilder();
			if (!string.IsNullOrWhiteSpace(_onConfigureCode))
				builder.AppendLine(_onConfigureCode.Trim());

			foreach (var relationship in _relationships)
			{
				builder.AppendLine($"// {relationship.Cardinality}: {relationship.PrincipalTable} -> {relationship.DependentTable}");
				builder.AppendLine($"// Keys: {string.Join(", ", relationship.KeyPairs.Select(x => $"{x.PrincipalColumn} = {x.DependentColumn}"))}");
			}

			return builder.ToString().TrimEnd();
		}
	}

	protected override async Task OnInitializedAsync()
	{
		_onConfigureCode = Project.DbContextOnConfigureCode ?? Project.DbContextCode;
		_relationships = Clone(Project.CustomRelationships
			.Where(relationship => relationship.DatabaseName.Equals(DatabaseName, StringComparison.OrdinalIgnoreCase)));

		if (Project.QueryGenerator is null)
		{
			_isLoading = false;
			return;
		}

		try
		{
			var tableNames = await Project.QueryGenerator.GetTablesAsync(DatabaseName);
			foreach (var table in tableNames)
				_tables.Add(await Project.QueryGenerator.GetTableAsync(table));

			if (_tables.Count > 0)
				SelectModel(_tables[0]);

			await LoadGeneratedCodeAsync();
		}
		catch (Exception ex)
		{
			await ErrorHandlingService.HandleErrorAsync(ex, SharedResource.CustomRelationships_Error_LoadModels);
		}
		finally
		{
			_isLoading = false;
		}
	}

	private async Task LoadGeneratedCodeAsync()
	{
		if (Project.QueryGenerator is null)
			return;

		_generatedCodeLoading = true;
		try
		{
			var result = await DbContextGenerator.GenerateAsync(
				Project.QueryGenerator,
				DatabaseName,
				_relationships.Cast<ICustomRelationship>().ToList());
			var files = new Dictionary<string, string>(result.ModelFiles, StringComparer.OrdinalIgnoreCase)
			{
				[$"{result.ContextTypeName}.cs"] = result.DbContextCode,
			};
			_generatedFiles = files
				.OrderBy(file => file.Key.Equals($"{result.ContextTypeName}.cs", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
				.ThenBy(file => file.Key, StringComparer.OrdinalIgnoreCase)
				.ToDictionary(file => file.Key, file => file.Value, StringComparer.OrdinalIgnoreCase);
			_selectedGeneratedFile ??= _generatedFiles.Keys.FirstOrDefault();
			_generatedCodeLoaded = true;
		}
		catch (Exception ex)
		{
			await ErrorHandlingService.HandleErrorAsync(ex, SharedResource.CustomRelationships_Error_GenerateCode);
		}
		finally
		{
			_generatedCodeLoading = false;
		}
	}

	private async Task GeneratedFileChanged(string fileName)
	{
		_selectedGeneratedFile = fileName;
		if (_generatedCodeEditorReady && _generatedCodeEditor is not null)
			await _generatedCodeEditor.SetValue(GeneratedCode);
	}

	private StandaloneEditorConstructionOptions GeneratedCodeEditorOptions(StandaloneCodeEditor editor)
		=> new()
		{
			AutomaticLayout = true,
			Language = "csharp",
			Theme = "vs-dark",
			ReadOnly = true,
			Value = GeneratedCode,
		};

	private async Task OnGeneratedCodeEditorInitialized()
	{
		_generatedCodeEditorReady = true;
		if (_generatedCodeEditor is not null)
			await _generatedCodeEditor.SetValue(GeneratedCode);
	}

	private void SelectModel(DatabaseTableDetail model)
	{
		_selectedModel = model;
		StartNewRelationship();
	}

	private string? GetModelItemStyle(DatabaseTableDetail model)
		=> ReferenceEquals(_selectedModel, model)
			? "background-color: var(--mud-palette-primary-hover); border-left: 4px solid var(--mud-palette-primary);"
			: null;

	private void PrincipalTableChanged(string principalTable)
	{
		_editRelationship.PrincipalTable = principalTable;
		if (_keyPairsWereAutoSuggested)
			SuggestKeyPairs();
	}

	private void StartNewRelationship()
	{
		_editRelationship = new CustomRelationship
		{
			DependentTable = _selectedModel?.FullName ?? string.Empty,
			PrincipalTable = _tables.FirstOrDefault(x => x.FullName != _selectedModel?.FullName)?.FullName ?? string.Empty,
			KeyPairs = [new RelationshipKeyPair()],
		};
		_keyPairsWereAutoSuggested = true;
		SuggestKeyPairs();
	}

	private void EditRelationship(CustomRelationship relationship)
	{
		_selectedModel = _tables.FirstOrDefault(table =>
			table.FullName.Equals(relationship.DependentTable, StringComparison.OrdinalIgnoreCase))
			?? _selectedModel;
		_editRelationship = Clone([relationship]).Single();
		_keyPairsWereAutoSuggested = false;
	}

	private void AddKeyPair() => _editRelationship.KeyPairs.Add(new RelationshipKeyPair());

	private void DependentColumnChanged(RelationshipKeyPair pair, string value)
	{
		pair.DependentColumn = value;
		_keyPairsWereAutoSuggested = false;
	}

	private void PrincipalColumnChanged(RelationshipKeyPair pair, string value)
	{
		pair.PrincipalColumn = value;
		_keyPairsWereAutoSuggested = false;
	}

	private void SuggestKeyPairs()
	{
		if (_selectedModel is null)
			return;

		var principal = _tables.FirstOrDefault(table => table.FullName == _editRelationship.PrincipalTable);
		if (principal is null)
			return;

		var suggestions = RelationshipKeyPairDetector.Detect(_selectedModel, principal);
		if (suggestions.Count == 0)
			return;

		_editRelationship.KeyPairs = suggestions.ToList();
	}

	private void RemoveRelationship(CustomRelationship relationship) => _relationships.Remove(relationship);

	private async Task SaveRelationship()
	{
		if (string.IsNullOrWhiteSpace(_editRelationship.PrincipalTable)
			|| string.IsNullOrWhiteSpace(_editRelationship.DependentTable)
			|| _editRelationship.KeyPairs.Count == 0)
			return;

		var existing = _relationships.FindIndex(x => x.Id == _editRelationship.Id);
		if (existing >= 0)
			_relationships[existing] = _editRelationship;
		else
		{
			_editRelationship.DatabaseName = DatabaseName;
			_relationships.Add(_editRelationship);
		}

		StartNewRelationship();
		await LoadGeneratedCodeAsync();
	}

	private IEnumerable<string> GetColumns(string tableName)
		=> _tables.FirstOrDefault(x => x.FullName == tableName)?.Columns.Select(x => x.Name) ?? [];

	private static string GetTypeName(string fullName)
		=> Regex.Replace(CodeGenerationNaming.ExtractTableName(fullName), "[^a-zA-Z0-9]", string.Empty);

	private static string GetModelName(string fullName)
		=> CodeGenerationNaming.Singularize(GetTypeName(fullName));

	private string GetCardinalityTooltip()
	{
		var selected = GetModelName(_selectedModel?.FullName ?? string.Empty);
		var linked = GetModelName(_editRelationship.PrincipalTable);
		var text = _editRelationship.Cardinality switch
		{
			RelationshipCardinality.OneToOne => SharedResource.CustomRelationships_Form_Cardinality_OneToOneTooltip,
			RelationshipCardinality.OneToMany => SharedResource.CustomRelationships_Form_Cardinality_OneToManyTooltip,
			RelationshipCardinality.ManyToOne => SharedResource.CustomRelationships_Form_Cardinality_ManyToOneTooltip,
			_ => SharedResource.CustomRelationships_Form_Cardinality_ManyToManyTooltip,
		};
		return Format(text, selected, linked);
	}

	private string GetCardinalityLabel(RelationshipCardinality cardinality) =>
		cardinality switch
		{
			RelationshipCardinality.OneToOne => SharedResource.CustomRelationships_Form_Cardinality_OneToOne,
			RelationshipCardinality.OneToMany => SharedResource.CustomRelationships_Form_Cardinality_OneToMany,
			RelationshipCardinality.ManyToOne => SharedResource.CustomRelationships_Form_Cardinality_ManyToOne,
			_ => SharedResource.CustomRelationships_Form_Cardinality_ManyToMany,
		};

	private string GetDeleteBehaviorLabel(RelationshipDeleteBehavior behavior) =>
		behavior switch
		{
			RelationshipDeleteBehavior.Cascade => SharedResource.CustomRelationships_Form_DeleteBehavior_Cascade,
			RelationshipDeleteBehavior.Restrict => SharedResource.CustomRelationships_Form_DeleteBehavior_Restrict,
			RelationshipDeleteBehavior.NoAction => SharedResource.CustomRelationships_Form_DeleteBehavior_NoAction,
			RelationshipDeleteBehavior.ClientSetNull => SharedResource.CustomRelationships_Form_DeleteBehavior_ClientSetNull,
			_ => throw new ArgumentOutOfRangeException(nameof(behavior), behavior, null),
		};

	private string GetPrincipalNavigationTooltip()
	{
		var value = string.IsNullOrWhiteSpace(_editRelationship.PrincipalNavigation)
			? SuggestedPrincipalNavigation
			: _editRelationship.PrincipalNavigation;
		var entity = GetModelName(_editRelationship.PrincipalTable);
		var type = GetNavigationType(isPrincipal: true);
		return Format(SharedResource.CustomRelationships_Form_PrincipalNavigationTooltip, entity, value, type);
	}

	private string GetDependentNavigationTooltip()
	{
		var value = string.IsNullOrWhiteSpace(_editRelationship.DependentNavigation)
			? SuggestedDependentNavigation
			: _editRelationship.DependentNavigation;
		var entity = GetModelName(_editRelationship.DependentTable);
		var type = GetNavigationType(isPrincipal: false);
		return Format(SharedResource.CustomRelationships_Form_DependentNavigationTooltip, entity, value, type);
	}

	private string GetNavigationType(bool isPrincipal)
	{
		if (_editRelationship.Cardinality == RelationshipCardinality.ManyToMany)
			return $"List<{GetModelName(isPrincipal ? _editRelationship.DependentTable : _editRelationship.PrincipalTable)}>";

		if (isPrincipal && _editRelationship.Cardinality == RelationshipCardinality.OneToMany)
			return $"List<{GetModelName(_editRelationship.DependentTable)}>";

		if (!isPrincipal && _editRelationship.Cardinality == RelationshipCardinality.ManyToOne)
			return $"List<{GetModelName(_editRelationship.PrincipalTable)}>";

		return GetModelName(isPrincipal ? _editRelationship.DependentTable : _editRelationship.PrincipalTable);
	}

	private void Save()
	{
		Project.DbContextOnConfigureCode = _onConfigureCode;
		Project.CustomRelationships =
		[
			.. Project.CustomRelationships.Where(relationship =>
				!relationship.DatabaseName.Equals(DatabaseName, StringComparison.OrdinalIgnoreCase)),
			.. _relationships
		];
		MudDialog.Close(DialogResult.Ok(Project));
	}

	private void Cancel() => MudDialog.Cancel();

	protected override async Task OnAfterRenderAsync(bool firstRender)
	{
		if (!firstRender)
			return;

		await Task.Delay(500);
		if (_disposed)
			return;

		StateHasChanged();
	}

	public void Dispose()
	{
		if (_disposed)
			return;

		_disposed = true;
		GC.SuppressFinalize(this);
	}

	private static List<CustomRelationship> Clone(IEnumerable<CustomRelationship> relationships)
	{
		var json = JsonSerializer.Serialize(relationships);
		return JsonSerializer.Deserialize<List<CustomRelationship>>(json) ?? [];
	}

	private static string Format(string format, params object[] args)
		=> string.Format(SharedResource.Culture, format, args);
}
