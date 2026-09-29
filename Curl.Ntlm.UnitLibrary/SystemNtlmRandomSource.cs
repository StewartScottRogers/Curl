using System.Security.Cryptography;

namespace Curl.Ntlm;

/// <summary>
/// The production <see cref="INtlmRandomSource" />: cryptographically strong bytes from
/// <see cref="RandomNumberGenerator.Fill" />, where curl calls <c>Curl_rand</c>.
/// </summary>
public sealed class SystemNtlmRandomSource : INtlmRandomSource
{
    /// <inheritdoc />
    public void Fill(Span<byte> destination) => RandomNumberGenerator.Fill(destination);
}
