using System.Net;
using System.Threading.Channels;
using Curl.Http2;
using Curl.Protocol.Abstractions;

namespace Curl.Console;

/// <summary>One connection of an <see cref="Http2ServerConnector" />: the server's side of it.</summary>
internal sealed class Http2ServerConnection : IConnection
{
    private static readonly int PrefaceLength = Http2Connection.ClientPreface.Length;

    private readonly Channel<byte[]> toClient = Channel.CreateUnbounded<byte[]>();

    private readonly List<byte> fromClient = [];

    private readonly HpackEncoder encoder = new();

    private readonly Lock gate = new();

    private readonly List<int> held = [];

    private readonly List<(int Count, TaskCompletionSource Reached)> streamWaiters = [];

    private readonly List<(int Count, TaskCompletionSource Reached)> readWaiters = [];

    private readonly bool holdsResponses;

    private byte[] unread = [];

    private int readCount;

    internal Http2ServerConnection(string host, uint? maxConcurrentStreams, bool holdsResponses)
    {
        Host = host;
        this.holdsResponses = holdsResponses;
        Http2Setting[] settings = maxConcurrentStreams is { } limit ? [new(Http2SettingIdentifier.MaxConcurrentStreams, limit)] : [];
        toClient.Writer.TryWrite(Http2FrameCodec.Serialize(Http2FrameFactory.CreateSettings(settings)));
    }

    /// <summary>Gets the host the connection was opened to.</summary>
    public string Host { get; }

    /// <summary>Gets the stream identifiers the client opened, in order.</summary>
    public List<int> StreamIds { get; } = [];

    /// <summary>Gets a value indicating whether the client closed the connection.</summary>
    public bool IsDisposed { get; private set; }

    /// <inheritdoc />
    public bool IsSecure => false;

    /// <inheritdoc />
    public EndPoint? RemoteEndPoint => null;

    /// <summary>Answers every stream whose response is held.</summary>
    public void ReleaseResponses()
    {
        lock (gate)
        {
            foreach (int streamId in held)
            {
                Respond(streamId);
            }

            held.Clear();
        }
    }

    /// <summary>Waits until the client has opened <paramref name="count" /> streams.</summary>
    /// <param name="count">The number of streams.</param>
    /// <returns>A task that completes when it has.</returns>
    public Task StreamsOpenedAsync(int count) => WaitForAsync(streamWaiters, count, () => StreamIds.Count);

    /// <summary>Waits until the client has asked for <paramref name="count" /> reads.</summary>
    /// <param name="count">The number of reads.</param>
    /// <returns>A task that completes when it has.</returns>
    public Task ReadsAskedAsync(int count) => WaitForAsync(readWaiters, count, () => readCount);

    /// <inheritdoc />
    public async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        lock (gate)
        {
            readCount++;
            Signal(readWaiters, readCount);
        }

        if (unread.Length == 0)
        {
            unread = await toClient.Reader.WaitToReadAsync(cancellationToken) && toClient.Reader.TryRead(out byte[]? chunk) ? chunk : [];
        }

        int count = Math.Min(unread.Length, buffer.Length);
        unread.AsSpan(0, count).CopyTo(buffer.Span);
        unread = unread[count..];
        return count;
    }

    /// <inheritdoc />
    public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
    {
        lock (gate)
        {
            fromClient.AddRange(buffer.ToArray());
            ReadClientFrames();
        }

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask FlushAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        IsDisposed = true;
        toClient.Writer.TryComplete();
        return ValueTask.CompletedTask;
    }

    private static void Signal(List<(int Count, TaskCompletionSource Reached)> waiters, int current)
    {
        foreach ((int count, TaskCompletionSource reached) in waiters.Where(waiter => waiter.Count <= current))
        {
            reached.TrySetResult();
        }
    }

    private Task WaitForAsync(List<(int Count, TaskCompletionSource Reached)> waiters, int count, Func<int> current)
    {
        lock (gate)
        {
            TaskCompletionSource reached = new(TaskCreationOptions.RunContinuationsAsynchronously);
            waiters.Add((count, reached));
            Signal(waiters, current());
            return reached.Task;
        }
    }

    /// <summary>Takes every whole frame the client has written after its preface, answering each HEADERS.</summary>
    private void ReadClientFrames()
    {
        int offset = PrefaceLength;
        while (fromClient.Count >= offset + 9)
        {
            int length = (fromClient[offset] << 16) | (fromClient[offset + 1] << 8) | fromClient[offset + 2];
            if (fromClient.Count < offset + 9 + length)
            {
                break;
            }

            int streamId = ((fromClient[offset + 5] & 0x7F) << 24) | (fromClient[offset + 6] << 16) | (fromClient[offset + 7] << 8) | fromClient[offset + 8];
            if (fromClient[offset + 3] == (byte)Http2FrameType.Headers)
            {
                OpenStream(streamId);
            }

            offset += 9 + length;
        }

        fromClient.RemoveRange(PrefaceLength, offset - PrefaceLength);
    }

    private void OpenStream(int streamId)
    {
        StreamIds.Add(streamId);
        Signal(streamWaiters, StreamIds.Count);
        if (holdsResponses)
        {
            held.Add(streamId);
        }
        else
        {
            Respond(streamId);
        }
    }

    private void Respond(int streamId) =>
        toClient.Writer.TryWrite(Http2FrameCodec.Serialize(Http2FrameFactory.CreateHeaders(streamId, encoder.Encode([new(":status", "200")]), isEndStream: true, isEndHeaders: true)));
}
