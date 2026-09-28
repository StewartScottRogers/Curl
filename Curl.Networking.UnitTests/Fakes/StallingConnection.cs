using System.Net;

using Curl.Protocol.Abstractions;

namespace Curl.Networking.Fakes;

/// <summary>
/// An <see cref="IConnection" /> that takes every write and whose every read never completes,
/// as a proxy that never answers CONNECT does, until its token is cancelled.
/// <see cref="OnStalled" /> runs as each read starts waiting, so a test can move the clock at
/// that moment.
/// </summary>
public sealed class StallingConnection : IConnection
{
    /// <summary>Gets or sets what runs as each read starts waiting; nothing by default.</summary>
    public Action OnStalled { get; init; } = () => { };

    /// <summary>Gets a value indicating whether the connection was disposed.</summary>
    public bool IsDisposed { get; private set; }

    /// <inheritdoc />
    public bool IsSecure => false;

    /// <inheritdoc />
    public EndPoint? RemoteEndPoint => null;

    /// <inheritdoc />
    public async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        OnStalled();
        await Task.Delay(Timeout.Infinite, cancellationToken);
        throw new InvalidOperationException("An infinite delay ended without being cancelled.");
    }

    /// <inheritdoc />
    public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken) => ValueTask.CompletedTask;

    /// <inheritdoc />
    public ValueTask FlushAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        IsDisposed = true;
        return ValueTask.CompletedTask;
    }
}
