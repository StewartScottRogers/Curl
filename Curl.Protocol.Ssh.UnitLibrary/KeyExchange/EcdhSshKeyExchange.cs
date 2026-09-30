using System.Security.Cryptography;
using Curl.Protocol.Ssh.Transport;

namespace Curl.Protocol.Ssh.KeyExchange;

/// <summary>
/// <c>ecdh-sha2-nistp256</c>, <c>-nistp384</c> and <c>-nistp521</c> (RFC 5656 section 4):
/// elliptic-curve Diffie-Hellman on a NIST curve, from the BCL's <see cref="ECDiffieHellman" />
/// through <see cref="NistCurveSshKeyShare" />.
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
        using NistCurveSshKeyShare share = new(curve, keySource);
        SshWireWriter init = new();
        init.WriteByte(SshMessageNumber.KeyExchangeDiffieHellmanInit);
        init.WriteString(share.ClientShare);
        await messages.SendAsync(init.ToArray(), cancellationToken).ConfigureAwait(false);

        SshWireReader reply = await messages.ReadAsync(SshMessageNumber.KeyExchangeDiffieHellmanReply, cancellationToken).ConfigureAwait(false);
        byte[] hostKey = reply.ReadString().ToArray();
        byte[] serverPoint = reply.ReadString().ToArray();
        byte[] signature = reply.ReadString().ToArray();
        byte[] encodedSharedSecret = SshExchangeHashInput.EncodeMpint(share.ComputeSharedSecret(serverPoint));

        SshExchangeHashInput hashInput = new(handshake, hostKey);
        hashInput.Fields.WriteString(share.ClientShare);
        hashInput.Fields.WriteString(serverPoint);
        return new SshKeyExchangeOutcome(hostKey, signature, encodedSharedSecret, hashInput.ComputeHash(encodedSharedSecret, curve.HashAlgorithm), curve.HashAlgorithm);
    }
}
