using Curl.Protocol.Abstractions;

namespace Curl.Conformance;

/// <summary>
/// A connector whose every TCP connection is a <see cref="SmallSendsConnection"/> over the one
/// <paramref name="inner"/> made, so curl's writes reach the case's servers in pieces (BL-2832, ADR-0721).
/// QUIC connections pass through whole.
/// </summary>
/// <param name="inner">The connector the connections come from.</param>
/// <param name="firstWriteLimit">The largest piece of a connection's first write, or <see langword="null"/>.</param>
/// <param name="everyWriteLimit">The largest piece of any write, or <see langword="null"/>.</param>
internal sealed class SmallSendsConnector(IConnector inner, int? firstWriteLimit, int? everyWriteLimit) : IConnector
{
    public async ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken)
    {
        ConnectResult result = await inner.ConnectAsync(target, cancellationToken).ConfigureAwait(false);
        return result.Connection is { } connection
            ? ConnectResult.Connected(new SmallSendsConnection(connection, firstWriteLimit, everyWriteLimit))
            : result;
    }

    public ValueTask<MultiplexedConnectResult> ConnectMultiplexedAsync(ConnectTarget target, CancellationToken cancellationToken) =>
        inner.ConnectMultiplexedAsync(target, cancellationToken);
}
