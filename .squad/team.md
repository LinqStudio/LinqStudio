# Squad Team

> LinqStudio

## Coordinator

| Name | Role | Notes |
|------|------|-------|
| Squad | Coordinator | Routes work, enforces handoffs and reviewer gates. |

## Members

| Name | Role | Charter | Status |
|------|------|---------|--------|
| Samy | 🏗️ Analyst / Architect | `.squad/agents/samy/charter.md` | ✅ Active |
| EvilJosh | ⚛️ Frontend Dev | `.squad/agents/eviljosh/charter.md` | ✅ Active |
| Simon | 🔧 Backend Core Dev | `.squad/agents/simon/charter.md` | ✅ Active |
| Jordan | 🧪 Tests Dev | `.squad/agents/jordan/charter.md` | ✅ Active |
| Alice | 🎭 Live Tester | `.squad/agents/alice/charter.md` | ✅ Active |
| Scribe | 📋 Session Logger | `.squad/agents/scribe/charter.md` | ✅ Active |
| Ralph | 🔄 Work Monitor | — | ✅ Active |

## Project Context

- **Owner:** snakex64
- **Project:** LinqStudio — IDE-like interface for writing and executing EF Core LINQ queries. Replaces SSMS. Multi-DB support. BlazorMonaco editor with live intellisense.
- **Stack:** .NET 10, Blazor Server, MudBlazor, BlazorMonaco, Roslyn, EF Core, Aspire, XUnit, Playwright
- **App URLs:** http://localhost:5077 (HTTP) · https://localhost:7169 (HTTPS)
- **Build:** `./build.ps1 Test` (Nuke) · `dotnet build` · `dotnet run --project src/LinqStudio.App.WebServer`
- **Created:** 2026-03-11
