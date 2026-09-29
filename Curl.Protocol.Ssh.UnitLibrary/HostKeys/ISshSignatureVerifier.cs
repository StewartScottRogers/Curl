namespace Curl.Protocol.Ssh.HostKeys;

/// <summary>
/// Checks the server's signature over the exchange hash with one host-key algorithm
/// (RFC 4253 section 8). Whether the host key itself is trusted is a separate check.
/// </summary>
internal interface ISshSignatureVerifier
{
    /// <summary>
    /// Verifies <paramref name="signature" /> over <paramref name="exchangeHash" /> with
    /// <paramref name="hostKey" />.
    /// </summary>
    /// <param name="hostKey">The host key blob, <c>K_S</c>.</param>
    /// <param name="signature">The signature blob: the signature's algorithm name, then its bytes.</param>
    /// <param name="exchangeHash">H.</param>
    /// <returns><see langword="true" /> when the signature is valid.</returns>
    /// <exception cref="InvalidDataException">
    /// A blob is malformed, or names a key type or signature algorithm other than the one
    /// negotiated.
    /// </exception>
    /// <exception cref="System.Security.Cryptography.CryptographicException">The platform refuses the key.</exception>
    bool Verify(ReadOnlyMemory<byte> hostKey, ReadOnlyMemory<byte> signature, byte[] exchangeHash);
}
