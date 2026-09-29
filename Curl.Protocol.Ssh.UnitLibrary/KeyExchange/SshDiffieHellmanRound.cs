using Curl.Cryptography;
using Curl.Protocol.Ssh.Transport;

namespace Curl.Protocol.Ssh.KeyExchange;

/// <summary>
/// The finite-field round both <c>diffie-hellman-group*</c> (RFC 4253 section 8) and
/// <c>diffie-hellman-group-exchange-*</c> (RFC 4419 section 3) end with: the client sends
/// e, the server answers with its host key, f and a signature, and K = f^x mod p.
/// </summary>
/// <param name="HostKey">The server's host key blob, <c>K_S</c>.</param>
/// <param name="ClientPublicValue">e, unsigned big-endian.</param>
/// <param name="ServerPublicValue">f, unsigned big-endian.</param>
/// <param name="Signature">The server's signature blob.</param>
/// <param name="SharedSecret">K, unsigned big-endian.</param>
internal sealed record SshDiffieHellmanRound(
    byte[] HostKey,
    byte[] ClientPublicValue,
    byte[] ServerPublicValue,
    byte[] Signature,
    byte[] SharedSecret)
{
    /// <summary>
    /// Sends e in <paramref name="initNumber" />, reads the reply
    /// <paramref name="replyNumber" /> and computes K.
    /// </summary>
    /// <param name="messages">The key exchange's message channel.</param>
    /// <param name="clientKey">The client's key pair.</param>
    /// <param name="initNumber">The message that carries e.</param>
    /// <param name="replyNumber">The message that answers it.</param>
    /// <param name="cancellationToken">Cancels the round.</param>
    /// <returns>The round's values.</returns>
    /// <exception cref="InvalidDataException">The reply is malformed, or f is not in 1 &lt; f &lt; p - 1.</exception>
    internal static async ValueTask<SshDiffieHellmanRound> RunAsync(
        SshKeyExchangeMessages messages,
        FiniteFieldDiffieHellman clientKey,
        byte initNumber,
        byte replyNumber,
        CancellationToken cancellationToken)
    {
        byte[] clientPublicValue = new byte[clientKey.Group.PrimeLength];
        clientKey.ComputePublicValue(clientPublicValue);
        SshWireWriter init = new();
        init.WriteByte(initNumber);
        init.WriteMpint(clientPublicValue);
        await messages.SendAsync(init.ToArray(), cancellationToken).ConfigureAwait(false);

        SshWireReader reply = await messages.ReadAsync(replyNumber, cancellationToken).ConfigureAwait(false);
        byte[] hostKey = reply.ReadString().ToArray();
        byte[] serverPublicValue = reply.ReadMpint().ToArray();
        byte[] signature = reply.ReadString().ToArray();
        byte[] sharedSecret = new byte[clientKey.Group.PrimeLength];
        if (!clientKey.TryComputeSharedSecret(serverPublicValue, sharedSecret))
        {
            throw new InvalidDataException("The SSH server's Diffie-Hellman public value f is not in 1 < f < p - 1.");
        }

        return new SshDiffieHellmanRound(hostKey, clientPublicValue, serverPublicValue, signature, sharedSecret);
    }

    /// <summary>
    /// Appends e and f to the exchange hash's input and computes H.
    /// </summary>
    /// <param name="hashInput">The input so far, up to the method's own fields before e.</param>
    /// <param name="hashAlgorithm">The method's hash.</param>
    /// <returns>The method's outcome.</returns>
    internal SshKeyExchangeOutcome Finish(SshExchangeHashInput hashInput, System.Security.Cryptography.HashAlgorithmName hashAlgorithm)
    {
        hashInput.Fields.WriteMpint(ClientPublicValue);
        hashInput.Fields.WriteMpint(ServerPublicValue);
        return new SshKeyExchangeOutcome(HostKey, Signature, SharedSecret, hashInput.ComputeHash(SharedSecret, hashAlgorithm), hashAlgorithm);
    }
}
