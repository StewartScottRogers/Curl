namespace Curl.Networking;

/// <summary>
/// Which TLS client runs a connection's handshake, as <see cref="TlsClientRouting" />
/// chooses it (ADR-0140, "The routing rule").
/// </summary>
public enum TlsClientRoute
{
    /// <summary>.NET's <c>SslStream</c>, through <see cref="SslStreamTlsProvider" />: every option set it can honour.</summary>
    SslStream,

    /// <summary>The hand-built client in <c>Curl.Tls.UnitLibrary</c>, through <see cref="HandBuiltTlsProvider" />.</summary>
    HandBuilt,
}
