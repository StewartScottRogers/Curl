using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Curl.Extended.Protocol.Plugin;

/// <summary>Starts a helper program with <see cref="Process" />; its standard error stays Curl's.</summary>
public sealed class SystemHelperProcessStarter : IHelperProcessStarter
{
    /// <inheritdoc />
    // Excluded per ADR-0083: every line needs a real child process, which only an Integration test may start.
    [ExcludeFromCodeCoverage(Justification = "ADR-0083: a thin Process adapter; every line needs a child process.")]
    public IHelperProcess Start(string program, string url)
    {
        ProcessStartInfo info = new(program)
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
        };
        info.ArgumentList.Add(url);
        try
        {
            return new SystemHelperProcess(Process.Start(info)!);
        }
        catch (Win32Exception failure)
        {
            throw new IOException(failure.Message, failure);
        }
    }

    [ExcludeFromCodeCoverage(Justification = "ADR-0083: a thin Process adapter; every line needs a child process.")]
    private sealed class SystemHelperProcess(Process process) : IHelperProcess
    {
        public Stream Input => process.StandardInput.BaseStream;

        public Stream Output => process.StandardOutput.BaseStream;

        public async Task<int> WaitForExitAsync(CancellationToken cancellationToken)
        {
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            return process.ExitCode;
        }

        public ValueTask DisposeAsync()
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }

            process.Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
