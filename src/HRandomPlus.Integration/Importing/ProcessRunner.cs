using System.ComponentModel;
using System.Diagnostics;

namespace HRandomPlus.Integration.Importing;

public sealed record ProcessRunRequest(string FileName, IReadOnlyList<string> Arguments, TimeSpan Timeout,
                                       IReadOnlyDictionary<string, string>? Environment = null);

public sealed record ProcessRunResult(bool Started, bool TimedOut, int? ExitCode, string StandardOutput,
                                      string StandardError, string? Error)
{
    public bool Success => Started && !TimedOut && ExitCode == 0;
}

public interface IProcessRunner
{
    Task<ProcessRunResult> RunAsync(ProcessRunRequest request, CancellationToken cancellationToken = default);
}

public sealed class SystemProcessRunner : IProcessRunner
{
    public async Task<ProcessRunResult> RunAsync(ProcessRunRequest request, CancellationToken cancellationToken = default)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = request.FileName,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        foreach (string argument in request.Arguments) startInfo.ArgumentList.Add(argument);
        if (request.Environment is not null)
            foreach ((string name, string value) in request.Environment)
                startInfo.Environment[name] = value;

        try
        {
            using var process = new Process { StartInfo = startInfo };
            if (!process.Start())
                return new ProcessRunResult(false, false, null, "", "", "The process could not be started.");
            Task<string> stdout = process.StandardOutput.ReadToEndAsync();
            Task<string> stderr = process.StandardError.ReadToEndAsync();
            using var timeout = new CancellationTokenSource(request.Timeout);
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
            try
            {
                await process.WaitForExitAsync(linked.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (timeout.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                try { process.Kill(entireProcessTree: true); } catch { }
                await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
                return new ProcessRunResult(true, true, null, await stdout.ConfigureAwait(false),
                    await stderr.ConfigureAwait(false), "The process timed out.");
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                try { process.Kill(entireProcessTree: true); } catch { }
                try { await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false); } catch { }
                try { await Task.WhenAll(stdout, stderr).ConfigureAwait(false); } catch { }
                cancellationToken.ThrowIfCancellationRequested();
                throw;
            }

            return new ProcessRunResult(true, false, process.ExitCode, await stdout.ConfigureAwait(false),
                await stderr.ConfigureAwait(false), process.ExitCode == 0 ? null : $"Process exited with code {process.ExitCode}.");
        }
        catch (Win32Exception ex)
        {
            return new ProcessRunResult(false, false, null, "", "", ex.Message);
        }
    }
}
