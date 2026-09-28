using System.Security.Cryptography;

namespace Curl.Protocol.Ssh;

/// <summary>
/// The production <see cref="ISshRandomSource" />: cryptographically strong bytes from
/// <see cref="RandomNumberGenerator.Fill" />.
/// </summary>
public sealed class SystemSshRandomSource : ISshRandomSource
{
    /// <inheritdoc />
    public void Fill(Span<byte> destination) => RandomNumberGenerator.Fill(destination);
}
