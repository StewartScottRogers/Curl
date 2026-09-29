namespace Curl.Tls;

/// <summary>
/// The key exchange parameters of a TLS 1.2 and below ServerKeyExchange: what the server's
/// signature covers, after both hello randoms (RFC 5246 section 7.4.3).
/// </summary>
public abstract record Tls12ServerKeyExchangeParameters
{
    /// <summary>Returns the parameters as they sit in the message.</summary>
    /// <returns>The encoded parameters.</returns>
    public byte[] Encode()
    {
        TlsWriter writer = new();
        Write(writer);
        return writer.ToArray();
    }

    /// <summary>Writes the parameters.</summary>
    internal abstract void Write(TlsWriter writer);
}
