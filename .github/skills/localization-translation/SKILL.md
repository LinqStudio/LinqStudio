---
name: localization-translation
description: >
  Add and use LinqStudio's English and French UI translations. Use when adding
  user-visible text, changing resource keys, translating labels or errors, or
  reviewing localized Blazor code.
---

# Localization and translation

Use this skill whenever changing user-visible text in Blazor components,
settings, dialogs, errors, or other UI code.

## Add a translation

1. Choose a stable, descriptive dotted resource key using the existing naming
   conventions.
2. Add the key and English value to
   `src/LinqStudio.Core/Resources/SharedResource.resx`.
3. Add the exact same key and French value to
   `src/LinqStudio.Core/Resources/SharedResource.fr.resx`.
4. Preserve format placeholders such as `{0}` exactly in both values.
5. Have the developer regenerate `SharedResource.Designer.cs` in Visual Studio
   using the `.resx` custom tool. If the developer is not available (if this is a background agent running independently) make sure to modify the designer file in the exact same way it would be if done automatically. If it doesn't work, don't be stubborn and just leave, instruct the next agent or the developer to regenerate the translation file.
6. Use the generated strongly typed `SharedResource` property in code.

## Generated designer rule

`src/LinqStudio.Core/Resources/SharedResource.Designer.cs` is generated code.
AI agents must not edit, hand-author, generate, or repeatedly “fix” it. Adding
a `.resx` key does not make its C# property available until the developer
regenerates the designer through Visual Studio. If regeneration has not happened,
do not write code referencing a missing property; report that regeneration is
required.

## Use translations in code

Use generated typed properties for normal UI text:

```razor
@SharedResource.DatabaseTreeView_SearchLabel
```

```csharp
await ErrorHandlingService.HandleErrorAsync(
    ex, SharedResource.DatabaseTreeView_LoadErrorDialog);
```

Resource keys use dots in `.resx`; Visual Studio generates underscore properties.
For example, `DatabaseTreeView.LoadErrorDialog` becomes
`SharedResource.DatabaseTreeView_LoadErrorDialog`.

Do not use hard-coded user-facing fallback strings such as:

```csharp
Text("DatabaseTreeView.LoadErrorDialog", "Failed to load database tables.")
```

Use `ResourceManager` only for genuinely dynamic keys that cannot use a typed
property. Such lookups must use the original dotted key and
`SharedResource.Culture`.

## Naming conventions

Use the existing resource prefixes:

- `Shell.<Element>.<Name>`
- `QueryEditor.<Area>.<Name>`
- `QueryResultGrid.<Name>`
- `SettingsPage.<Area>.<Name>`
- `Global.<Category>.<Name>`
- `UserSettings.<SectionName>` and its properties
- `DatabaseTreeView.<Area>.<Name>`
- `CustomRelationships.<Area>.<Name>`

## Culture and validation

English is the neutral/default resource and French is the `fr` resource.
Standard .NET resource lookup follows the ambient UI culture and falls back to
English. A `.resx` file alone does not enable a new application locale.

Before finishing:

- Verify every English key exists exactly once in the French resource and vice
  versa.
- Verify corresponding placeholders match.
- Search changed UI code for hard-coded user-facing strings.
- Leave `SharedResource.Designer.cs` for Visual Studio regeneration.

On Windows, compare source keys with:

```powershell
$en = [xml](Get-Content -Raw 'src\LinqStudio.Core\Resources\SharedResource.resx')
$fr = [xml](Get-Content -Raw 'src\LinqStudio.Core\Resources\SharedResource.fr.resx')
$ek = @($en.root.data | ForEach-Object name)
$fk = @($fr.root.data | ForEach-Object name)
Compare-Object $ek $fk
```

No output means the key sets match.
