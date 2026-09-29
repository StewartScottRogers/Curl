namespace Curl.Tls;

/// <summary>
/// The key block divided into each side's write keys (RFC 5246 section 6.3): client MAC
/// key, server MAC key, client key, server key, client IV, server IV, in that order.
/// </summary>
/// <param name="ClientWrite">The keys that protect what the client sends.</param>
/// <param name="ServerWrite">The keys that protect what the server sends.</param>
public sealed record Tls12KeyBlock(Tls12WriteKeys ClientWrite, Tls12WriteKeys ServerWrite)
{
    /// <summary>
    /// Divides <paramref name="keyBlock" /> (from <see cref="TlsPrf.ComputeKeyBlock" />)
    /// by the lengths <paramref name="parameters" /> implies; bytes past
    /// <see cref="Tls12RecordProtectionParameters.KeyBlockLength" /> are ignored.
    /// </summary>
    /// <param name="parameters">The negotiated record protection.</param>
    /// <param name="keyBlock">The key block.</param>
    /// <returns>Both sides' write keys.</returns>
    /// <exception cref="ArgumentException"><paramref name="keyBlock" /> is shorter than the parameters need.</exception>
    public static Tls12KeyBlock Partition(Tls12RecordProtectionParameters parameters, ReadOnlySpan<byte> keyBlock)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        if (keyBlock.Length < parameters.KeyBlockLength)
        {
            throw new ArgumentException($"The key block needs {parameters.KeyBlockLength} bytes; this is {keyBlock.Length}.", nameof(keyBlock));
        }

        int macKeyLength = parameters.MacKeyLength;
        int keyLength = parameters.KeyLength;
        int ivLength = parameters.FixedIvLength;
        int keysStart = 2 * macKeyLength;
        int ivsStart = keysStart + (2 * keyLength);
        return new Tls12KeyBlock(
            new Tls12WriteKeys(
                keyBlock[..macKeyLength].ToArray(),
                keyBlock.Slice(keysStart, keyLength).ToArray(),
                keyBlock.Slice(ivsStart, ivLength).ToArray()),
            new Tls12WriteKeys(
                keyBlock.Slice(macKeyLength, macKeyLength).ToArray(),
                keyBlock.Slice(keysStart + keyLength, keyLength).ToArray(),
                keyBlock.Slice(ivsStart + ivLength, ivLength).ToArray()));
    }
}
