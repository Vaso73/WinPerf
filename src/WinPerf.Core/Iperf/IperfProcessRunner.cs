using System.Collections.Concurrent;
using System.Diagnostics;

namespace WinPerf.Core.Iperf;

public sealed class IperfProcessRunner
{
    public static ProcessStartInfo CreateStartInfo(IperfCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentException.ThrowIfNullOrWhiteSpace(command.ExecutablePath);

        var startInfo = new ProcessStartInfo
        {
            FileName = command.ExecutablePath,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        foreach (var argument in command.Arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        return startInfo;
    }

    public async Task<IperfRunResult> RunAsync(
        IperfCommand command,
        Func<IperfProcessOutputLine, CancellationToken, ValueTask>? onOutput = null,
        CancellationToken cancellationToken = default)
    {
        var startInfo = CreateStartInfo(command);
        var output = new ConcurrentQueue<IperfProcessOutputLine>();
        var startedAt = DateTimeOffset.UtcNow;

        using var process = new Process
        {
            StartInfo = startInfo,
            EnableRaisingEvents = true
        };

        if (!process.Start())
        {
            throw new InvalidOperationException("Failed to start iperf3 process.");
        }

        var stdoutTask = ReadLinesAsync(
            process.StandardOutput,
            IperfOutputStream.StandardOutput,
            output,
            onOutput,
            cancellationToken);

        var stderrTask = ReadLinesAsync(
            process.StandardError,
            IperfOutputStream.StandardError,
            output,
            onOutput,
            cancellationToken);

        try
        {
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            await Task.WhenAll(stdoutTask, stderrTask).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            TryKill(process);
            throw;
        }

        return new IperfRunResult(
            process.ExitCode,
            startedAt,
            DateTimeOffset.UtcNow,
            output.ToArray());
    }

    private static async Task ReadLinesAsync(
        TextReader reader,
        IperfOutputStream stream,
        ConcurrentQueue<IperfProcessOutputLine> output,
        Func<IperfProcessOutputLine, CancellationToken, ValueTask>? onOutput,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            var text = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);

            if (text is null)
            {
                break;
            }

            var line = new IperfProcessOutputLine(stream, text, DateTimeOffset.UtcNow);
            output.Enqueue(line);

            if (onOutput is not null)
            {
                await onOutput(line, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
        }
        catch (System.ComponentModel.Win32Exception)
        {
        }
    }
}
