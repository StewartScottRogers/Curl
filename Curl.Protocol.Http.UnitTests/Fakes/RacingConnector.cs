using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Http.Fakes;

/// <summary>
/// An <see cref="IConnector" /> whose QUIC and TCP connects each complete only when the test
/// completes <see cref="QuicResult" /> or <see cref="TcpResult" />, whether or not their
/// tokens were cancelled, and which records when each started and the token it was given:
/// the fake for the <c>--http3</c> race of QUIC against TCP (ADR-0144 section 4).
/// </summary>
public sealed class RacingConnector : IConnector
{
    private readonly TaskCompletionSource quicStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);

    private readonly TaskCompletionSource tcpStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Gets the source the QUIC connect's result is taken from.</summary>
    public TaskCompletionSource<MultiplexedConnectResult> QuicResult { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Gets the source the TCP connect's result is taken from.</summary>
    public TaskCompletionSource<ConnectResult> TcpResult { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Gets a task that completes when the QUIC connect has started.</summary>
    public Task QuicStarted => quicStarted.Task;

    /// <summary>Gets a task that completes when the TCP connect has started.</summary>
    public Task TcpStarted => tcpStarted.Task;

    /// <summary>Gets the token the QUIC connect was given.</summary>
    public CancellationToken QuicToken { get; private set; }

    /// <summary>Gets the token the TCP connect was given.</summary>
    public CancellationToken TcpToken { get; private set; }

    /// <summary>Gets how many TCP connects were started.</summary>
    public int TcpConnects { get; private set; }

    /// <inheritdoc />
    public async ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken)
    {
        TcpToken = cancellationToken;
        TcpConnects++;
        tcpStarted.TrySetResult();
        return await TcpResult.Task;
    }

    /// <inheritdoc />
    public async ValueTask<MultiplexedConnectResult> ConnectMultiplexedAsync(ConnectTarget target, CancellationToken cancellationToken)
    {
        QuicToken = cancellationToken;
        quicStarted.TrySetResult();
        return await QuicResult.Task;
    }
}
