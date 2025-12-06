using System.Diagnostics;
using System.Net.Http;
using Xunit;

namespace LinqStudio.App.WebServer.E2ETests.Fixtures;

public class AppServerFixture : IAsyncLifetime
{
    private Process? _process;
    public string BaseUrl { get; private set; } = "http://127.0.0.1:5020";

    public async Task InitializeAsync()
    {
        // start the web server using dotnet run on a fixed port
        var solutionRoot = FindSolutionRoot();
        var projectPath = Path.Combine(solutionRoot, "src", "LinqStudio.App.WebServer");

        var startInfo = new ProcessStartInfo()
        {
            FileName = "dotnet",
            Arguments = $"run --project \"{projectPath}\" --urls http://127.0.0.1:5020",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        _process = Process.Start(startInfo);

        // wait for server to be ready by polling
        using var client = new HttpClient();

        var success = false;
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed.TotalSeconds < 60)
        {
            try
            {
                var res = await client.GetAsync(BaseUrl + "/editor");
                if (res.IsSuccessStatusCode)
                {
                    success = true;
                    break;
                }
            }
            catch
            {
                // ignore
            }

            await Task.Delay(500);
        }

        if (!success)
        {
            // capture std out/error for diagnostics
            var outText = _process != null ? await _process.StandardOutput.ReadToEndAsync() : string.Empty;
            var errText = _process != null ? await _process.StandardError.ReadToEndAsync() : string.Empty;
            throw new InvalidOperationException($"Failed to start app server within timeout. Stdout:\n{outText}\nStderr:\n{errText}");
        }
    }

    private static string FindSolutionRoot()
    {
        var dir = new DirectoryInfo(Environment.CurrentDirectory);
        for (int i = 0; i < 8 && dir != null; i++)
        {
            if (File.Exists(Path.Combine(dir.FullName, "LinqStudio.slnx")) || File.Exists(Path.Combine(dir.FullName, "LinqStudio.sln")))
                return dir.FullName;
            dir = dir.Parent;
        }

        throw new InvalidOperationException("Could not locate repository root containing LinqStudio.slnx or LinqStudio.sln");
    }

    public async Task DisposeAsync()
    {
        try
        {
            if (_process != null && !_process.HasExited)
            {
                _process.Kill(entireProcessTree: true);
                _process.WaitForExit(5000);
            }
        }
        catch { }
        await Task.CompletedTask;
    }
}
