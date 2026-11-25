using System.Net;
using Curl.Http2;
using Curl.Protocol.Abstractions;

namespace Curl.Original.Protocol.Http.Fakes;

/// <summary>
/// An <see cref="IConnection" /> over a <see cref="ScriptedConnection" /> whose peer has gone by
/// the time a GOAWAY is due: writing an HTTP/2 GOAWAY frame throws <see cref="IOException" />,
/// and every other read and write is the scripted connection's.
/// </summary>
/// <param name="scripted">The connection that plays the server.</param>
public sealed class GoAwayRefusingConnection(ScriptedConnection scripted) : IConnection
{
    /// <summary>Where an HTTP/2 frame header holds the frame's type (RFC 9113 section 4.1).</summary>
    private const int FrameTypeOffset = 3;

    /// <summary>Gets the number of GOAWAY writes refused.</summary>
    public int RefusedGoAwayCount { get; private set; }

    /// <inheritdoc />
    public bool IsSecure => scripted.IsSecure;

    /// <inheritdoc />
    public EndPoint? RemoteEndPoint => scripted.RemoteEndPoint;

    /// <inheritdoc />
    public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken) => scripted.ReadAsync(buffer, cancellationToken);

    /// <inheritdoc />
    public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
    {
        if (buffer.Length > FrameTypeOffset && buffer.Span[FrameTypeOffset] == (byte)Http2FrameType.GoAway)
        {
            RefusedGoAwayCount++;
            throw new IOException("The peer has gone.");
        }

        return scripted.WriteAsync(buffer, cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask FlushAsync(CancellationToken cancellationToken) => scripted.FlushAsync(cancellationToken);

    /// <inheritdoc />
    public ValueTask DisposeAsync() => scripted.DisposeAsync();
}
