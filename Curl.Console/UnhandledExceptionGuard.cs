using System.Text;

using Curl.Protocol.Abstractions;

namespace Curl.Console;

/// <summary>
/// Runs the whole command line and turns any exception that escapes it, other than a
/// cancellation, into curl's <c>curl: (2) &lt;message&gt;</c> line on standard error and exit 2,
/// <c>CURLE_FAILED_INIT</c>, so no failure kills the process without a message and a normal
/// exit code (ADR-0766, BL-3172).
/// </summary>
internal static class UnhandledExceptionGuard
{
    /// <summary>
    /// Runs <paramref name="run" /> and returns its exit code; an exception escaping it, other than an
    /// <see cref="OperationCanceledException" />, which keeps its own handling, flushes
    /// <paramref name="standardOutput" />, writes one <c>curl: (2) &lt;message&gt;</c> line to
    /// <paramref name="standardError" /> and gives exit 2.
    /// </summary>
    /// <param name="run">The run of the command line, giving its exit code.</param>
    /// <param name="standardOutput">Standard output, flushed before the failure line is written.</param>
    /// <param name="standardError">Where the failure line is written.</param>
    /// <returns>The run's exit code, or 2 when an exception escaped it.</returns>
    internal static async Task<int> RunAsync(Func<Task<int>> run, Stream standardOutput, Stream standardError)
    {
        try
        {
            return await run().ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            await FlushQuietlyAsync(standardOutput).ConfigureAwait(false);
            var line = Encoding.UTF8.GetBytes($"curl: ({(int)CurlExitCode.FailedInit}) {exception.Message}\n");
            await standardError.WriteAsync(line).ConfigureAwait(false);
            await standardError.FlushAsync().ConfigureAwait(false);
            return (int)CurlExitCode.FailedInit;
        }
    }

    // Standard output that cannot be flushed (a closed pipe) must not hide the failure line.
    private static async Task FlushQuietlyAsync(Stream standardOutput)
    {
        try
        {
            await standardOutput.FlushAsync().ConfigureAwait(false);
        }
        catch (IOException)
        {
        }
    }
}
