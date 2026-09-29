using System.Security.Cryptography;
using Curl.Cryptography;

namespace Curl.Protocol.Ssh.KeyExchange;

/// <summary>
/// The production <see cref="ISshEphemeralKeySource" />: fresh random key pairs from the
/// BCL's <see cref="ECDiffieHellman" /> and <see cref="FiniteFieldDiffieHellman.Generate" />.
/// </summary>
internal sealed class SystemSshEphemeralKeySource : ISshEphemeralKeySource
{
    /// <inheritdoc />
    public ECDiffieHellman CreateEllipticCurveKey(ECCurve curve) => ECDiffieHellman.Create(curve);

    /// <inheritdoc />
    public FiniteFieldDiffieHellman CreateFiniteFieldKey(FiniteFieldDiffieHellmanGroup group) =>
        FiniteFieldDiffieHellman.Generate(group);
}
