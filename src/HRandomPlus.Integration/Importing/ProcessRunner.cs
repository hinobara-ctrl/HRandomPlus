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
    private static readonly TimeSpan TerminationGracePeriod = TimeSpan.FromSeconds(2);

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
                bool exited = await WaitForExitWithinAsync(process.WaitForExitAsync(CancellationToken.None),
                    TerminationGracePeriod).ConfigureAwait(false);
                string capturedOutput = await ReadCapturedOutputAsync(stdout, exited).ConfigureAwait(false);
                string capturedError = await ReadCapturedOutputAsync(stderr, exited).ConfigureAwait(false);
                return new ProcessRunResult(true, true, null, capturedOutput,
                    capturedError, exited ? "The process timed out." : "The process timed out and could not be terminated promptly.");
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                try { process.Kill(entireProcessTree: true); } catch { }
                bool exited = false;
                try
                {
                    exited = await WaitForExitWithinAsync(process.WaitForExitAsync(CancellationToken.None),
                        TerminationGracePeriod).ConfigureAwait(false);
                }
                catch { }
                _ = await ReadCapturedOutputAsync(stdout, exited).ConfigureAwait(false);
                _ = await ReadCapturedOutputAsync(stderr, exited).ConfigureAwait(false);
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

    internal static async Task<bool> WaitForExitWithinAsync(Task exitTask, TimeSpan maximumWait)
    {
        Task completed = await Task.WhenAny(exitTask, Task.Delay(maximumWait)).ConfigureAwait(false);
        if (!ReferenceEquals(completed, exitTask)) return false;
        await exitTask.ConfigureAwait(false);
        return true;
    }

    private static async Task<string> ReadCapturedOutputAsync(Task<string> readTask, bool processExited)
    {
        if (processExited)
        {
            try { return await readTask.ConfigureAwait(false); }
            catch { return string.Empty; }
        }

        _ = readTask.ContinueWith(task => _ = task.Exception,
            CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
        return readTask.IsCompletedSuccessfully ? readTask.Result : string.Empty;
    }
}
