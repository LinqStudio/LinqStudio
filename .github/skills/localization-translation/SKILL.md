---
name: linqstudio-localization
description: >
  Add, review, and validate LinqStudio's English and French UI translations.
  Use when adding user-visible text, changing resource keys, translating settings
  labels or errors, checking culture behavior, or reviewing resource-generated
  code.
---

# LinqStudio localization

Use this skill for any user-visible text in Blazor components, settings labels,
dialogs, errors, or other UI code. Read the localization sections of
`.github/skills/project-conventions/SKILL.md` and
`.github/skills/blazor-frontend/SKILL.md` before changing translations.

## Source files and generated code

The two source translation files are:

- `src/LinqStudio.Core/Resources/SharedResource.resx` — English/default values.
- `src/LinqStudio.Core/Resources/SharedResource.fr.resx` — French values.

The project file configures the default resource with
`PublicResXFileCodeGenerator`, `LastGenOutput=SharedResource.Designer.cs`, and
marks the designer as `AutoGen`/`DependentUpon`. The tracked
`src/LinqStudio.Core/Resources/SharedResource.Designer.cs` is generated code.

**Never manually edit, hand-author, or generate `SharedResource.Designer.cs` as
an AI agent.** When a developer needs updated strongly typed properties, they
must regenerate it through Visual Studio's resource tooling (the `.resx`
custom tool), then review the generated diff. The designer may be reviewed for
consistency, but it is not a source file. If it is already changed in a
working tree, do not repeatedly reformat or regenerate it; leave ownership of
regeneration to the developer.

## Adding a translation

1. Identify the UI area and choose a descriptive resource key using the existing
   dotted hierarchy. Keep the key stable; it is an identifier, not prose.
2. Add the key to **both** source `.resx` files. The English entry belongs in
   `SharedResource.resx`; the French translation with the exact same key belongs
   in `SharedResource.fr.resx`.
3. Preserve format placeholders exactly (`{0}`, `{1}`, etc.) and keep matching
   plural/format variants together. Translate the value, not the key.
4. Do not add a hard-coded user-facing fallback in Razor or C# when a resource
   key should be used. A fallback for a dynamic lookup may be intentional, but
   it must be a safe key/name fallback rather than new UI copy.
5. If code needs a strongly typed property that is not currently present, stop
   after editing the `.resx` files and tell the developer to regenerate the
   designer in Visual Studio. Do not make the generated file compile by hand.

### Naming conventions

Use the conventions already present in the resources:

| Context | Pattern | Examples |
| --- | --- | --- |
| Shell/layout | `Shell.<Element>.<Name>` | `Shell.Navigation.Toggle` |
| Editor/query UI | `QueryEditor.<Area>.<Name>` | `QueryEditor.Message.Empty` |
| Result grid | `QueryResultGrid.<Name>` | `QueryResultGrid.RowCount` |
| Settings page | `SettingsPage.<Area>.<Name>` | `SettingsPage.Error.ErrorSavingSetting` |
| Shared messages | `Global.<Category>.<Name>` | `Global.MessageBox.Yes` |
| Settings section | `UserSettings.<SectionName>` | `UserSettings.UISettings` |
| Settings property | `UserSettings.<SectionName>.<PropertyName>` | `UserSettings.UISettings.IsDarkMode` |
| Connection UI | `ConnectionSettings.<Area>.<Name>` | `ConnectionSettings.Message.Saved` |
| Database tree | `DatabaseTreeView.ContextMenu.<Action>` | `DatabaseTreeView.ContextMenu.Refresh` |
| Relationship dialog | `CustomRelationships.<Area>.<Name>` | `CustomRelationships.Form.Save` |

Resource keys contain dots. The generated C# property converts dots to
underscores (for example, `Global.MessageBox.Yes` becomes
`SharedResource.Global_MessageBox_Yes`). Dynamic lookups must use the original
dotted key with `SharedResource.ResourceManager.GetString(key,
`SharedResource.Culture)`.

## Culture behavior

The application uses standard .NET `ResourceManager` resolution. English is the
neutral/default resource and French is the `fr` resource. `SharedResource.Culture`
is nullable and defaults to `null`, so lookups normally follow the ambient
`CurrentUICulture` and fall back to the neutral English resource. The French
resource is selected when the runtime UI culture is French (including normal
parent-culture fallback such as `fr-FR` → `fr`).

The current WebServer `Program.cs` does not register ASP.NET Core localization
middleware, a culture selector, or an explicit supported-culture list. Do not
claim that a new locale is enabled merely because a `.resx` file exists; a
developer must also add the application culture-selection/configuration path.
Existing code passes `SharedResource.Culture` for dynamic lookups and
`string.Format`, so preserve that pattern.

## Validation checklist

Before finishing a localization change:

- Check that every key in the English file exists exactly once in the French
  file, and vice versa. The current files contain matching key sets.
- Check that placeholders in corresponding values match and that translated
  values are not accidentally English where a French translation is expected.
- Check that user-visible strings are read through `SharedResource` (or an
  intentional `ResourceManager` lookup), including settings section/property
  labels.
- Do not treat `SharedResource.Designer.cs` changes as a required AI-generated
  step. If strongly typed members are needed, request developer regeneration in
  Visual Studio and review the result afterward.
- Build/test only when the surrounding code change requires it; a translation
  resource-only review should at minimum parse both XML files and compare keys.

On Windows, this PowerShell check compares the two source key sets without
writing temporary files:

```powershell
$en = [xml](Get-Content -Raw 'src\LinqStudio.Core\Resources\SharedResource.resx')
$fr = [xml](Get-Content -Raw 'src\LinqStudio.Core\Resources\SharedResource.fr.resx')
$ek = @($en.root.data | ForEach-Object name)
$fk = @($fr.root.data | ForEach-Object name)
Compare-Object $ek $fk
```

No output means the key sets match. Also inspect the resource diff for
translation quality and placeholder parity; key-set equality alone does not
prove that a translation is correct.

## Avoiding generated-file churn

Resource edits and generated-code edits are separate responsibilities. An agent
should change only the two source `.resx` files, keep the generated designer
untouched, and report that Visual Studio regeneration is required if a new
strongly typed property is needed. On a later task, do not "fix" an old
designer diff by regenerating it again: review it, preserve unrelated developer
changes, and ask the developer to regenerate from the current English resource
when they are ready.
