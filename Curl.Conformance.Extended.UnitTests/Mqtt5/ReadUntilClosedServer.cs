using Curl.Conformance.Extended.Harness;

namespace Curl.Conformance.Extended.UnitTests.Mqtt5;

/// <summary>
/// Serves a connection through <paramref name="server"/> without the run's stop signal, so the server reads
/// until curl closes the connection (BL-4544). The harness cancels its servers as soon as curl returns, and a
/// cancelled read drops bytes curl already sent, so a broker serving under the stop signal could miss curl's
/// last packets.
/// </summary>
/// <param name="server">The server whose record a test reads after the run.</param>
internal sealed class ReadUntilClosedServer(IReferenceServer server) : IReferenceServer
{
    /// <inheritdoc />
    public Task ServeAsync(ReferenceConnection connection, CancellationToken cancellationToken) =>
        server.ServeAsync(connection, CancellationToken.None);
}
