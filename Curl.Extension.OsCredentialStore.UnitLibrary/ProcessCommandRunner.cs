using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace Curl.Extension.OsCredentialStore;

/// <summary>
/// Runs a credential store's tool as a child process, reading both its outputs whole. A tool that cannot be
/// started reads as exit code 127, as a shell reports a command it cannot find.
/// </summary>
/// <remarks>Starting a process touches the OS, which a unit test run must not, so per ADR-0083 the type is left out of the coverage measure.</remarks>
[ExcludeFromCodeCoverage(Justification = "ADR-0083: a thin Process.Start adapter; what the tools print is parsed and tested in MacKeychainStore and LibsecretStore.")]
internal sealed class ProcessCommandRunner : ICommandRunner
{
    public async ValueTask<CommandOutput> RunAsync(string fileName, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        ProcessStartInfo start = new(fileName, arguments)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            UseShellExecute = false,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        try
        {
            using var process = Process.Start(start)!;
            process.StandardInput.Close();
            var output = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var error = process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);
            return new CommandOutput(process.ExitCode, await output, await error);
        }
        catch (Win32Exception exception)
        {
            return new CommandOutput(127, string.Empty, exception.Message);
        }
    }
}
