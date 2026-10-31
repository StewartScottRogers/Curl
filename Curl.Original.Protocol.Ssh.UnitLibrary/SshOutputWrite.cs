using System.Globalization;
using Curl.Protocol.Abstractions;

namespace Curl.Original.Protocol.Ssh;

/// <summary>
/// Writes a download's bytes to the transfer's output and turns a failed write into curl's
/// exit 23, <c>Failure writing output to destination, passed N returned M</c>, as curl's
/// transfer layer fails a write callback that takes less than it was given: an <c>-o</c>
/// file that cannot be created, or a standard output that has gone, ends the transfer as a
/// failed result, so the session still closes the handle and sends <c>DISCONNECT</c> and
/// the curl tool still prints <c>-w</c> (CGR-133).
/// </summary>
internal static class SshOutputWrite
{
    /// <summary>Writes <paramref name="data" /> to <paramref name="output" />.</summary>
    /// <param name="output">The transfer's output.</param>
    /// <param name="data">The bytes received.</param>
    /// <param name="received">The bytes written before these, the failed result's count.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>
    /// <see langword="null" /> when the bytes were written; otherwise the exit 23 result, its
    /// <c>returned M</c> being <see cref="OutputWriteFailedException.BytesAccepted" /> or 0.
    /// </returns>
    internal static async ValueTask<TransferResult?> TryWriteAsync(Stream output, ReadOnlyMemory<byte> data, long received, CancellationToken cancellationToken)
    {
        try
        {
            await output.WriteAsync(data, cancellationToken).ConfigureAwait(false);
            return null;
        }
        catch (IOException exception) when (!SshConnectionFailure.Is(exception))
        {
            int accepted = exception is OutputWriteFailedException failed ? failed.BytesAccepted : 0;
            return TransferResult.Failure(
                CurlExitCode.WriteError,
                "Failure writing output to destination, passed " + data.Length.ToString(CultureInfo.InvariantCulture)
                    + " returned " + accepted.ToString(CultureInfo.InvariantCulture),
                received);
        }
    }
}
