#if NET472
using System.Diagnostics;

namespace HomeAssistantX.Tests.Infrastructure;

internal sealed class CrossProcessHomeAssistantServer : IDisposable
{
    private readonly Process _process;
    private int _disposed;

    private CrossProcessHomeAssistantServer(Process process, Uri baseUri)
    {
        _process = process;
        BaseUri = baseUri;
    }

    public Uri BaseUri { get; }

    public static async Task<CrossProcessHomeAssistantServer> StartAsync()
    {
        var configuration = Directory.GetParent(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar))?.Name
            ?? "Release";
        var executable = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "HomeAssistantX.TestServer",
            "bin",
            configuration,
            "net10.0",
            "HomeAssistantX.TestServer.exe"));
        if (!File.Exists(executable))
        {
            throw new FileNotFoundException("Build HomeAssistantX.slnx before running the .NET Framework WebSocket contracts.", executable);
        }

        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = executable,
                WorkingDirectory = Path.GetDirectoryName(executable),
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WindowStyle = ProcessWindowStyle.Hidden
            }
        };
        process.Start();
        try
        {
            var ready = await WithTimeoutAsync(process.StandardOutput.ReadLineAsync()).ConfigureAwait(false);
            if (ready is null || !ready.StartsWith("READY ", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("The loopback Home Assistant process did not report readiness.");
            }

            return new CrossProcessHomeAssistantServer(process, new Uri(ready.Substring("READY ".Length), UriKind.Absolute));
        }
        catch
        {
            if (!process.HasExited)
            {
                process.Kill();
            }

            process.Dispose();
            throw;
        }
    }

    public async Task SendCommandAsync(string command, string expectedResponse)
    {
        await _process.StandardInput.WriteLineAsync(command).ConfigureAwait(false);
        await _process.StandardInput.FlushAsync().ConfigureAwait(false);
        var response = await WithTimeoutAsync(_process.StandardOutput.ReadLineAsync()).ConfigureAwait(false);
        Assert.Equal(expectedResponse, response);
    }

    private static async Task<T> WithTimeoutAsync<T>(Task<T> task)
    {
        var completed = await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(10))).ConfigureAwait(false);
        if (completed != task)
        {
            throw new TimeoutException("The loopback Home Assistant process did not respond in time.");
        }

        return await task.ConfigureAwait(false);
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        if (!_process.HasExited)
        {
            try
            {
                _process.StandardInput.WriteLine("EXIT");
                _process.StandardInput.Flush();
                if (!_process.WaitForExit(5000))
                {
                    _process.Kill();
                }
            }
            catch (InvalidOperationException)
            {
            }
        }

        _process.Dispose();
    }
}
#endif
