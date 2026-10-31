using System.Net;
using Curl.Protocol.Abstractions;

namespace Curl.Ssh;

/// <summary>
/// The connection an SSH session's transport reads and writes, passed through to the
/// connection of the transfer using the session now: a reused connection is handed to each
/// transfer as a connection of its own, so <see cref="Current" /> is switched to it while the
/// transport, its keys and its counters stay as they were (CGR-130, ADR-0865).
/// </summary>
/// <param name="current">The connection of the transfer that opened the session.</param>
internal sealed class SshSessionConnection(IConnection current) : IConnection
{
    /// <summary>Gets or sets the connection of the transfer using the session.</summary>
    internal IConnection Current { get; set; } = current;

    /// <inheritdoc />
    public bool IsSecure => Current.IsSecure;

    /// <inheritdoc />
    public EndPoint? RemoteEndPoint => Current.RemoteEndPoint;

    /// <inheritdoc />
    public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken) => Current.ReadAsync(buffer, cancellationToken);

    /// <inheritdoc />
    public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken) => Current.WriteAsync(buffer, cancellationToken);

    /// <inheritdoc />
    public ValueTask FlushAsync(CancellationToken cancellationToken) => Current.FlushAsync(cancellationToken);

    /// <summary>Does nothing: the transfer's own connection is disposed by the transfer.</summary>
    /// <returns>A completed task.</returns>
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
