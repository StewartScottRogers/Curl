using System.Numerics;
using System.Security.Cryptography;
using Curl.Cryptography;
using Curl.Protocol.Ssh.Transport;

namespace Curl.Protocol.Ssh.KeyExchange;

/// <summary>
/// <c>diffie-hellman-group-exchange-sha1</c> and <c>-sha256</c> (RFC 4419): the client
/// asks for a group size, the server sends a prime and generator, and a Diffie-Hellman
/// round follows in that group.
/// </summary>
/// <param name="hashAlgorithm">The method's hash.</param>
/// <param name="keySource">Where the client's ephemeral key pair comes from.</param>
internal sealed class GroupExchangeSshKeyExchange(HashAlgorithmName hashAlgorithm, ISshEphemeralKeySource keySource) : ISshKeyExchange
{
    /// <summary>
    /// The smallest prime, in bits, the client asks for and accepts: measured from the
    /// Windows reference build's <c>SSH_MSG_KEX_DH_GEX_REQUEST</c> (BL-564).
    /// </summary>
    internal const uint MinimumBits = 2048;

    /// <summary>The preferred prime size in bits, measured likewise.</summary>
    internal const uint PreferredBits = 4096;

    /// <summary>The largest prime, in bits, the client asks for and accepts, measured likewise.</summary>
    internal const uint MaximumBits = 4096;

    /// <inheritdoc />
    public async ValueTask<SshKeyExchangeOutcome> ExchangeAsync(
        SshKeyExchangeMessages messages,
        SshNegotiatedHandshake handshake,
        CancellationToken cancellationToken)
    {
        SshWireWriter request = new();
        request.WriteByte(SshMessageNumber.GroupExchangeRequest);
        request.WriteUInt32(MinimumBits);
        request.WriteUInt32(PreferredBits);
        request.WriteUInt32(MaximumBits);
        await messages.SendAsync(request.ToArray(), cancellationToken).ConfigureAwait(false);

        SshWireReader groupMessage = await messages.ReadAsync(SshMessageNumber.GroupExchangeGroup, cancellationToken).ConfigureAwait(false);
        byte[] prime = groupMessage.ReadMpint().ToArray();
        byte[] generator = groupMessage.ReadMpint().ToArray();
        FiniteFieldDiffieHellmanGroup group = CreateGroup(prime, generator);

        using FiniteFieldDiffieHellman clientKey = keySource.CreateFiniteFieldKey(group);
        SshDiffieHellmanRound round = await SshDiffieHellmanRound.RunAsync(
            messages,
            clientKey,
            SshMessageNumber.GroupExchangeInit,
            SshMessageNumber.GroupExchangeReply,
            cancellationToken).ConfigureAwait(false);

        SshExchangeHashInput hashInput = new(handshake, round.HostKey);
        hashInput.Fields.WriteUInt32(MinimumBits);
        hashInput.Fields.WriteUInt32(PreferredBits);
        hashInput.Fields.WriteUInt32(MaximumBits);
        hashInput.Fields.WriteMpint(prime);
        hashInput.Fields.WriteMpint(generator);
        return round.Finish(hashInput, hashAlgorithm);
    }

    private static FiniteFieldDiffieHellmanGroup CreateGroup(byte[] prime, byte[] generator)
    {
        long bits = new BigInteger(prime, isUnsigned: true, isBigEndian: true).GetBitLength();
        if (bits < MinimumBits || bits > MaximumBits || !FiniteFieldDiffieHellmanGroup.TryCreate(prime, generator, out FiniteFieldDiffieHellmanGroup? group))
        {
            throw new InvalidDataException($"The SSH server's group-exchange prime of {bits} bits and its generator are not a usable group.");
        }

        return group;
    }
}
