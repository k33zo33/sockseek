using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace Sockseek.Desktop;

public sealed class SystemDesktopProcessLauncher : IDesktopProcessLauncher
{
    public Task<IDesktopProcessSession> LaunchAsync(DesktopDaemonLaunchRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var startInfo = new ProcessStartInfo
        {
            FileName = request.FileName,
            Arguments = request.Arguments,
            WorkingDirectory = request.WorkingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        foreach (var pair in request.EnvironmentVariables)
        {
            if (pair.Value is null)
                startInfo.Environment.Remove(pair.Key);
            else
                startInfo.Environment[pair.Key] = pair.Value;
        }

        var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        if (!process.Start())
            throw new InvalidOperationException($"Failed to launch desktop daemon process '{request.FileName}'.");

        return Task.FromResult<IDesktopProcessSession>(new SystemDesktopProcessSession(process));
    }

    private sealed class SystemDesktopProcessSession : IDesktopProcessSession
    {
        private readonly Process process;
        private readonly Channel<string> lines = Channel.CreateUnbounded<string>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false,
        });
        private readonly Task stdoutPump;
        private readonly Task stderrPump;
        private int disposeStarted;

        public SystemDesktopProcessSession(Process process)
        {
            this.process = process;
            stdoutPump = PumpAsync(process.StandardOutput, lines.Writer);
            stderrPump = PumpAsync(process.StandardError, lines.Writer);
            _ = CompleteWhenFinishedAsync();
        }

        public async IAsyncEnumerable<string> ReadOutputLinesAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            while (await lines.Reader.WaitToReadAsync(cancellationToken))
            {
                while (lines.Reader.TryRead(out var line))
                    yield return line;
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref disposeStarted, 1) != 0)
                return;

            try
            {
                if (!process.HasExited)
                {
                    try
                    {
                        process.Kill(entireProcessTree: true);
                    }
                    catch (InvalidOperationException)
                    {
                    }
                }

                try
                {
                    await process.WaitForExitAsync();
                }
                catch (InvalidOperationException)
                {
                }

                await Task.WhenAll(stdoutPump, stderrPump);
            }
            finally
            {
                process.Dispose();
            }
        }

        private async Task CompleteWhenFinishedAsync()
        {
            try
            {
                await Task.WhenAll(stdoutPump, stderrPump);
            }
            finally
            {
                lines.Writer.TryComplete();
            }
        }

        private static async Task PumpAsync(StreamReader reader, ChannelWriter<string> writer)
        {
            while (true)
            {
                var line = await reader.ReadLineAsync();
                if (line is null)
                    break;

                await writer.WriteAsync(line);
            }
        }
    }
}
