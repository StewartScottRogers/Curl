using System.Security.Cryptography;
using System.Text;
using Curl.Protocol.Ssh.Transport;

namespace Curl.Protocol.Ssh.Keys;

/// <summary>
/// An ECDSA user key on NIST P-256, P-384 or P-521: <c>ecdsa-sha2-nistp256</c>,
/// <c>-nistp384</c> and <c>-nistp521</c> blobs and signatures (RFC 5656 sections 3.1 and
/// 3.1.2), hashed with SHA-256, SHA-384 and SHA-512.
/// </summary>
internal sealed class EcdsaSshPrivateKey : SshPrivateKey
{
    private static readonly EcdsaCurve[] Curves =
    [
        new("nistp256", "1.2.840.10045.3.1.7", ECCurve.NamedCurves.nistP256, HashAlgorithmName.SHA256, 32),
        new("nistp384", "1.3.132.0.34", ECCurve.NamedCurves.nistP384, HashAlgorithmName.SHA384, 48),
        new("nistp521", "1.3.132.0.35", ECCurve.NamedCurves.nistP521, HashAlgorithmName.SHA512, 66),
    ];

    private readonly EcdsaCurve curve;

    private readonly ECParameters parameters;

    private EcdsaSshPrivateKey(EcdsaCurve curve, ECParameters parameters)
    {
        this.curve = curve;
        this.parameters = parameters;
        KeyType = "ecdsa-sha2-" + curve.Identifier;
        SshWireWriter blob = new();
        blob.WriteString(Encoding.ASCII.GetBytes(KeyType));
        blob.WriteString(Encoding.ASCII.GetBytes(curve.Identifier));
        blob.WriteString([0x04, .. parameters.Q.X!, .. parameters.Q.Y!]);
        PublicKeyBlob = blob.ToArray();
    }

    /// <inheritdoc />
    internal override string KeyType { get; }

    /// <inheritdoc />
    internal override byte[] PublicKeyBlob { get; }

    /// <summary>
    /// Builds the key on the curve an ASN.1 object identifier names, as PKCS #8 and SEC 1
    /// files name it.
    /// </summary>
    /// <param name="curveOid">The curve's object identifier, dotted.</param>
    /// <param name="privateKey">d, unsigned big-endian.</param>
    /// <param name="publicPoint">Q as an uncompressed point, or <see langword="null" /> to compute it from d.</param>
    /// <returns>The key, or <see langword="null" /> when the curve is none of the three.</returns>
    /// <exception cref="CryptographicException">The values do not form a key on the curve.</exception>
    internal static EcdsaSshPrivateKey? FromCurveOid(string curveOid, ReadOnlySpan<byte> privateKey, byte[]? publicPoint) =>
        Array.Find(Curves, candidate => candidate.Oid == curveOid) is { } curve ? Create(curve, privateKey, publicPoint) : null;

    /// <summary>
    /// Builds the key on the curve SSH names <paramref name="curveIdentifier" />, as an
    /// <c>openssh-key-v1</c> file names it.
    /// </summary>
    /// <param name="curveIdentifier">The curve's SSH name, such as <c>nistp256</c>.</param>
    /// <param name="privateKey">d, unsigned big-endian.</param>
    /// <param name="publicPoint">Q as an uncompressed point.</param>
    /// <returns>The key, or <see langword="null" /> when the curve is none of the three.</returns>
    /// <exception cref="CryptographicException">The values do not form a key on the curve.</exception>
    internal static EcdsaSshPrivateKey? FromCurveIdentifier(string curveIdentifier, ReadOnlySpan<byte> privateKey, byte[] publicPoint) =>
        Array.Find(Curves, candidate => candidate.Identifier == curveIdentifier) is { } curve ? Create(curve, privateKey, publicPoint) : null;

    /// <inheritdoc />
    private protected override byte[] SignRaw(string algorithm, byte[] data)
    {
        using ECDsa ecdsa = ECDsa.Create(parameters);
        byte[] signature = ecdsa.SignData(data, curve.Hash);
        SshWireWriter values = new();
        values.WriteMpint(signature.AsSpan(0, curve.FieldLength));
        values.WriteMpint(signature.AsSpan(curve.FieldLength));
        return values.ToArray();
    }

    private static EcdsaSshPrivateKey Create(EcdsaCurve curve, ReadOnlySpan<byte> privateKey, byte[]? publicPoint)
    {
        if (privateKey.Length > curve.FieldLength)
        {
            throw new CryptographicException("The ECDSA private key is longer than its curve's order.");
        }

        byte[] d = new byte[curve.FieldLength];
        privateKey.CopyTo(d.AsSpan(curve.FieldLength - privateKey.Length));
        ECParameters parameters = new() { Curve = curve.Curve, D = d, Q = publicPoint is null ? ComputePublicPoint(curve, d) : ReadPoint(curve, publicPoint) };
        using ECDsa check = ECDsa.Create(parameters);
        return new EcdsaSshPrivateKey(curve, parameters);
    }

    private static ECPoint ComputePublicPoint(EcdsaCurve curve, byte[] d)
    {
        using ECDsa ecdsa = ECDsa.Create(new ECParameters { Curve = curve.Curve, D = d });
        return ecdsa.ExportParameters(includePrivateParameters: false).Q;
    }

    private static ECPoint ReadPoint(EcdsaCurve curve, byte[] point) =>
        point.Length == 1 + (2 * curve.FieldLength) && point[0] == 0x04
            ? new ECPoint { X = point[1..(1 + curve.FieldLength)], Y = point[(1 + curve.FieldLength)..] }
            : throw new CryptographicException("The ECDSA public key is not an uncompressed point on its curve.");

    private sealed record EcdsaCurve(string Identifier, string Oid, ECCurve Curve, HashAlgorithmName Hash, int FieldLength);
}
