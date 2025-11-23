using Nuke.Common;
using Nuke.Common.CI.GitHubActions;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tools.DotNet;
using System.Collections.Generic;
using System.Linq;
using static Nuke.Common.Tools.DotNet.DotNetTasks;

[GitHubActions(
    "build-and-test",
    GitHubActionsImage.UbuntuLatest,
    On = new[] { GitHubActionsTrigger.Push, GitHubActionsTrigger.PullRequest })]
class Build : NukeBuild
{
    public static int Main() => Execute<Build>(x => x.Test);

    [Parameter("Configuration to build - Default is 'Debug' (local) or 'Release' (server)")]
    readonly Configuration Configuration = IsLocalBuild ? Configuration.Debug : Configuration.Release;

    [Solution]
    readonly Solution Solution;

    // Get all projects except the build project itself
    IEnumerable<Project> BuildableProjects => Solution.AllProjects.Where(p => p != Solution.GetProject("_build"));

    Target Clean => _ => _
        .Before(Restore)
        .Executes(() =>
        {
            // Clean bin/obj folders for all projects except the build project
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

    Target Test => _ => _
        .DependsOn(Compile)
        .Executes(() =>
        {
            DotNetTest(s => s
                .SetProjectFile(Solution)
                .EnableNoBuild());
        });
}
