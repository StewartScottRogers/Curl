using Curl.Protocol.Abstractions;

namespace Curl.Console;

/// <summary>
/// A TLS provider whose handshake always succeeds and returns the plaintext connection, so
/// the request sent "over TLS" can be read from the scripted connection.
/// </summary>
internal sealed class PassThroughTlsProvider : ITlsProvider
{
    public ValueTask<ConnectResult> AuthenticateAsClientAsync(
        IConnection plaintext,
        string targetHost,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult(ConnectResult.Connected(plaintext));
}
