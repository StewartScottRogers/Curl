using System.Security.Cryptography;
using Curl.Protocol.Ssh.Transport;

namespace Curl.Protocol.Ssh.KeyExchange;

/// <summary>
/// <c>ecdh-sha2-nistp256</c>, <c>-nistp384</c> and <c>-nistp521</c> (RFC 5656 section 4):
/// elliptic-curve Diffie-Hellman on a NIST curve, from the BCL's <see cref="ECDiffieHellman" />.
/// </summary>
/// <param name="curve">The curve and its hash.</param>
/// <param name="keySource">Where the client's ephemeral key pair comes from.</param>
internal sealed class EcdhSshKeyExchange(SshNistCurve curve, ISshEphemeralKeySource keySource) : ISshKeyExchange
{
    /// <inheritdoc />
    public async ValueTask<SshKeyExchangeOutcome> ExchangeAsync(
        SshKeyExchangeMessages messages,
        SshNegotiatedHandshake handshake,
        CancellationToken cancellationToken)
    {
        using ECDiffieHellman clientKey = keySource.CreateEllipticCurveKey(curve.Curve);
        byte[] clientPoint = curve.EncodePoint(clientKey.ExportParameters(includePrivateParameters: false).Q);
        SshWireWriter init = new();
        init.WriteByte(SshMessageNumber.KeyExchangeDiffieHellmanInit);
        init.WriteString(clientPoint);
        await messages.SendAsync(init.ToArray(), cancellationToken).ConfigureAwait(false);

        SshWireReader reply = await messages.ReadAsync(SshMessageNumber.KeyExchangeDiffieHellmanReply, cancellationToken).ConfigureAwait(false);
        byte[] hostKey = reply.ReadString().ToArray();
        byte[] serverPoint = reply.ReadString().ToArray();
        byte[] signature = reply.ReadString().ToArray();

        using ECDiffieHellman serverKey = ECDiffieHellman.Create(curve.DecodePublicPoint(serverPoint));
        byte[] sharedSecret = clientKey.DeriveRawSecretAgreement(serverKey.PublicKey);

        SshExchangeHashInput hashInput = new(handshake, hostKey);
        hashInput.Fields.WriteString(clientPoint);
        hashInput.Fields.WriteString(serverPoint);
        return new SshKeyExchangeOutcome(hostKey, signature, sharedSecret, hashInput.ComputeHash(sharedSecret, curve.HashAlgorithm), curve.HashAlgorithm);
    }
}
