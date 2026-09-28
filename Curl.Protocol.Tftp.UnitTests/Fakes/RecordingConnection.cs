using System.Net;
using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Tftp.Fakes;

/// <summary>
/// An <see cref="IConnection" /> that records every byte written to it and whether it
/// was disposed; reads return <paramref name="reply" />, then end of stream.
/// </summary>
/// <param name="reply">
/// What the connection's reads return, as Latin-1 text; <see langword="null" /> for an
/// empty <c>200</c> reply, <c>""</c> for none.
/// </param>
public sealed class RecordingConnection(string? reply = null) : IConnection
{
    private readonly MemoryStream written = new();

    private readonly MemoryStream replyBytes = new(Encoding.Latin1.GetBytes(reply ?? "HTTP/1.1 200 OK\r\n\r\n"));

    /// <summary>
    /// Gets every byte written, in order.
    /// </summary>
    public byte[] Written => written.ToArray();

    /// <summary>
    /// Gets how many reply bytes were read.
    /// </summary>
    public long ReplyBytesRead => replyBytes.Position;

    /// <summary>
    /// Gets a value indicating whether the connection was disposed.
    /// </summary>
    public bool IsDisposed { get; private set; }

    /// <inheritdoc />
    public bool IsSecure => false;

    /// <inheritdoc />
    public EndPoint? RemoteEndPoint => null;

    /// <inheritdoc />
    public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken) =>
        ValueTask.FromResult(replyBytes.Read(buffer.Span));

    /// <inheritdoc />
    public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
    {
        written.Write(buffer.Span);
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
}
