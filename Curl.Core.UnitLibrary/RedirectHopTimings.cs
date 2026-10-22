using Curl.Protocol.Abstractions;

namespace Curl.Core;

/// <summary>
/// The timings of the hops of one redirect chain, merged as curl 8.22.0's <c>Curl_pgrsTime</c> keeps
/// them: each phase (name lookup, connect, TLS handshake, pretransfer, posttransfer, first byte) adds
/// the time from its own hop's start to the event, so a phase that took 2 s on the first hop and 1 s on
/// the next is reported as 3 s, without the time between (upstream <c>tests/unit/unit1399.c</c>).
/// </summary>
/// <param name="timeProvider">The provider whose <see cref="TimeProvider.GetTimestamp" /> took the hops' timings.</param>
public sealed class RedirectHopTimings(TimeProvider timeProvider)
{
    private long? firstStarted;
    private bool connectRecorded;
    private long? nameResolved;
    private long? connected;
    private long? tlsHandshakeCompleted;
    private long? requestReady;
    private long? requestSent;
    private long? firstByteReceived;

    /// <summary>Adds one hop's timings; a hop that recorded none adds nothing.</summary>
    /// <param name="hop">The hop's timings, or <see langword="null" />.</param>
    public void Add(TransferTimings? hop)
    {
        if (hop is null)
        {
            return;
        }

        firstStarted ??= hop.Started;
        connectRecorded |= hop.Connect is not null;
        nameResolved = Sum(nameResolved, hop, hop.Connect?.NameResolved);
        connected = Sum(connected, hop, hop.Connect?.Connected);
        tlsHandshakeCompleted = Sum(tlsHandshakeCompleted, hop, hop.Connect?.TlsHandshakeCompleted);
        requestReady = Sum(requestReady, hop, hop.RequestReady);
        requestSent = Sum(requestSent, hop, hop.RequestSent);
        firstByteReceived = Sum(firstByteReceived, hop, hop.FirstByteReceived);
    }

    /// <summary>
    /// The chain's timings: the last hop's, measured from the first added hop's start, with each phase
    /// at that start plus its summed time and <see cref="TransferTimings.RedirectDuration" /> the time
    /// until the last hop began. A phase no hop reached stays <see langword="null" />, and so does the connect timings when no hop recorded any.
    /// </summary>
    /// <param name="last">The last hop's timings, after it was added; <see langword="null" /> gives <see langword="null" />.</param>
    /// <returns>The merged timings.</returns>
    public TransferTimings? Merge(TransferTimings? last)
    {
        if (last is null)
        {
            return null;
        }

        long started = firstStarted ?? last.Started;
        ConnectTimings connect = new(last.Connect?.Started ?? started, StartedPlus(started, nameResolved), StartedPlus(started, connected), StartedPlus(started, tlsHandshakeCompleted));
        return last with
        {
            Started = started,
            Connect = connectRecorded ? connect : null,
            RequestReady = StartedPlus(started, requestReady),
            RequestSent = StartedPlus(started, requestSent),
            FirstByteReceived = StartedPlus(started, firstByteReceived),
            RedirectDuration = timeProvider.GetElapsedTime(started, last.Started),
        };
    }

    private static long? StartedPlus(long started, long? total) => started + total;

    private static long? Sum(long? total, TransferTimings hop, long? reached) =>
        reached is long at ? (total ?? 0) + (at - hop.Started) : total;
}
