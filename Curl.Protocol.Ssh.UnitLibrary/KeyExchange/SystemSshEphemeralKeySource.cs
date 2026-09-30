using System.Security.Cryptography;
using Curl.Cryptography;

namespace Curl.Protocol.Ssh.KeyExchange;

/// <summary>
/// The production <see cref="ISshEphemeralKeySource" />: fresh random keys from the BCL's
/// <see cref="ECDiffieHellman" /> and from <c>Curl.Cryptography</c>'s <see cref="FiniteFieldDiffieHellman.Generate" />,
/// <see cref="X25519.GeneratePrivateKey" />, <see cref="MlKem.GenerateKey(MlKemParameterSet)" /> and
/// <see cref="Sntrup761.GenerateKeyPair(Span{byte}, Span{byte})" />.
/// </summary>
internal sealed class SystemSshEphemeralKeySource : ISshEphemeralKeySource
{
    /// <inheritdoc />
    public ECDiffieHellman CreateEllipticCurveKey(ECCurve curve) => ECDiffieHellman.Create(curve);

    /// <inheritdoc />
    public FiniteFieldDiffieHellman CreateFiniteFieldKey(FiniteFieldDiffieHellmanGroup group) =>
        FiniteFieldDiffieHellman.Generate(group);

    /// <inheritdoc />
    public void CreateX25519PrivateKey(Span<byte> privateKey) => X25519.GeneratePrivateKey(privateKey);

    /// <inheritdoc />
    public MlKem CreateMlKemKey(MlKemParameterSet parameterSet) => MlKem.GenerateKey(parameterSet);

    /// <inheritdoc />
    public void CreateSntrup761KeyPair(Span<byte> publicKey, Span<byte> secretKey) => Sntrup761.GenerateKeyPair(publicKey, secretKey);
}
