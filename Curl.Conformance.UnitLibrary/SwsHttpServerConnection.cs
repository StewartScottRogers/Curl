using System.Net;
using Curl.Protocol.Abstractions;

namespace Curl.Conformance;

/// <summary>
/// One client connection to the sws emulation. Every complete request the client writes is
/// answered at once, and the reply waits to be read; the bytes written are recorded while
/// the server still reads them.
/// </summary>
/// <remarks>
/// <para>
/// A reply is sent in writes of up to 20 bytes, as <c>sws_send_doc</c> sends it, each
/// readable from the moment sws would write it: <c>writedelay: N</c> puts N milliseconds after
/// every write, and <c>&lt;postcmd&gt;</c> <c>wait N</c> keeps sws busy N seconds after the
/// reply, so a close, or the next reply, comes that much later. Times are read from the
/// injected <see cref="TimeProvider"/>, and a read waits on it for bytes not yet sent.
/// </para>
/// <para>
/// Once the server has closed its side, reads drain the replies already sent and then return
/// 0, and writes are accepted and dropped unrecorded, as a socket write into a closed
/// connection can succeed without the server ever reading it. A read with no reply waiting
/// also returns 0: in memory the client is the only writer, so nothing else could arrive;
/// except after <c>idle</c>, where it waits until cancelled, after <c>stream</c>, where it
/// reads the streamed text without end, and on an upgraded connection, where it waits for the
/// server to close it one second after the client last wrote.
/// </para>
/// </remarks>
internal sealed class SwsHttpServerConnection : IConnection
{
    private const int BytesPerWrite = 20;

    private static readonly byte[] StreamedText = "a string to stream 01234567890\n"u8.ToArray();

    // sws reads an upgraded connection until a select() of one second finds nothing to read.
    private static readonly TimeSpan UpgradedTrafficQuietTime = TimeSpan.FromSeconds(1);

    // No case's timing depends on a gap this short, and no healthy timer fires this late; so a wait
    // no longer than this is too short for a stall inside it to reorder the timers a case races.
    private static readonly TimeSpan LateWakeLimit = TimeSpan.FromSeconds(1);

    private static readonly TimeSpan StalledTimersSettleTime = TimeSpan.FromMilliseconds(250);

    private readonly SwsHttpReplySelector replySelector;

    private readonly SwsServerCommands serverCommands;

    private readonly TimeSpan waitAfterReply;

    private readonly SwsServerRecording recording;

    private readonly TimeProvider timeProvider;

    private readonly SwsServerAbandonment abandonment;

    private readonly long openedAt;

    private readonly List<byte> unservedRequestBytes = [];

    private readonly List<SwsServerSend> pendingSends = [];

    private TimeSpan serverBusyUntil;

    private bool readsRequests = true;

    private bool idling;

    private bool streaming;

    private int streamedOffset;

    private bool upgradedTrafficOpen;

    private TimeSpan lastUpgradedTrafficAt;

    private bool disconnected;

    public SwsHttpServerConnection(SwsHttpReplySelector replySelector, SwsServerCommands serverCommands, TimeSpan waitAfterReply, SwsServerRecording recording, TimeProvider timeProvider, SwsServerAbandonment abandonment)
    {
        this.replySelector = replySelector;
        this.serverCommands = serverCommands;
        this.waitAfterReply = waitAfterReply;
        this.recording = recording;
        this.timeProvider = timeProvider;
        this.abandonment = abandonment;
        openedAt = timeProvider.GetTimestamp();
    }

    public bool IsSecure => false;

    public EndPoint? RemoteEndPoint => null;

    public EndPoint? LocalEndPoint { get; init; }

    private TimeSpan Now => timeProvider.GetElapsedTime(openedAt);

    private TimeSpan UpgradedTrafficClosesAt =>
        (serverBusyUntil > lastUpgradedTrafficAt ? serverBusyUntil : lastUpgradedTrafficAt) + UpgradedTrafficQuietTime;

    public async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        abandonment.ThrowIfAbandoned();
        return pendingSends.Count > 0
            ? await ReadSentAsync(buffer, cancellationToken)
            : await ReadWithNothingSentAsync(buffer, cancellationToken);
    }

    public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
    {
        abandonment.ThrowIfAbandoned();
        if (upgradedTrafficOpen)
        {
            RecordUpgradedTraffic(buffer.Span);
        }
        else if (readsRequests)
        {
            recording.Record(buffer.Span);
            unservedRequestBytes.AddRange(buffer.Span);
            ServeCompleteRequests();
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask FlushAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

    public ValueTask DisposeAsync()
    {
        RecordDisconnectOnce();
        return ValueTask.CompletedTask;
    }

    // Waits for the first send, then reads it and every later one already sent, up to a close.
    private async ValueTask<int> ReadSentAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        await WaitUntilAsync(pendingSends[0].SentAt, cancellationToken);
        int count = 0;
        while (count < buffer.Length && pendingSends.Count > 0 && !pendingSends[0].IsClose && pendingSends[0].SentAt <= Now)
        {
            count += ReadFirstSend(buffer.Span[count..]);
        }

        return count;
    }

    private int ReadFirstSend(Span<byte> destination)
    {
        SwsServerSend first = pendingSends[0];
        int count = Math.Min(destination.Length, first.Bytes.Length);
        first.Bytes.AsSpan(0, count).CopyTo(destination);
        if (count == first.Bytes.Length)
        {
            pendingSends.RemoveAt(0);
        }
        else
        {
            pendingSends[0] = new SwsServerSend(first.Bytes[count..], first.SentAt, false);
        }

        return count;
    }

    private async ValueTask<int> ReadWithNothingSentAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        if (streaming)
        {
            return ReadStreamedText(buffer.Span);
        }

        if (upgradedTrafficOpen)
        {
            return await WaitForUpgradedTrafficToCloseAsync(cancellationToken);
        }

        return idling ? await WaitUntilCancelledAsync(cancellationToken) : 0;
    }

    private int ReadStreamedText(Span<byte> destination)
    {
        for (int index = 0; index < destination.Length; index++)
        {
            destination[index] = StreamedText[(streamedOffset + index) % StreamedText.Length];
        }

        streamedOffset = (streamedOffset + destination.Length) % StreamedText.Length;
        return destination.Length;
    }

    // A write while waiting moves the close later, so the wait starts again from it.
    private async ValueTask<int> WaitForUpgradedTrafficToCloseAsync(CancellationToken cancellationToken)
    {
        while (Now < UpgradedTrafficClosesAt)
        {
            await WaitUntilAsync(UpgradedTrafficClosesAt, cancellationToken);
        }

        CloseUpgradedTraffic();
        return 0;
    }

    // A task nothing completes, so the wait only ends, cancelled, with the token.
    private static Task<int> WaitUntilCancelledAsync(CancellationToken cancellationToken) =>
        new TaskCompletionSource<int>().Task.WaitAsync(cancellationToken);

    // A system timer can fire a little before the time provider's timestamp reaches the moment,
    // and a read that woke early would find nothing sent and return 0, ending the reply early;
    // so the wait goes on until the moment has passed.
    // A process that stalls (a busy CI runner starving the thread pool) runs every timer that fell
    // due in the stall - curl's -m among them - when it recovers, in no set order; so after a wait
    // long enough for a stall to hide in, or a wake more than LateWakeLimit past the moment, the
    // server waits StalledTimersSettleTime more for them to go first, and a stall cannot put the
    // close of test29's ten-second wait ahead of its two-second -m. A late wake alone does not
    // show the stall (BL-1321): one from 1.9 s to 10.4 s wakes the close only 0.4 s late, beside
    // a -m 8.4 s overdue (BL-1441).
    private async ValueTask WaitUntilAsync(TimeSpan moment, CancellationToken cancellationToken)
    {
        TimeSpan remaining = moment - Now;
        if (remaining <= TimeSpan.Zero)
        {
            return;
        }

        bool longWait = remaining > LateWakeLimit;
        do
        {
            await Task.Delay(remaining, timeProvider, cancellationToken);
            remaining = moment - Now;
        }
        while (remaining > TimeSpan.Zero);

        if (longWait || -remaining > LateWakeLimit)
        {
            await Task.Delay(StalledTimersSettleTime, timeProvider, cancellationToken);
        }
    }

    private void RecordUpgradedTraffic(ReadOnlySpan<byte> traffic)
    {
        if (Now >= UpgradedTrafficClosesAt)
        {
            CloseUpgradedTraffic();
            return;
        }

        recording.Record(traffic);
        lastUpgradedTrafficAt = Now;
    }

    private void CloseUpgradedTraffic()
    {
        upgradedTrafficOpen = false;
        pendingSends.Add(new SwsServerSend([], Now, true));
        RecordDisconnectOnce();
    }

    private void ServeCompleteRequests()
    {
        int requestLength;
        bool upgradesConnection = false;
        while (readsRequests && (requestLength = SwsHttpRequestFraming.FindRequestLength(unservedRequestBytes.ToArray(), serverCommands, out upgradesConnection)) >= 0)
        {
            SwsHttpReply reply = replySelector.Select(unservedRequestBytes.GetRange(0, requestLength).ToArray());
            unservedRequestBytes.RemoveRange(0, requestLength);
            Serve(reply, upgradesConnection);
        }
    }

    // sws's 404 document is sent before any <servercmd> is read, so none applies to it.
    private void Serve(SwsHttpReply reply, bool upgradesConnection)
    {
        if (!reply.IsFromTestCase)
        {
            Send(reply.Bytes, TimeSpan.Zero, TimeSpan.Zero, true);
            return;
        }

        if (serverCommands.MonitorsConnections)
        {
            recording.ArmDisconnectMonitor();
        }

        idling = serverCommands.ReplyMode == SwsReplyMode.Idle;
        streaming = serverCommands.ReplyMode == SwsReplyMode.Stream;
        if (serverCommands.ReplyMode == SwsReplyMode.Normal)
        {
            Send(reply.Bytes, TimeSpan.FromMilliseconds(Math.Max(0, serverCommands.MillisecondsAfterEachWrite)), waitAfterReply, reply.ClosesConnection);
        }

        // Streaming never reads again; an upgraded connection reads traffic, not requests.
        readsRequests &= !(streaming || upgradesConnection);
        upgradedTrafficOpen = upgradesConnection && !reply.ClosesConnection && serverCommands.ReplyMode == SwsReplyMode.Normal;
        lastUpgradedTrafficAt = Now;
    }

    // The bytes go in writes of up to 20, each followed by the write delay; a close waits for the
    // last write's delay and for the <postcmd> wait.
    private void Send(byte[] bytes, TimeSpan delayAfterEachWrite, TimeSpan waitAfterLastWrite, bool closes)
    {
        TimeSpan writtenAt = serverBusyUntil > Now ? serverBusyUntil : Now;
        int offset = 0;
        do
        {
            int count = Math.Min(BytesPerWrite, bytes.Length - offset);
            if (count > 0)
            {
                pendingSends.Add(new SwsServerSend(bytes[offset..(offset + count)], writtenAt, false));
            }

            offset += count;
            writtenAt += delayAfterEachWrite;
        }
        while (offset < bytes.Length);

        serverBusyUntil = writtenAt + waitAfterLastWrite;
        if (closes)
        {
            readsRequests = false;
            pendingSends.Add(new SwsServerSend([], serverBusyUntil, true));
            RecordDisconnectOnce();
        }
    }

    private void RecordDisconnectOnce()
    {
        if (!disconnected)
        {
            disconnected = true;
            recording.RecordDisconnect();
        }
    }
}
