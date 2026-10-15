using System.Net;
using System.Security.Cryptography.X509Certificates;
using Curl.Protocol.Abstractions;

namespace Curl.Conformance;

/// <summary>
/// An <see cref="IConnector"/> that stands in for upstream's MQTTS server, <c>tests/server/mqttd.c</c>
/// behind <c>stunnel</c>: a connection to <see cref="MqttsPort"/> (<c>%MQTTSPORT</c>) speaks TLS with
/// the certificate it is given, and the decrypted packets reach the plain MQTT server on
/// <see cref="MqttServerConnector.MqttPort"/> of the connector it wraps, which records them for
/// <c>&lt;verify&gt;&lt;protocol&gt;</c>. Every other connection reaches the wrapped connector as it is.
/// No socket is opened.
/// </summary>
/// <param name="certificate">The server certificate, with its private key; see <see cref="HttpsServerConnector.LoadCertificate"/>.</param>
/// <param name="backend">The connector whose plain MQTT server the TLS connections reach, and which every other connection reaches.</param>
public sealed class MqttsServerConnector(X509Certificate2 certificate, IConnector backend) : IConnector
{
    /// <summary>The port of the MQTTS server, <c>%MQTTSPORT</c>: servers.pm picks a free port, and this one no other stand-in uses.</summary>
    public const int MqttsPort = 9013;

    private readonly TlsServerOptions options = new(certificate, [], false);

    /// <summary>Gets the <c>&lt;server&gt;</c> names whose cases reach this stand-in: <c>mqtts</c>.</summary>
    public static IReadOnlyList<string> EmulatedServers { get; } = ["mqtts"];

    /// <summary>Opens a TLS connection to the plain MQTT server when <paramref name="target"/>'s port is <see cref="MqttsPort"/>; any other on the wrapped connector.</summary>
    /// <param name="target">The host and port to connect to.</param>
    /// <param name="cancellationToken">Passed to the server connected to.</param>
    /// <returns>The connected result.</returns>
    public async ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (target.Port != MqttsPort)
        {
            return await backend.ConnectAsync(target, cancellationToken).ConfigureAwait(false);
        }

        ConnectResult connected = await backend.ConnectAsync(new ConnectTarget(target.Host, MqttServerConnector.MqttPort, target.UseTls), cancellationToken).ConfigureAwait(false);
        return ConnectResult.Connected(new TlsRelayConnection(connected.Connection!, options, new IPEndPoint(IPAddress.Loopback, target.Port)));
    }
}
