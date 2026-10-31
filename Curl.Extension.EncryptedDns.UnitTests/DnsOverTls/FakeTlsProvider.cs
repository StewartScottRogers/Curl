using Curl.Protocol.Abstractions;

namespace Curl.Extension.EncryptedDns.DnsOverTls;

/// <summary>
/// A TLS provider that records the plaintext connection, the bytes already written on it and the host name it was
/// asked to verify, and answers with <see cref="Secure" />, or fails with exit 35 when it is <see langword="null" />.
/// </summary>
internal sealed class FakeTlsProvider : ITlsProvider
{
    public ScriptedConnection? Secure { get; init; } = new(isSecure: true);

    public IConnection? Plaintext { get; private set; }

    public int PlaintextBytesWrittenBeforeHandshake { get; private set; } = -1;

    public string? TargetHost { get; private set; }

    public ValueTask<ConnectResult> AuthenticateAsClientAsync(IConnection plaintext, string targetHost, CancellationToken cancellationToken)
    {
        Plaintext = plaintext;
        PlaintextBytesWrittenBeforeHandshake = ((ScriptedConnection)plaintext).Written.Count;
        TargetHost = targetHost;
        return ValueTask.FromResult(Secure is null
            ? ConnectResult.Failed(CurlExitCode.SslConnectError, "TLS handshake failed")
            : ConnectResult.Connected(Secure));
    }
}
