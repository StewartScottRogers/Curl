namespace Curl.Tls;

/// <summary>How a <see cref="Tls12BulkCipher" /> lays out a protected record.</summary>
internal enum Tls12CipherMode
{
    /// <summary>The content, then the MAC if there is one.</summary>
    Null,

    /// <summary>CBC with a MAC: MAC-then-encrypt, or encrypt-then-MAC (RFC 7366).</summary>
    Cbc,

    /// <summary>A stream cipher over the content and its MAC, its keystream running on from record to record (RFC 5246 section 6.2.3.1).</summary>
    Stream,

    /// <summary>GCM or CCM with a 4-byte salt from the key block and an 8-byte explicit nonce in the record (RFC 5288, RFC 6655).</summary>
    ExplicitNonceAead,

    /// <summary>An AEAD whose 12-byte IV is XORed with the sequence number, no explicit nonce (RFC 7905).</summary>
    XorNonceAead,
}
