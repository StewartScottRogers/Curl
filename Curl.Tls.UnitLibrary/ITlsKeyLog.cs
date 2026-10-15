namespace Curl.Tls;

/// <summary>
/// Receives a connection's secrets as NSS key-log entries, what OpenSSL hands curl's
/// <c>SSLKEYLOGFILE</c> callback, so a packet capture of the connection can be decrypted.
/// </summary>
public interface ITlsKeyLog
{
    /// <summary>Records one secret.</summary>
    /// <param name="label">The NSS key-log label, such as <c>CLIENT_RANDOM</c> for a TLS 1.2 master secret.</param>
    /// <param name="clientRandom">The 32-byte random of the connection's ClientHello.</param>
    /// <param name="secret">The secret the label names.</param>
    void Append(string label, ReadOnlySpan<byte> clientRandom, ReadOnlySpan<byte> secret);
}
