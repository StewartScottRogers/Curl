using System.Diagnostics;
using System.Net;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Http.Fakes;

/// <summary>
/// An in-memory <see cref="IMultiplexedConnection" />: hands out scripted request streams in
/// order, opens recorded unidirectional streams with client-initiated IDs 2, 6, 10 and on,
/// hands out scripted server streams to accepts, then waits until an accept is cancelled,
/// and records how it was closed and disposed.
/// </summary>
/// <param name="requestStreams">The bidirectional streams the opens return, first to last.</param>
public sealed class FakeMultiplexedConnection(params FakeMultiplexedStream[] requestStreams) : IMultiplexedConnection
{
    private readonly Queue<FakeMultiplexedStream> pending = new(requestStreams);

    private readonly Queue<FakeMultiplexedStream> serverStreams = new();

    private readonly TaskCompletionSource disposed = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <inheritdoc />
    public EndPoint? RemoteEndPoint { get; init; }

    /// <inheritdoc />
    public EndPoint? LocalEndPoint { get; init; }

    /// <inheritdoc />
    public string ApplicationProtocol { get; init; } = "h3";

    /// <inheritdoc />
    public long? BidirectionalStreamLimit { get; init; }

    /// <summary>
    /// Gets the exception opening a stream throws, or <see langword="null" /> to open streams.
    /// </summary>
    public Exception? OpenException { get; init; }

    /// <summary>
    /// Gets the exception opening a bidirectional stream throws once the unidirectional
    /// streams are open, or <see langword="null" /> to open them.
    /// </summary>
    public Exception? BidirectionalOpenException { get; init; }

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

    /// <summary>Gets a task that completes when the connection is disposed.</summary>
    public Task Disposed => disposed.Task;

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">Every scripted stream has been opened.</exception>
    public ValueTask<IMultiplexedStream> OpenBidirectionalStreamAsync(CancellationToken cancellationToken)
    {
        if ((OpenException ?? BidirectionalOpenException) is { } failure)
        {
            return ValueTask.FromException<IMultiplexedStream>(failure);
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

    /// <summary>
    /// Gets the unidirectional streams the server opens, handed out by
    /// <see cref="AcceptUnidirectionalStreamAsync" /> first to last.
    /// </summary>
    public IEnumerable<FakeMultiplexedStream> ServerStreams { init => serverStreams = new(value); }

    /// <summary>
    /// Gets the exception accepting throws once <see cref="ServerStreams" /> are used up, or
    /// <see langword="null" /> to wait until the accept is cancelled.
    /// </summary>
    public Exception? AcceptException { get; init; }

    /// <summary>Gets a value indicating whether an accept waiting for a server stream was cancelled.</summary>
    public bool IsAcceptCancelled { get; private set; }

    /// <inheritdoc />
    public async ValueTask<IMultiplexedStream> AcceptUnidirectionalStreamAsync(CancellationToken cancellationToken)
    {
        if (serverStreams.TryDequeue(out FakeMultiplexedStream? stream))
        {
            return stream;
        }

        if (AcceptException is not null)
        {
            throw AcceptException;
        }

        try
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
        }
        finally
        {
            IsAcceptCancelled = true;
        }

        throw new UnreachableException();
    }

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
        disposed.TrySetResult();
        return ValueTask.CompletedTask;
    }
}
