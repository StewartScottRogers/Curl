using Curl.Protocol.Abstractions;

namespace Curl.Original.Protocol.Tftp;

/// <summary>
/// Stamps a finished TFTP transfer's <see cref="TransferResult" /> with the
/// <see cref="TransferTimings" /> behind <c>%{time_*}</c> and <c>%{speed_*}</c> (BL-5102).
/// The datagram channel is already open when a transfer starts, so name lookup, connect and
/// pretransfer share the timestamp the transfer began exchanging packets at, and the first
/// byte counts as received when a transfer succeeds.
/// </summary>
/// <param name="timeProvider">The clock every timestamp is read from.</param>
/// <param name="started">The timestamp the transfer started at.</param>
internal sealed class TftpTransferTimings(TimeProvider timeProvider, long started)
{
    private readonly long channelReady = timeProvider.GetTimestamp();

    /// <summary>
    /// Returns <paramref name="result" /> with its report carrying the transfer's timings.
    /// </summary>
    /// <param name="result">The outcome of the transfer.</param>
    /// <param name="isUpload">Whether the transfer was an upload, which has no first byte received (BL-5327).</param>
    /// <returns>The same outcome with <see cref="TransferReport.Timings" /> set.</returns>
    internal TransferResult Stamp(TransferResult result, bool isUpload)
    {
        var completed = timeProvider.GetTimestamp();
        // A download never connects (curl's UDP "connect" is not timed, so %{time_connect} is 0),
        // its first byte is taken as received when the channel was ready, and the request counts as
        // sent once the transfer is over (BL-5387). An upload keeps its BL-5327 timings.
        TransferTimings timings = isUpload
            ? new(started, new ConnectTimings(started, channelReady, channelReady, null), channelReady, null, null, completed)
            : new(
                started,
                new ConnectTimings(started, channelReady, null, null),
                channelReady,
                completed,
                result.IsSuccess ? channelReady : null,
                completed);
        return result with { Report = (result.Report ?? new TransferReport()) with { Timings = timings } };
    }
}
