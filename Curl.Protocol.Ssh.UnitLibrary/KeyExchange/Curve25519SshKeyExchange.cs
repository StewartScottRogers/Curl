using System.Security.Cryptography;
using Curl.Cryptography;
using Curl.Protocol.Ssh.Transport;

namespace Curl.Protocol.Ssh.KeyExchange;

/// <summary>
/// <c>curve25519-sha256</c> and its older name <c>curve25519-sha256@libssh.org</c> (RFC 8731):
/// X25519 from the hand-built <see cref="X25519" />, through <see cref="X25519SshKeyShare" />,
/// over the <c>SSH_MSG_KEX_ECDH_*</c> messages of RFC 5656, with SHA-256. The 32 bytes X25519
/// produces are K read as an unsigned big-endian integer, and an all-zero K is refused.
/// </summary>
/// <param name="keySource">Where the client's ephemeral private key comes from.</param>
internal sealed class Curve25519SshKeyExchange(ISshEphemeralKeySource keySource) : ISshKeyExchange
{
    /// <inheritdoc />
    public async ValueTask<SshKeyExchangeOutcome> ExchangeAsync(
        SshKeyExchangeMessages messages,
        SshNegotiatedHandshake handshake,
        CancellationToken cancellationToken)
    {
        using X25519SshKeyShare share = new(keySource);
        SshWireWriter init = new();
        init.WriteByte(SshMessageNumber.KeyExchangeDiffieHellmanInit);
        init.WriteString(share.ClientShare);
        await messages.SendAsync(init.ToArray(), cancellationToken).ConfigureAwait(false);

        SshWireReader reply = await messages.ReadAsync(SshMessageNumber.KeyExchangeDiffieHellmanReply, cancellationToken).ConfigureAwait(false);
        byte[] hostKey = reply.ReadString().ToArray();
        byte[] serverPublicKey = reply.ReadString().ToArray();
        byte[] signature = reply.ReadString().ToArray();
        byte[] encodedSharedSecret = SshExchangeHashInput.EncodeMpint(share.ComputeSharedSecret(serverPublicKey));

        SshExchangeHashInput hashInput = new(handshake, hostKey);
        hashInput.Fields.WriteString(share.ClientShare);
        hashInput.Fields.WriteString(serverPublicKey);
        return new SshKeyExchangeOutcome(hostKey, signature, encodedSharedSecret, hashInput.ComputeHash(encodedSharedSecret, HashAlgorithmName.SHA256), HashAlgorithmName.SHA256);
    }
}
