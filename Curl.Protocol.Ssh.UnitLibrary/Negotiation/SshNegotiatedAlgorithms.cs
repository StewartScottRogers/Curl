namespace Curl.Protocol.Ssh.Negotiation;

/// <summary>
/// The algorithms both sides agreed on (RFC 4253 section 7.1).
/// </summary>
/// <param name="KeyExchange">The key-exchange method.</param>
/// <param name="ServerHostKey">The host-key algorithm.</param>
/// <param name="CipherClientToServer">The cipher for client-to-server packets.</param>
/// <param name="CipherServerToClient">The cipher for server-to-client packets.</param>
/// <param name="MacClientToServer">
/// The MAC for client-to-server packets, or <see langword="null" /> when the cipher is an
/// AEAD cipher that authenticates its own packets.
/// </param>
/// <param name="MacServerToClient">The MAC for server-to-client packets, or <see langword="null" /> for an AEAD cipher.</param>
/// <param name="CompressionClientToServer">The compression method for client-to-server packets.</param>
/// <param name="CompressionServerToClient">The compression method for server-to-client packets.</param>
/// <param name="IsStrictKeyExchange">
/// Whether both sides signalled strict key exchange, so no message but the key exchange's
/// own may arrive before the first <c>NEWKEYS</c> and sequence numbers reset at each one.
/// </param>
/// <param name="DiscardServerGuess">
/// Whether the server sent a guessed key-exchange packet after its <c>KEXINIT</c> that
/// guessed wrong, so the next packet must be read and ignored (RFC 4253 section 7).
/// </param>
internal sealed record SshNegotiatedAlgorithms(
    string KeyExchange,
    string ServerHostKey,
    string CipherClientToServer,
    string CipherServerToClient,
    string? MacClientToServer,
    string? MacServerToClient,
    string CompressionClientToServer,
    string CompressionServerToClient,
    bool IsStrictKeyExchange,
    bool DiscardServerGuess);
