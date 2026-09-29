namespace Curl.Kerberos;

/// <summary>
/// The random bytes Kerberos encryption needs: the confounder each encryption type puts in
/// front of the plaintext. Injected so the RFCs' test vectors reproduce exactly.
/// </summary>
public interface IKerberosRandomSource
{
    /// <summary>Fills <paramref name="destination" /> with random bytes.</summary>
    /// <param name="destination">The bytes to overwrite.</param>
    void Fill(Span<byte> destination);
}
