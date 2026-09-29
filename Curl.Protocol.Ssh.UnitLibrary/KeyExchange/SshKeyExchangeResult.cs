using Curl.Protocol.Ssh.Negotiation;

namespace Curl.Protocol.Ssh.KeyExchange;

/// <summary>
/// A finished key exchange: both sides have sent <c>NEWKEYS</c>, and the packet layer
/// takes its keys from <paramref name="Keys" /> for the ciphers and MACs in
/// <paramref name="Algorithms" />.
/// </summary>
/// <param name="Algorithms">The algorithms this exchange agreed.</param>
/// <param name="HostKey">The server's host key blob, <c>K_S</c>, whose signature over H verified; <c>SshHostKeyChecker</c> decides whether it is accepted.</param>
/// <param name="ExchangeHash">H of this exchange.</param>
/// <param name="SessionIdentifier">H of the session's first exchange, which never changes.</param>
/// <param name="Keys">The six keys' derivation.</param>
internal sealed record SshKeyExchangeResult(
    SshNegotiatedAlgorithms Algorithms,
    byte[] HostKey,
    byte[] ExchangeHash,
    byte[] SessionIdentifier,
    SshKeyDerivation Keys);
