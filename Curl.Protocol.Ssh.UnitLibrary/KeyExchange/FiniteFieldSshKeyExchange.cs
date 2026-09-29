using System.Security.Cryptography;
using Curl.Cryptography;
using Curl.Protocol.Ssh.Transport;

namespace Curl.Protocol.Ssh.KeyExchange;

/// <summary>
/// <c>diffie-hellman-group1-sha1</c>, <c>group14-sha1</c> (RFC 4253 section 8),
/// <c>group14-sha256</c>, <c>group16-sha512</c> and <c>group18-sha512</c> (RFC 8268):
/// Diffie-Hellman in a fixed MODP group, from <see cref="FiniteFieldDiffieHellman" />.
/// </summary>
/// <param name="group">The MODP group.</param>
/// <param name="hashAlgorithm">The method's hash.</param>
/// <param name="keySource">Where the client's ephemeral key pair comes from.</param>
internal sealed class FiniteFieldSshKeyExchange(
    FiniteFieldDiffieHellmanGroup group,
    HashAlgorithmName hashAlgorithm,
    ISshEphemeralKeySource keySource) : ISshKeyExchange
{
    /// <inheritdoc />
    public async ValueTask<SshKeyExchangeOutcome> ExchangeAsync(
        SshKeyExchangeMessages messages,
        SshNegotiatedHandshake handshake,
        CancellationToken cancellationToken)
    {
        using FiniteFieldDiffieHellman clientKey = keySource.CreateFiniteFieldKey(group);
        SshDiffieHellmanRound round = await SshDiffieHellmanRound.RunAsync(
            messages,
            clientKey,
            SshMessageNumber.KeyExchangeDiffieHellmanInit,
            SshMessageNumber.KeyExchangeDiffieHellmanReply,
            cancellationToken).ConfigureAwait(false);
        return round.Finish(new SshExchangeHashInput(handshake, round.HostKey), hashAlgorithm);
    }
}
