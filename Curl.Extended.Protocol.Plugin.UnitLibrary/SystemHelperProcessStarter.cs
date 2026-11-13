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
        RefuseUnsafeBatchArgument(program, url);
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

    /// <summary>
    /// Refuses, for a <c>.cmd</c> or <c>.bat</c> helper, a URL holding a cmd.exe metacharacter (<c>&amp; | &lt; &gt; ^ " %</c>)
    /// or a control character, because cmd.exe would run the rest of the URL as another command. Other helpers are unchanged.
    /// </summary>
    /// <exception cref="IOException">The helper is a batch file and the URL holds such a character.</exception>
    internal static void RefuseUnsafeBatchArgument(string program, string url)
    {
        if (IsBatchFile(program) && (url.AsSpan().IndexOfAny("&|<>^\"%") >= 0 || ContainsControlCharacter(url)))
        {
            throw new IOException("the URL holds a character cmd.exe would run as a command (& | < > ^ \" % or a control character), so it is not passed to a .cmd or .bat helper");
        }
    }

    private static bool IsBatchFile(string program)
    {
        int end = program.Length;
        while (end > 0 && IsTrailingNoise(program[end - 1]))
        {
            end--;
        }

        ReadOnlySpan<char> name = program.AsSpan(0, end);
        return name.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith(".bat", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsTrailingNoise(char character) => character == '.' || char.IsWhiteSpace(character) || char.IsControl(character);

    private static bool ContainsControlCharacter(string url)
    {
        foreach (char character in url)
        {
            if (char.IsControl(character))
            {
                return true;
            }
        }

        return false;
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
