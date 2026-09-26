using System.Net;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Telnet.Fakes;

/// <summary>
/// An <see cref="IConnection" /> that plays a telnet server from a script: each
/// <see cref="ScriptedRead" /> is returned by one read, in order, and once the script is
/// exhausted every read returns zero, which is the server closing. Every byte written is
/// recorded in <see cref="Sent" />.
/// </summary>
/// <param name="reads">What the server sends, one read at a time.</param>
public sealed class ScriptedConnection(params ScriptedRead[] reads) : IConnection
{
    private readonly Lock sentLock = new();

    private readonly List<byte> sent = [];

    private int nextRead;

    private TaskCompletionSource sentChanged = NewSignal();

    /// <inheritdoc />
    public bool IsSecure => false;

    /// <inheritdoc />
    public EndPoint? RemoteEndPoint => null;

    /// <summary>Gets a value indicating whether the connection has been disposed.</summary>
    public bool IsDisposed { get; private set; }

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
        if (nextRead == reads.Length)
        {
            return 0;
        }

        ScriptedRead read = reads[nextRead++];
        await WaitUntilSentAsync(read.AfterBytesSent).ConfigureAwait(false);
        read.Bytes.CopyTo(buffer);
        return read.Bytes.Length;
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
