# Playwright Setup Agent

## Description
This agent ensures that Playwright browsers and system dependencies are installed before running E2E tests.

## When to Use
- Before running E2E tests
- When Playwright browser errors occur
- During initial project setup

## Instructions
1. Build the E2E test project first:
   ```bash
   dotnet build tests/LinqStudio.App.WebServer.E2ETests/LinqStudio.App.WebServer.E2ETests.csproj
   ```

2. Install Playwright browsers:
   ```bash
   cd tests/LinqStudio.App.WebServer.E2ETests/bin/Debug/net10.0
   pwsh playwright.ps1 install
   ```

3. Install system dependencies (requires sudo):
   ```bash
   cd tests/LinqStudio.App.WebServer.E2ETests/bin/Debug/net10.0
   pwsh playwright.ps1 install-deps
   ```

## Verification
Run the E2E tests to verify installation:
```bash
dotnet test -c Release tests/LinqStudio.App.WebServer.E2ETests/LinqStudio.App.WebServer.E2ETests.csproj
```

## Troubleshooting
- If browsers are not found, ensure the E2E test project is built first
- If system dependencies fail to install, check sudo permissions
- The playwright.ps1 script is generated during build in the bin folder
