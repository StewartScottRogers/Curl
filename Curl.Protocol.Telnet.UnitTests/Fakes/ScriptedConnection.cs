using System.Net;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Telnet.Fakes;

/// <summary>
/// An <see cref="IConnection" /> that plays a telnet server from a script: each
/// <see cref="ScriptedRead" /> is returned by one read, in order, and once the script is
/// exhausted every read returns zero, which is the server closing. A scripted read longer
/// than the reader's buffer is returned over as many reads as it takes. Every byte written
/// is recorded in <see cref="Sent" />, and the length of every read buffer in
/// <see cref="ReadBufferLengths" />.
/// </summary>
/// <param name="reads">What the server sends, one read at a time.</param>
public sealed class ScriptedConnection(params ScriptedRead[] reads) : IConnection
{
    private readonly Lock sentLock = new();

    private readonly List<byte> sent = [];

    private readonly List<int> readBufferLengths = [];

    private int nextRead;

    private int readOffset;

    private TaskCompletionSource sentChanged = NewSignal();

    /// <inheritdoc />
    public bool IsSecure => false;

    /// <inheritdoc />
    public EndPoint? RemoteEndPoint => null;

    /// <summary>Gets a value indicating whether the connection has been disposed.</summary>
    public bool IsDisposed { get; private set; }

    /// <summary>Gets the length of the buffer each read was given, in order.</summary>
    public IReadOnlyList<int> ReadBufferLengths => readBufferLengths;

    /// <summary>Gets every byte written so far, in order.</summary>
    public byte[] Sent
    {
        get
        {
            lock (sentLock)
            {
                return [.. sent];
            }
        }
    }

    /// <inheritdoc />
    public async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        readBufferLengths.Add(buffer.Length);
        if (nextRead == reads.Length)
        {
            return 0;
        }

        ScriptedRead read = reads[nextRead];
        await WaitUntilSentAsync(read.AfterBytesSent).ConfigureAwait(false);
        int count = Math.Min(buffer.Length, read.Bytes.Length - readOffset);
        read.Bytes.AsMemory(readOffset, count).CopyTo(buffer);
        readOffset += count;
        if (readOffset == read.Bytes.Length)
        {
            nextRead++;
            readOffset = 0;
        }

        return count;
    }

    /// <inheritdoc />
    public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
    {
        TaskCompletionSource changed;
        lock (sentLock)
        {
            sent.AddRange(buffer.Span);
            changed = sentChanged;
            sentChanged = NewSignal();
        }

        changed.SetResult();
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask FlushAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        IsDisposed = true;
        return ValueTask.CompletedTask;
    }

    private static TaskCompletionSource NewSignal() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private async Task WaitUntilSentAsync(int count)
    {
        while (true)
        {
            Task changed;
            lock (sentLock)
            {
                if (sent.Count >= count)
                {
                    return;
                }

                changed = sentChanged.Task;
            }

            await changed.WaitAsync(TimeSpan.FromSeconds(10)).ConfigureAwait(false);
        }
    }
}
