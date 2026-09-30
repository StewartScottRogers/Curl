using System.Security.Cryptography;

namespace Curl.Protocol.Ssh.KeyExchange;

/// <summary>
/// Elliptic-curve Diffie-Hellman on a NIST curve, from the BCL's <see cref="ECDiffieHellman" />:
/// uncompressed points each way (RFC 5656 section 4), the server's checked to lie on the
/// curve, and the secret the x-coordinate of the product, <see cref="SshNistCurve.FieldLength" />
/// bytes.
/// </summary>
internal sealed class NistCurveSshKeyShare : ISshKeyShare
{
    private readonly SshNistCurve curve;

    private readonly ECDiffieHellman clientKey;

    /// <summary>
    /// Initializes a new instance of the <see cref="NistCurveSshKeyShare" /> class with a key
    /// pair from <paramref name="keySource" />.
    /// </summary>
    /// <param name="curve">The curve.</param>
    /// <param name="keySource">Where the key pair comes from.</param>
    internal NistCurveSshKeyShare(SshNistCurve curve, ISshEphemeralKeySource keySource)
    {
        this.curve = curve;
        clientKey = keySource.CreateEllipticCurveKey(curve.Curve);
        ClientShare = curve.EncodePoint(clientKey.ExportParameters(includePrivateParameters: false).Q);
    }

    /// <inheritdoc />
    public byte[] ClientShare { get; }

    /// <inheritdoc />
    public int ServerShareLength => 1 + (2 * curve.FieldLength);

    /// <inheritdoc />
    public byte[] ComputeSharedSecret(ReadOnlySpan<byte> serverShare)
    {
        using ECDiffieHellman serverKey = ECDiffieHellman.Create(curve.DecodePublicPoint(serverShare));
        return clientKey.DeriveRawSecretAgreement(serverKey.PublicKey);
    }

    /// <inheritdoc />
    public void Dispose() => clientKey.Dispose();
}
