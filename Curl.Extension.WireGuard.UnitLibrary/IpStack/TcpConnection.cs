using System.Diagnostics.CodeAnalysis;
using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace Curl.Extension.WireGuard.IpStack;

/// <summary>
/// One TCP client connection (RFC 9293) over an <see cref="IIpPacketChannel" />: the three-way handshake with the
/// Maximum Segment Size option, in-order delivery with a 65535-byte receive window, sending within the peer's window,
/// retransmission on the RFC 6298 timer, and FIN and RST. Segments that arrive out of order are dropped and
/// acknowledged again, so the peer retransmits them (ADR-1121).
/// </summary>
public sealed class TcpConnection : IAsyncDisposable
{
    /// <summary>The receive buffer, and so the greatest window advertised.</summary>
    public const int ReceiveBufferCapacity = 65535;

    /// <summary>The Maximum Segment Size assumed when the SYN-ACK carries none (RFC 9293 section 3.7.1).</summary>
    public const ushort DefaultMaximumSegmentSize = 536;

    /// <summary>How many times the SYN is retransmitted before the connection times out.</summary>
    public const int MaximumSynRetransmissions = 6;

    /// <summary>How many times a segment is retransmitted, once connected, before the connection times out.</summary>
    public const int MaximumDataRetransmissions = 15;

    private readonly IIpPacketChannel channel;
    private readonly TcpConnectionParameters parameters;
    private readonly TimeProvider timeProvider;
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly CancellationTokenSource receiveLoopCancellation = new();
    private readonly List<TcpSegment> unacknowledged = [];
    private readonly List<byte> received = [];
    private readonly RetransmissionTimeoutEstimator estimator = new();
    private readonly ITimer retransmissionTimer;
    private TaskCompletionSource stateChanged = NewSignal();
    private Task receiveLoop = Task.CompletedTask;
    private Task retransmission = Task.CompletedTask;
    private uint sendUnacknowledged;
    private uint sendNext;
    private uint receiveNext;
    private int peerWindow;
    private ushort peerMaximumSegmentSize = DefaultMaximumSegmentSize;
    private ushort advertisedWindow;
    private bool established;
    private bool finishSent;
    private bool peerFinished;
    private Exception? failure;
    private int retransmissions;
    private uint? timedAcknowledgement;
    private long timedSince;

    private TcpConnection(IIpPacketChannel channel, TcpConnectionParameters parameters, TimeProvider timeProvider)
    {
        this.channel = channel;
        this.parameters = parameters;
        this.timeProvider = timeProvider;
        sendUnacknowledged = parameters.InitialSequenceNumber;
        sendNext = parameters.InitialSequenceNumber;
        retransmissionTimer = timeProvider.CreateTimer(_ => retransmission = RetransmitAsync(), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
    }

    private int FreeSpace => ReceiveBufferCapacity - received.Count;

    private int SendWindowRemaining => peerWindow - (int)(sendNext - sendUnacknowledged);

    /// <summary>Opens a connection: sends the SYN and waits for the SYN-ACK, retransmitting the SYN on the timer.</summary>
    /// <param name="channel">Where the IP packets go and come from.</param>
    /// <param name="parameters">The addresses, ports, initial sequence number and Maximum Segment Size.</param>
    /// <param name="timeProvider">The clock the retransmission timer runs on.</param>
    /// <param name="cancellationToken">Cancels the handshake.</param>
    /// <returns>The connection, established.</returns>
    /// <exception cref="IOException">The peer reset the connection, the SYN timed out or the channel failed.</exception>
    public static async Task<TcpConnection> ConnectAsync(
        IIpPacketChannel channel,
        TcpConnectionParameters parameters,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(channel);
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentNullException.ThrowIfNull(timeProvider);
        TcpConnection connection = new(channel, parameters, timeProvider);
        connection.receiveLoop = connection.ReceiveLoopAsync();
        var handshake = connection.HandshakeAsync(cancellationToken);
        await handshake.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        if (!handshake.IsCompletedSuccessfully)
        {
            await connection.AbortAsync();
        }

        await handshake;
        return connection;
    }

    /// <summary>Waits for data and copies as much as there is, up to the buffer's length.</summary>
    /// <param name="buffer">Where the data goes.</param>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <returns>How many bytes were copied; zero once the peer has sent FIN and every byte before it was read.</returns>
    /// <exception cref="IOException">The peer reset the connection, a segment timed out or the channel failed.</exception>
    public async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        if (buffer.IsEmpty)
        {
            return 0;
        }

        await WaitUntilAsync(() => received.Count > 0 || peerFinished || failure is not null, cancellationToken);
        await gate.WaitAsync(cancellationToken);
        try
        {
            if (received.Count == 0)
            {
                ThrowIfFailed();
                return 0;
            }

            var count = Math.Min(buffer.Length, received.Count);
            CollectionsMarshal.AsSpan(received)[..count].CopyTo(buffer.Span);
            received.RemoveRange(0, count);
            await SendWindowUpdateAsync(cancellationToken);
            return count;
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>Sends every byte of <paramref name="data" /> in segments no larger than the peer's Maximum Segment Size, waiting while its window is full.</summary>
    /// <param name="data">The bytes to send.</param>
    /// <param name="cancellationToken">Cancels the wait for the window.</param>
    /// <returns>A task that completes when the last segment is sent, not when it is acknowledged.</returns>
    /// <exception cref="IOException">The peer reset the connection, a segment timed out or the channel failed.</exception>
    /// <exception cref="ObjectDisposedException">The connection is closed.</exception>
    public async Task WriteAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken)
    {
        while (!data.IsEmpty)
        {
            data = data[await SendWhenWindowOpensAsync(data, cancellationToken)..];
        }
    }

    /// <summary>Closes the connection: sends FIN, waits until everything sent is acknowledged or the connection fails, then stops receiving.</summary>
    /// <returns>A task that completes when the connection is closed.</returns>
    public async ValueTask DisposeAsync()
    {
        await FinishAsync();
        await AbortAsync();
    }

    /// <summary>Stops receiving and retransmitting at once, sending nothing.</summary>
    public void Abort()
    {
        receiveLoopCancellation.Cancel();
        retransmissionTimer.Dispose();
    }

    private static TaskCompletionSource NewSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    private static bool IsBefore(uint sequence, uint other) => (int)(sequence - other) < 0;

    private async Task AbortAsync()
    {
        Abort();
        await receiveLoop;
        await retransmission;
    }

    private async Task HandshakeAsync(CancellationToken cancellationToken)
    {
        await SendSynchronizeAsync(cancellationToken);
        await WaitUntilAsync(() => established || failure is not null, cancellationToken);
        ThrowIfFailed();
    }

    private async Task SendSynchronizeAsync(CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            await SendNewAsync(NewSegment(TcpFlags.Syn, [], parameters.MaximumSegmentSize), cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task<int> SendWhenWindowOpensAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken)
    {
        while (true)
        {
            Task signal;
            await gate.WaitAsync(cancellationToken);
            try
            {
                ThrowIfFailed();
                ObjectDisposedException.ThrowIf(finishSent, this);
                var count = Math.Min(Math.Min(data.Length, peerMaximumSegmentSize), SendWindowRemaining);
                if (count > 0)
                {
                    await SendNewAsync(NewSegment(TcpFlags.Psh, data[..count].ToArray(), null), cancellationToken);
                    return count;
                }

                signal = stateChanged.Task;
            }
            finally
            {
                gate.Release();
            }

            await signal.WaitAsync(cancellationToken);
        }
    }

    private async Task FinishAsync()
    {
        await gate.WaitAsync(CancellationToken.None);
        try
        {
            if (failure is not null || finishSent)
            {
                return;
            }

            finishSent = true;
            await SendNewAsync(NewSegment(TcpFlags.Fin, [], null), CancellationToken.None);
        }
        finally
        {
            gate.Release();
        }

        await WaitUntilAsync(() => unacknowledged.Count == 0 || failure is not null, CancellationToken.None);
    }

    private async Task WaitUntilAsync(Func<bool> condition, CancellationToken cancellationToken)
    {
        while (true)
        {
            Task signal;
            await gate.WaitAsync(cancellationToken);
            try
            {
                if (condition())
                {
                    return;
                }

                signal = stateChanged.Task;
            }
            finally
            {
                gate.Release();
            }

            await signal.WaitAsync(cancellationToken);
        }
    }

    private async Task ReceiveLoopAsync()
    {
        var cancellationToken = receiveLoopCancellation.Token;
        try
        {
            while (true)
            {
                var packet = await channel.ReceiveAsync(cancellationToken);
                await AcceptPacketAsync(packet, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            await gate.WaitAsync(CancellationToken.None);
            Fail(exception);
            gate.Release();
        }
    }

    private async Task AcceptPacketAsync(ReadOnlyMemory<byte> packet, CancellationToken cancellationToken)
    {
        if (!TryReadSegment(packet.Span, out var segment))
        {
            return;
        }

        await gate.WaitAsync(cancellationToken);
        try
        {
            await AcceptSegmentAsync(segment, cancellationToken);
            Signal();
        }
        finally
        {
            gate.Release();
        }
    }

    private bool TryReadSegment(ReadOnlySpan<byte> bytes, [NotNullWhen(true)] out TcpSegment? segment)
    {
        segment = null;
        return IpPacketCodec.TryParse(bytes, out var packet)
            && IsFromPeer(packet!)
            && TcpSegmentCodec.TryParse(packet!.Payload, packet.Source, packet.Destination, out segment)
            && IsForThisConnection(segment!);
    }

    private bool IsFromPeer(IpPacket packet) =>
        packet.Protocol == IpPacketCodec.TcpProtocol
        && packet.Source.Equals(parameters.Remote.Address)
        && packet.Destination.Equals(parameters.Local.Address);

    private bool IsForThisConnection(TcpSegment segment) =>
        segment.SourcePort == parameters.Remote.Port && segment.DestinationPort == parameters.Local.Port;

    private async Task AcceptSegmentAsync(TcpSegment segment, CancellationToken cancellationToken)
    {
        if (failure is not null)
        {
            return;
        }

        if (segment.Flags.HasFlag(TcpFlags.Rst))
        {
            AcceptReset(segment);
        }
        else if (!established)
        {
            await AcceptSynchronizeAcknowledgementAsync(segment, cancellationToken);
        }
        else
        {
            AcceptAcknowledgement(segment);
            await AcceptDataAsync(segment, cancellationToken);
        }
    }

    private void AcceptReset(TcpSegment segment)
    {
        var acceptable = established
            ? segment.SequenceNumber == receiveNext
            : segment.Flags.HasFlag(TcpFlags.Ack) && segment.AcknowledgementNumber == sendNext;
        if (acceptable)
        {
            Fail(new SocketException((int)SocketError.ConnectionReset));
        }
    }

    private async Task AcceptSynchronizeAcknowledgementAsync(TcpSegment segment, CancellationToken cancellationToken)
    {
        const TcpFlags synchronizeAcknowledgement = TcpFlags.Syn | TcpFlags.Ack;
        if ((segment.Flags & synchronizeAcknowledgement) != synchronizeAcknowledgement || segment.AcknowledgementNumber != sendNext)
        {
            return;
        }

        established = true;
        receiveNext = segment.SequenceNumber + 1;
        peerMaximumSegmentSize = segment.MaximumSegmentSize ?? DefaultMaximumSegmentSize;
        AcceptAcknowledgement(segment);
        await SendAcknowledgementAsync(cancellationToken);
    }

    private void AcceptAcknowledgement(TcpSegment segment)
    {
        var acknowledgement = segment.AcknowledgementNumber;
        if (!segment.Flags.HasFlag(TcpFlags.Ack) || IsBefore(acknowledgement, sendUnacknowledged) || IsBefore(sendNext, acknowledgement))
        {
            return;
        }

        peerWindow = segment.Window;
        if (acknowledgement == sendUnacknowledged)
        {
            return;
        }

        sendUnacknowledged = acknowledgement;
        unacknowledged.RemoveAll(sent => !IsBefore(acknowledgement, sent.SequenceNumber + sent.SequenceLength));
        SampleRoundTripTime(acknowledgement);
        retransmissions = 0;
        if (unacknowledged.Count == 0)
        {
            retransmissionTimer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        }
        else
        {
            retransmissionTimer.Change(estimator.Timeout, Timeout.InfiniteTimeSpan);
        }
    }

    private void SampleRoundTripTime(uint acknowledgement)
    {
        if (timedAcknowledgement is { } timed && !IsBefore(acknowledgement, timed))
        {
            estimator.AddSample(timeProvider.GetElapsedTime(timedSince));
            timedAcknowledgement = null;
        }
    }

    private async Task AcceptDataAsync(TcpSegment segment, CancellationToken cancellationToken)
    {
        if (segment.SequenceLength == 0)
        {
            return;
        }

        if (segment.SequenceNumber == receiveNext)
        {
            var taken = Math.Min(segment.Payload.Length, FreeSpace);
            received.AddRange(segment.Payload.AsSpan(0, taken));
            receiveNext += (uint)taken;
            if (taken == segment.Payload.Length && segment.Flags.HasFlag(TcpFlags.Fin))
            {
                receiveNext++;
                peerFinished = true;
            }
        }

        await SendAcknowledgementAsync(cancellationToken);
    }

    private async Task SendWindowUpdateAsync(CancellationToken cancellationToken)
    {
        if (advertisedWindow < ReceiveBufferCapacity / 2 && FreeSpace >= ReceiveBufferCapacity / 2)
        {
            await SendAcknowledgementAsync(cancellationToken);
        }
    }

    private async Task RetransmitAsync()
    {
        await gate.WaitAsync(CancellationToken.None);
        try
        {
            if (unacknowledged.Count == 0 || failure is not null)
            {
                return;
            }

            if (retransmissions == (established ? MaximumDataRetransmissions : MaximumSynRetransmissions))
            {
                Fail(new SocketException((int)SocketError.TimedOut));
                return;
            }

            retransmissions++;
            estimator.BackOff();
            timedAcknowledgement = null;
            retransmissionTimer.Change(estimator.Timeout, Timeout.InfiniteTimeSpan);
            await TransmitAsync(unacknowledged[0], receiveLoopCancellation.Token);
        }
        catch (Exception exception)
        {
            Fail(exception);
        }
        finally
        {
            gate.Release();
        }
    }

    private Task SendAcknowledgementAsync(CancellationToken cancellationToken) =>
        TransmitAsync(NewSegment(TcpFlags.None, [], null), cancellationToken);

    private async Task SendNewAsync(TcpSegment segment, CancellationToken cancellationToken)
    {
        unacknowledged.Add(segment);
        sendNext += segment.SequenceLength;
        if (timedAcknowledgement is null)
        {
            timedAcknowledgement = sendNext;
            timedSince = timeProvider.GetTimestamp();
        }

        if (unacknowledged.Count == 1)
        {
            retransmissionTimer.Change(estimator.Timeout, Timeout.InfiniteTimeSpan);
        }

        await TransmitAsync(segment, cancellationToken);
    }

    private TcpSegment NewSegment(TcpFlags flags, byte[] payload, ushort? maximumSegmentSize) =>
        new((ushort)parameters.Local.Port, (ushort)parameters.Remote.Port, sendNext, 0, flags, 0, maximumSegmentSize, payload);

    private async Task TransmitAsync(TcpSegment segment, CancellationToken cancellationToken)
    {
        advertisedWindow = (ushort)FreeSpace;
        var sent = segment with
        {
            AcknowledgementNumber = established ? receiveNext : 0,
            Flags = established ? segment.Flags | TcpFlags.Ack : segment.Flags,
            Window = advertisedWindow,
        };
        var local = parameters.Local.Address;
        var remote = parameters.Remote.Address;
        var bytes = TcpSegmentCodec.Build(sent, local, remote);
        await channel.SendAsync(IpPacketCodec.Build(new IpPacket(local, remote, IpPacketCodec.TcpProtocol, bytes)), cancellationToken);
    }

    private void ThrowIfFailed()
    {
        if (failure is not null)
        {
            throw new IOException(failure.Message, failure);
        }
    }

    private void Fail(Exception exception)
    {
        failure ??= exception;
        unacknowledged.Clear();
        retransmissionTimer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        Signal();
    }

    private void Signal()
    {
        var previous = stateChanged;
        stateChanged = NewSignal();
        previous.SetResult();
    }
}
