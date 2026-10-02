using System.Net;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Imap.Fakes;

/// <summary>
/// An <see cref="IConnector" /> that reports the connection it returns as opened, numbered 3,
/// on the target's events, as <c>TcpConnector</c> reports its <c>Established connection</c>
/// line, then answers with <paramref name="result" />.
/// </summary>
/// <param name="result">The result of the connect.</param>
public sealed class OpenedReportingConnector(ConnectResult result) : IConnector
{
    /// <inheritdoc />
    public ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken)
    {
        target.Events.ReportConnectionOpened(new ConnectionOpenedEvent
        {
            HostName = target.Host,
            RemoteEndPoint = new IPEndPoint(IPAddress.Loopback, target.Port),
            LocalEndPoint = new IPEndPoint(IPAddress.Loopback, 50000),
            ConnectionNumber = 3,
        });
        return ValueTask.FromResult(result);
    }
}
