using Nuke.Common;
using Nuke.Common.CI.GitHubActions;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.DotNet;
using System.Collections.Generic;
using System.Linq;
using static Nuke.Common.Tools.DotNet.DotNetTasks;

[GitHubActions(
	"build-and-test",
	GitHubActionsImage.UbuntuLatest,
	OnPullRequestBranches = new[] { "main" },
	InvokedTargets = new[] { nameof(Test) },
	EnableGitHubToken = true,
	FetchDepth = 0)]
[GitHubActions(
	"manual-unit-tests",
	GitHubActionsImage.UbuntuLatest,
	On = new[] { GitHubActionsTrigger.WorkflowDispatch },
	InvokedTargets = new[] { nameof(UnitTests) },
	EnableGitHubToken = true,
	FetchDepth = 0)]
[GitHubActions(
	"manual-e2e-tests",
	GitHubActionsImage.UbuntuLatest,
	On = new[] { GitHubActionsTrigger.WorkflowDispatch },
	InvokedTargets = new[] { nameof(E2ETests) },
	EnableGitHubToken = true,
	FetchDepth = 0)]
class Build : NukeBuild
{
	public static int Main() => Execute<Build>(x => x.Test);

	[Parameter("Configuration to build - Default is 'Debug' (local) or 'Release' (server)")]
	readonly Configuration Configuration = IsLocalBuild ? Configuration.Debug : Configuration.Release;

	[Solution]
	readonly Solution Solution;

	// Get all projects except the build project itself
	IEnumerable<Project> BuildableProjects => Solution.AllProjects.Where(p => p != Solution.GetProject("_build"));

	// Test project discovery
	IEnumerable<Project> UnitTestProjects => Solution.AllProjects.Where(p => p.Name.EndsWith(".Tests"));
	IEnumerable<Project> E2ETestProjects => Solution.AllProjects.Where(p => p.Name.EndsWith(".E2ETests"));

	Target Clean => _ => _
		.Before(Restore)
		.Executes(() =>
		{
			foreach (var project in BuildableProjects)
			{
				DotNetClean(s => s
					.SetProject(project)
					.SetConfiguration(Configuration));
			}
		});

	Target Restore => _ => _
		.Executes(() =>
		{
			DotNetRestore(s => s
				.SetProjectFile(Solution));
		});

	Target Compile => _ => _
		.DependsOn(Restore)
		.Executes(() =>
		{
			DotNetBuild(s => s
			   .SetProjectFile(Solution)
			   .SetConfiguration(Configuration)
			   .EnableNoRestore());
		});

	// Install Playwright browsers for E2E tests using the built-in script
	Target PlaywrightInstall => _ => _
		.DependsOn(Compile)
		.Executes(() =>
		{
			// Find the first E2E test project
			var e2eProject = E2ETestProjects.FirstOrDefault();
			if (e2eProject == null)
			{
				return; // No E2E tests, skip
			}

			// Locate the playwright.ps1 script in the build output
			var targetFramework = "net10.0";
			var playwrightScript = e2eProject.Directory / "bin" / Configuration / targetFramework / "playwright.ps1";

			// Run playwright install with OS dependencies (required on Linux)
			ProcessTasks.StartProcess("pwsh", $"{playwrightScript} install --with-deps").AssertZeroExitCode();
		});

	// Run unit tests only (exclude E2E)
	Target UnitTests => _ => _
		.DependsOn(Compile)
		.Executes(() =>
		{
			foreach (var project in UnitTestProjects)
			{
				DotNetTest(s => s
					.SetProjectFile(project)
					.SetConfiguration(Configuration)
					.EnableNoBuild()
					.EnableNoRestore());
			}
		});

	// Run E2E tests only (ensure Playwright is installed first)
	Target E2ETests => _ => _
		.DependsOn(PlaywrightInstall)
		.After(UnitTests)
		.Executes(() =>
		{
			foreach (var project in E2ETestProjects)
			{
				DotNetTest(s => s
					.SetProjectFile(project)
					.SetConfiguration(Configuration)
					.EnableNoBuild()
					.EnableNoRestore());
			}
		});

	// Aggregate target (default in CI)
	Target Test => _ => _
		.DependsOn(UnitTests, E2ETests)
		.Executes(() => { });
}
