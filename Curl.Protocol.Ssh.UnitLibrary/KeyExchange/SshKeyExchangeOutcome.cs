using System.Security.Cryptography;

namespace Curl.Protocol.Ssh.KeyExchange;

/// <summary>
/// What a key-exchange method produced, before the host key's signature is checked.
/// </summary>
/// <param name="HostKey">The server's host key blob, <c>K_S</c>.</param>
/// <param name="Signature">The server's signature blob over <paramref name="ExchangeHash" />.</param>
/// <param name="SharedSecret">The shared secret K, unsigned big-endian.</param>
/// <param name="ExchangeHash">The exchange hash H.</param>
/// <param name="HashAlgorithm">The method's hash, which also derives the keys.</param>
internal sealed record SshKeyExchangeOutcome(
    byte[] HostKey,
    byte[] Signature,
    byte[] SharedSecret,
    byte[] ExchangeHash,
    HashAlgorithmName HashAlgorithm);
