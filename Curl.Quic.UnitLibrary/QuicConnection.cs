using System.Net;
using Curl.Protocol.Abstractions;

namespace Curl.Quic;

/// <summary>
/// A QUIC connection whose handshake is complete, carried over a datagram channel and
/// offered through the multiplexed-connection contract HTTP/3 uses (ADR-0144, BL-721). One
/// loop owns the channel: it sends what the connection has queued, receives the server's
/// datagrams and runs the loss detection timer, and wakes whenever a stream is written,
/// read or aborted so what that queued goes out. Readers, writers and openers never touch
/// the channel; they change the <see cref="QuicClientHandshake" />'s streams under one lock
/// and wait for the loop to report that something arrived.
/// </summary>
public sealed class QuicConnection : IMultiplexedConnection
{
    private const int ReceiveBufferLength = (int)QuicTransportParameters.DefaultMaxUdpPayloadSize;

    private readonly QuicClientHandshake handshake;

    private readonly IDatagramChannel channel;

    private readonly TimeProvider timeProvider;

    private readonly Lock gate = new();

    private readonly CancellationTokenSource stop = new();

    private readonly Task loop;

    private CancellationTokenSource wake = new();

    private TaskCompletionSource changed = NewSignal();

    private MultiplexedConnectionFailedException? failure;

    private bool closed;

    /// <summary>Initializes a new instance of the <see cref="QuicConnection" /> class and starts the loop that carries it.</summary>
    /// <param name="handshake">The handshake, complete, which the connection now owns and disposes.</param>
    /// <param name="channel">The channel the handshake ran over, which the connection now owns and disposes.</param>
    /// <param name="timeProvider">The clock the handshake runs on, for the loss detection timer and pacing.</param>
    /// <exception cref="ArgumentException">The handshake is not complete.</exception>
    public QuicConnection(QuicClientHandshake handshake, IDatagramChannel channel, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(handshake);
        ArgumentNullException.ThrowIfNull(channel);
        ArgumentNullException.ThrowIfNull(timeProvider);
        if (!handshake.IsComplete || handshake.Failure is not null)
        {
            throw new ArgumentException("Only a QUIC handshake that has completed can carry a connection.", nameof(handshake));
        }

        this.handshake = handshake;
        this.channel = channel;
        this.timeProvider = timeProvider;
        loop = RunAsync();
    }

    /// <inheritdoc />
    public EndPoint? RemoteEndPoint => channel.ServerEndPoint;

    /// <inheritdoc />
    public EndPoint? LocalEndPoint => channel.LocalEndPoint;

    /// <inheritdoc />
    public string ApplicationProtocol => handshake.Tls.ApplicationProtocol!;

    /// <inheritdoc />
    public ValueTask<IMultiplexedStream> OpenBidirectionalStreamAsync(CancellationToken cancellationToken) =>
        WaitForStreamAsync(streams => streams.OpenBidirectional(), cancellationToken);

    /// <inheritdoc />
    public ValueTask<IMultiplexedStream> OpenUnidirectionalStreamAsync(CancellationToken cancellationToken) =>
        WaitForStreamAsync(streams => streams.OpenUnidirectional(), cancellationToken);

    /// <inheritdoc />
    public ValueTask<IMultiplexedStream> AcceptUnidirectionalStreamAsync(CancellationToken cancellationToken) =>
        WaitForStreamAsync(streams => streams.AcceptUnidirectional(), cancellationToken);

    /// <inheritdoc />
    public async ValueTask CloseAsync(long applicationErrorCode, CancellationToken cancellationToken)
    {
        IReadOnlyList<byte[]> datagrams;
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(closed, this);
            closed = true;
            datagrams = failure is null ? handshake.CloseWithApplicationError((ulong)applicationErrorCode) : [];
        }

        await stop.CancelAsync().ConfigureAwait(false);
        await loop.ConfigureAwait(false);
        Signal();
        foreach (var datagram in datagrams)
        {
            await channel.SendAsync(datagram, channel.ServerEndPoint, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Closes the connection with application error 0 unless <see cref="CloseAsync" /> already has, then disposes the handshake and the channel.</summary>
    /// <returns>A task that completes when everything is released.</returns>
    public async ValueTask DisposeAsync()
    {
        bool open;
        lock (gate)
        {
            open = !closed;
        }

        if (open)
        {
            await CloseAsync(0, CancellationToken.None).ConfigureAwait(false);
        }

        handshake.Dispose();
        stop.Dispose();
        await channel.DisposeAsync().ConfigureAwait(false);
    }

    /// <summary>Runs <paramref name="attempt" /> under the lock until it returns a value, waiting between tries for the loop to report a change; wakes the loop after each try so what it queued goes out.</summary>
    internal async ValueTask<T> WaitForAsync<T>(Func<T?> attempt, CancellationToken cancellationToken)
        where T : struct
    {
        while (true)
        {
            T? result;
            Task next;
            lock (gate)
            {
                ThrowIfUnusable();
                result = attempt();
                next = changed.Task;
            }

            Wake();
            if (result is { } value)
            {
                return value;
            }

            await next.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Runs <paramref name="change" /> under the lock, then wakes the loop so what it queued goes out.</summary>
    internal void Change(Action change)
    {
        lock (gate)
        {
            ThrowIfUnusable();
            change();
        }

        Wake();
    }

    private static TaskCompletionSource NewSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    private async ValueTask<IMultiplexedStream> WaitForStreamAsync(Func<QuicStreamSet, QuicStream?> take, CancellationToken cancellationToken)
    {
        var stream = await WaitForAsync(() => take(handshake.Streams) is { } taken ? new StreamHandle(taken) : (StreamHandle?)null, cancellationToken).ConfigureAwait(false);
        return new QuicMultiplexedStream(this, stream.Stream);
    }

    private void ThrowIfUnusable()
    {
        ObjectDisposedException.ThrowIf(closed, this);
        if (failure is not null)
        {
            throw failure;
        }
    }

    private void Wake()
    {
        CancellationTokenSource current;
        lock (gate)
        {
            current = wake;
        }

        current.Cancel();
    }

    // Tells every waiter that the streams may have changed.
    private void Signal()
    {
        TaskCompletionSource previous;
        lock (gate)
        {
            previous = changed;
            changed = NewSignal();
        }

        previous.SetResult();
    }

    private async Task RunAsync()
    {
        await Task.Yield();
        var buffer = new byte[ReceiveBufferLength];
        try
        {
            while (!stop.IsCancellationRequested)
            {
                await SendQueuedAsync().ConfigureAwait(false);
                await ReceiveOrTimeOutAsync(buffer).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (stop.IsCancellationRequested)
        {
            // CloseAsync stopped the loop.
        }
        catch (Exception error)
        {
            Fail(new MultiplexedConnectionFailedException(CurlExitCode.RecvError, $"QUIC: the datagram channel failed: {error.Message}"));
        }
    }

    private async Task SendQueuedAsync()
    {
        IReadOnlyList<byte[]> datagrams;
        lock (gate)
        {
            wake = new CancellationTokenSource();
            datagrams = handshake.TakeDatagramsToSend();
        }

        await SendAsync(datagrams).ConfigureAwait(false);
    }

    // Waits for a datagram until the loss detection timer is due or a stream change wakes the loop.
    private async Task ReceiveOrTimeOutAsync(byte[] buffer)
    {
        TimeSpan untilTimeout;
        CancellationToken wakeToken;
        lock (gate)
        {
            untilTimeout = handshake.TimeUntilLossDetectionTimeout;
            wakeToken = wake.Token;
        }

        using var timer = new CancellationTokenSource(untilTimeout, timeProvider);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(stop.Token, wakeToken, timer.Token);
        try
        {
            var received = await channel.ReceiveAsync(buffer, linked.Token).ConfigureAwait(false);
            if (received.RemoteEndPoint.Equals(channel.ServerEndPoint))
            {
                await ReceiveAsync(buffer.AsMemory(0, received.Length)).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (!stop.IsCancellationRequested && timer.IsCancellationRequested)
        {
            IReadOnlyList<byte[]> probes;
            lock (gate)
            {
                probes = handshake.OnLossDetectionTimeout();
            }

            await SendAsync(probes).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!stop.IsCancellationRequested)
        {
            // A stream change woke the loop; the next turn sends what it queued.
        }
    }

    private async Task ReceiveAsync(ReadOnlyMemory<byte> datagram)
    {
        IReadOnlyList<byte[]> answers;
        QuicHandshakeFailure? connectionFailure;
        lock (gate)
        {
            answers = handshake.Receive(datagram);
            connectionFailure = handshake.Failure;
        }

        await SendAsync(answers).ConfigureAwait(false);
        if (connectionFailure is not null)
        {
            Fail(new MultiplexedConnectionFailedException(connectionFailure.ExitCode, connectionFailure.Message));
            return;
        }

        Signal();
    }

    private void Fail(MultiplexedConnectionFailedException error)
    {
        lock (gate)
        {
            failure = error;
        }

        stop.Cancel();
        Signal();
    }

    private async Task SendAsync(IReadOnlyList<byte[]> datagrams)
    {
        foreach (var datagram in datagrams)
        {
            TimeSpan wait;
            lock (gate)
            {
                wait = handshake.TimeUntilSend(datagram.Length);
            }

            await Task.Delay(wait, timeProvider, stop.Token).ConfigureAwait(false);
            lock (gate)
            {
                handshake.OnDatagramSent(datagram.Length);
            }

            await channel.SendAsync(datagram, channel.ServerEndPoint, stop.Token).ConfigureAwait(false);
        }
    }

    private readonly record struct StreamHandle(QuicStream Stream);
}
