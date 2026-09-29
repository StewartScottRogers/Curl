using System.Net;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Http.Fakes;

/// <summary>
/// An in-memory <see cref="IMultiplexedConnection" />: hands out scripted request streams in
/// order, opens recorded unidirectional streams with client-initiated IDs 2, 6, 10 and on,
/// and records how it was closed and disposed.
/// </summary>
/// <param name="requestStreams">The bidirectional streams the opens return, first to last.</param>
public sealed class FakeMultiplexedConnection(params FakeMultiplexedStream[] requestStreams) : IMultiplexedConnection
{
    private readonly Queue<FakeMultiplexedStream> pending = new(requestStreams);

    /// <inheritdoc />
    public EndPoint? RemoteEndPoint { get; init; }

    /// <inheritdoc />
    public EndPoint? LocalEndPoint { get; init; }

    /// <inheritdoc />
    public string ApplicationProtocol { get; init; } = "h3";

    /// <summary>
    /// Gets the exception opening a stream throws, or <see langword="null" /> to open streams.
    /// </summary>
    public Exception? OpenException { get; init; }

    /// <summary>
    /// Gets the exception <see cref="CloseAsync" /> throws, or <see langword="null" /> to close.
    /// </summary>
    public Exception? CloseException { get; init; }

    /// <summary>Gets the unidirectional streams the client opened, in order.</summary>
    public List<FakeMultiplexedStream> UnidirectionalStreams { get; } = [];

    /// <summary>Gets the application error code the connection was closed with, or <see langword="null" />.</summary>
    public long? CloseCode { get; private set; }

    /// <summary>Gets a value indicating whether the connection was disposed.</summary>
    public bool IsDisposed { get; private set; }

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">Every scripted stream has been opened.</exception>
    public ValueTask<IMultiplexedStream> OpenBidirectionalStreamAsync(CancellationToken cancellationToken)
    {
        if (OpenException is not null)
        {
            return ValueTask.FromException<IMultiplexedStream>(OpenException);
        }

        return pending.TryDequeue(out FakeMultiplexedStream? stream)
            ? ValueTask.FromResult<IMultiplexedStream>(stream)
            : throw new InvalidOperationException("No request stream was scripted.");
    }

    /// <inheritdoc />
    public ValueTask<IMultiplexedStream> OpenUnidirectionalStreamAsync(CancellationToken cancellationToken)
    {
        if (OpenException is not null)
        {
            return ValueTask.FromException<IMultiplexedStream>(OpenException);
        }

        FakeMultiplexedStream stream = new(2 + (4 * UnidirectionalStreams.Count), []);
        UnidirectionalStreams.Add(stream);
        return ValueTask.FromResult<IMultiplexedStream>(stream);
    }

    /// <inheritdoc />
    /// <exception cref="NotSupportedException">Always: the handler reads no server stream yet.</exception>
    public ValueTask<IMultiplexedStream> AcceptUnidirectionalStreamAsync(CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    /// <inheritdoc />
    public ValueTask CloseAsync(long applicationErrorCode, CancellationToken cancellationToken)
    {
        if (CloseException is not null)
        {
            return ValueTask.FromException(CloseException);
        }

        CloseCode = applicationErrorCode;
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        IsDisposed = true;
        return ValueTask.CompletedTask;
    }
}
