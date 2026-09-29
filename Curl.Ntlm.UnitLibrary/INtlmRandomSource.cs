namespace Curl.Ntlm;

/// <summary>
/// The random bytes an NTLM answer needs: the NTLMv2 client challenge (and, for callers
/// that exchange keys, the exported session key). Injected so MS-NLMP's test vectors and
/// curl's own answers reproduce exactly.
/// </summary>
public interface INtlmRandomSource
{
    /// <summary>Fills <paramref name="destination" /> with random bytes.</summary>
    /// <param name="destination">The bytes to overwrite.</param>
    void Fill(Span<byte> destination);
}
