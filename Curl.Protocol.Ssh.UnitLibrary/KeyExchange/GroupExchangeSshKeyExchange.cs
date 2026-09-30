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
/// <param name="sizes">The prime sizes asked for and accepted: the platform preset's (ADR-0206, ADR-0268).</param>
/// <param name="keySource">Where the client's ephemeral key pair comes from.</param>
internal sealed class GroupExchangeSshKeyExchange(HashAlgorithmName hashAlgorithm, SshGroupExchangeSizes sizes, ISshEphemeralKeySource keySource) : ISshKeyExchange
{
    /// <inheritdoc />
    public async ValueTask<SshKeyExchangeOutcome> ExchangeAsync(
        SshKeyExchangeMessages messages,
        SshNegotiatedHandshake handshake,
        CancellationToken cancellationToken)
    {
        SshWireWriter request = new();
        request.WriteByte(SshMessageNumber.GroupExchangeRequest);
        WriteSizes(request);
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
        WriteSizes(hashInput.Fields);
        hashInput.Fields.WriteMpint(prime);
        hashInput.Fields.WriteMpint(generator);
        return round.Finish(hashInput, hashAlgorithm);
    }

    private void WriteSizes(SshWireWriter writer)
    {
        writer.WriteUInt32(sizes.MinimumBits);
        writer.WriteUInt32(sizes.PreferredBits);
        writer.WriteUInt32(sizes.MaximumBits);
    }

    private FiniteFieldDiffieHellmanGroup CreateGroup(byte[] prime, byte[] generator)
    {
        long bits = new BigInteger(prime, isUnsigned: true, isBigEndian: true).GetBitLength();
        if (bits < sizes.MinimumBits || bits > sizes.MaximumBits || !FiniteFieldDiffieHellmanGroup.TryCreate(prime, generator, out FiniteFieldDiffieHellmanGroup? group))
        {
            throw new InvalidDataException($"The SSH server's group-exchange prime of {bits} bits and its generator are not a usable group.");
        }

        return group;
    }
}
