using System.Security.Cryptography;

namespace Curl.Kerberos;

/// <summary>
/// The production <see cref="IKerberosRandomSource" />: cryptographically strong bytes from
/// <see cref="RandomNumberGenerator.Fill" />.
/// </summary>
public sealed class SystemKerberosRandomSource : IKerberosRandomSource
{
    /// <inheritdoc />
    public void Fill(Span<byte> destination) => RandomNumberGenerator.Fill(destination);
}
