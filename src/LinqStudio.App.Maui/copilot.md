# LinqStudio.App.Maui

MAUI Blazor Hybrid app hosting LinqStudio. Targets Windows only (`net10.0-windows10.0.19041.0`).
Uses `FileSystemProjectRepository`/`FileSystemQueryRepository` from LinqStudio.Core with `~/Documents/LinqStudio/Projects/` as the base path.
Reuses all LinqStudio.Blazor components (same routing and layout as WebServer).
`usersettings.json` is loaded via `builder.Configuration.AddJsonFile` so settings persist across restarts.
