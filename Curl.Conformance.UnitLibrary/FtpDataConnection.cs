using System.Net;
using System.Runtime.InteropServices;
using Curl.Protocol.Abstractions;

namespace Curl.Conformance;

/// <summary>
/// One passive-mode FTP data connection of upstream's <c>tests/ftpserver.pl</c> (at
/// <c>curl-8_21_0</c>), opened by <c>PASV</c> or <c>EPSV</c> on the control channel: the control
/// channel <see cref="Send"/>s a <c>RETR</c>, <c>LIST</c> or <c>NLST</c> answer into it and
/// <see cref="Close"/>s it. A read with nothing waiting waits until bytes are sent or the
/// connection closes, and returns 0 once it is closed and drained. Bytes the client writes are
/// dropped: uploads are BL-1907's.
/// </summary>
internal sealed class FtpDataConnection : IConnection
{
    private readonly Lock gate = new();

    private readonly List<byte> unreadBytes = [];

    private TaskCompletionSource changed = new(TaskCreationOptions.RunContinuationsAsynchronously);

    private bool closed;

    public bool IsSecure => false;

    public EndPoint? RemoteEndPoint { get; init; }

    public EndPoint? LocalEndPoint { get; init; }

    /// <summary>Makes <paramref name="bytes"/> readable after any sent before them.</summary>
    /// <param name="bytes">The bytes the server writes.</param>
    public void Send(ReadOnlySpan<byte> bytes)
    {
        lock (gate)
        {
            unreadBytes.AddRange(bytes);
        }

        Signal();
    }

    /// <summary>Closes the server's end: once what was sent is read, reads return 0.</summary>
    public void Close()
    {
        lock (gate)
        {
            closed = true;
        }

        Signal();
    }

    public async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        Task signalled;
        lock (gate)
        {
            if (unreadBytes.Count > 0 || closed)
            {
                int count = Math.Min(buffer.Length, unreadBytes.Count);
                CollectionsMarshal.AsSpan(unreadBytes)[..count].CopyTo(buffer.Span);
                unreadBytes.RemoveRange(0, count);
                return count;
            }

            signalled = changed.Task;
        }

        await signalled.WaitAsync(cancellationToken);
        return await ReadAsync(buffer, cancellationToken);
    }

    public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken) => ValueTask.CompletedTask;

    public ValueTask FlushAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private void Signal()
    {
        TaskCompletionSource signal;
        lock (gate)
        {
            signal = changed;
            changed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        signal.TrySetResult();
    }
}
