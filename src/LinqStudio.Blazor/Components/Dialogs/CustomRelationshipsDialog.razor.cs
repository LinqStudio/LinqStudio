using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using LinqStudio.Abstractions.Models;
using LinqStudio.Core.Models;
using LinqStudio.Core.CodeGeneration;
using LinqStudio.Core.Resources;
using LinqStudio.Blazor.Services;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace LinqStudio.Blazor.Components.Dialogs;

public partial class CustomRelationshipsDialog : ComponentBase
{
	[CascadingParameter]
	private IMudDialogInstance MudDialog { get; set; } = null!;

	[Parameter, EditorRequired]
	public Project Project { get; set; } = null!;

	[Inject]
	private ErrorHandlingService ErrorHandlingService { get; set; } = null!;

	private readonly List<DatabaseTableDetail> _tables = [];
	private List<CustomRelationship> _relationships = [];
	private CustomRelationship _editRelationship = new();
	private DatabaseTableDetail? _selectedModel;
	private string? _onConfigureCode;
	private bool _isLoading = true;
	private int _activeTab;
	private bool _keyPairsWereAutoSuggested;

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
		_relationships = Clone(Project.CustomRelationships);

		if (Project.QueryGenerator is null)
		{
			_isLoading = false;
			return;
		}

		try
		{
			var tableNames = await Project.QueryGenerator.GetTablesAsync();
			foreach (var table in tableNames)
				_tables.Add(await Project.QueryGenerator.GetTableAsync(table.FullName));

			if (_tables.Count > 0)
				SelectModel(_tables[0]);
		}
		catch (Exception ex)
		{
			await ErrorHandlingService.HandleErrorAsync(ex, "Failed to load database models.");
		}
		finally
		{
			_isLoading = false;
		}
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

	private void SaveRelationship()
	{
		if (string.IsNullOrWhiteSpace(_editRelationship.PrincipalTable)
			|| string.IsNullOrWhiteSpace(_editRelationship.DependentTable)
			|| _editRelationship.KeyPairs.Count == 0)
			return;

		var existing = _relationships.FindIndex(x => x.Id == _editRelationship.Id);
		if (existing >= 0)
			_relationships[existing] = _editRelationship;
		else
			_relationships.Add(_editRelationship);

		StartNewRelationship();
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
		var key = _editRelationship.Cardinality switch
		{
			RelationshipCardinality.OneToOne => "CustomRelationships.Form.Cardinality.OneToOneTooltip",
			RelationshipCardinality.OneToMany => "CustomRelationships.Form.Cardinality.OneToManyTooltip",
			RelationshipCardinality.ManyToOne => "CustomRelationships.Form.Cardinality.ManyToOneTooltip",
			_ => "CustomRelationships.Form.Cardinality.ManyToManyTooltip",
		};
		var fallback = _editRelationship.Cardinality switch
		{
			RelationshipCardinality.OneToOne => "One {0} is linked to one {1}.",
			RelationshipCardinality.OneToMany => "One {1} can be linked to many {0}.",
			RelationshipCardinality.ManyToOne => "Many {1} can be linked to one {0}.",
			_ => "Many {0} can be linked to many {1}.",
		};
		return TextFormat(key, fallback, selected, linked);
	}

	private string GetPrincipalNavigationTooltip()
	{
		var value = string.IsNullOrWhiteSpace(_editRelationship.PrincipalNavigation)
			? SuggestedPrincipalNavigation
			: _editRelationship.PrincipalNavigation;
		var entity = GetModelName(_editRelationship.PrincipalTable);
		var type = GetNavigationType(isPrincipal: true);
		return TextFormat(
			"CustomRelationships.Form.PrincipalNavigationTooltip",
			$"The property {value} will be added to the {entity} class: {type} {value} {{ get; set; }}",
			entity, value, type);
	}

	private string GetDependentNavigationTooltip()
	{
		var value = string.IsNullOrWhiteSpace(_editRelationship.DependentNavigation)
			? SuggestedDependentNavigation
			: _editRelationship.DependentNavigation;
		var entity = GetModelName(_editRelationship.DependentTable);
		var type = GetNavigationType(isPrincipal: false);
		return TextFormat(
			"CustomRelationships.Form.DependentNavigationTooltip",
			$"The property {value} will be added to the {entity} class: {type} {value} {{ get; set; }}",
			entity, value, type);
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
		Project.CustomRelationships = _relationships;
		MudDialog.Close(DialogResult.Ok(Project));
	}

	private void Cancel() => MudDialog.Cancel();

	private static List<CustomRelationship> Clone(IEnumerable<CustomRelationship> relationships)
	{
		var json = JsonSerializer.Serialize(relationships);
		return JsonSerializer.Deserialize<List<CustomRelationship>>(json) ?? [];
	}

	private static string Text(string key, string fallback)
		=> SharedResource.ResourceManager.GetString(key, SharedResource.Culture) ?? fallback;

	private static string TextFormat(string key, string fallback, params object[] args)
		=> string.Format(SharedResource.Culture, Text(key, fallback), args);
}
