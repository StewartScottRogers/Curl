namespace Curl.Protocol.Ssh.Negotiation;

/// <summary>
/// Picks each algorithm from the two <c>KEXINIT</c> messages as RFC 4253 section 7.1
/// says: the first name on the client's list that is also on the server's.
/// </summary>
internal static class SshAlgorithmNegotiator
{
    /// <summary>
    /// Agrees every algorithm the session needs.
    /// </summary>
    /// <param name="client">The client's <c>KEXINIT</c>.</param>
    /// <param name="server">The server's <c>KEXINIT</c>.</param>
    /// <returns>
    /// The agreed algorithms, or <see langword="null" /> when one list shares no name with
    /// its counterpart. Key-exchange signals are never chosen as a method, and the MAC
    /// lists are not consulted beside an AEAD cipher.
    /// </returns>
    internal static SshNegotiatedAlgorithms? Negotiate(SshKexInit client, SshKexInit server)
    {
        string? keyExchange = FirstShared(
            [.. client.KeyExchange.Where(name => !SshAlgorithmCatalogue.IsKeyExchangeSignal(name))],
            server.KeyExchange);
        string? hostKey = FirstShared(client.ServerHostKey, server.ServerHostKey);
        DirectionAlgorithms? outbound = NegotiateDirection(
            client.CipherClientToServer, server.CipherClientToServer,
            client.MacClientToServer, server.MacClientToServer,
            client.CompressionClientToServer, server.CompressionClientToServer);
        DirectionAlgorithms? inbound = NegotiateDirection(
            client.CipherServerToClient, server.CipherServerToClient,
            client.MacServerToClient, server.MacServerToClient,
            client.CompressionServerToClient, server.CompressionServerToClient);
        if (keyExchange is null || hostKey is null || outbound is null || inbound is null)
        {
            return null;
        }

        return new SshNegotiatedAlgorithms(
            keyExchange,
            hostKey,
            outbound.Cipher,
            inbound.Cipher,
            outbound.Mac,
            inbound.Mac,
            outbound.Compression,
            inbound.Compression,
            IsStrictKeyExchange(client, server),
            DiscardServerGuess(server, keyExchange, hostKey));
    }

    private static DirectionAlgorithms? NegotiateDirection(
        IReadOnlyList<string> clientCiphers,
        IReadOnlyList<string> serverCiphers,
        IReadOnlyList<string> clientMacs,
        IReadOnlyList<string> serverMacs,
        IReadOnlyList<string> clientCompressions,
        IReadOnlyList<string> serverCompressions)
    {
        string? cipher = FirstShared(clientCiphers, serverCiphers);
        string? compression = FirstShared(clientCompressions, serverCompressions);
        if (cipher is null || compression is null)
        {
            return null;
        }

        if (SshAlgorithmCatalogue.IsAuthenticatedEncryption(cipher))
        {
            return new DirectionAlgorithms(cipher, null, compression);
        }

        string? mac = FirstShared(clientMacs, serverMacs);
        return mac is null ? null : new DirectionAlgorithms(cipher, mac, compression);
    }

    private static bool IsStrictKeyExchange(SshKexInit client, SshKexInit server) =>
        client.KeyExchange.Contains(SshAlgorithmCatalogue.StrictKeyExchangeClient)
        && server.KeyExchange.Contains(SshAlgorithmCatalogue.StrictKeyExchangeServer);

    private static bool DiscardServerGuess(SshKexInit server, string keyExchange, string hostKey) =>
        server.FirstKexPacketFollows
        && (server.KeyExchange[0] != keyExchange || server.ServerHostKey[0] != hostKey);

    private static string? FirstShared(IReadOnlyList<string> client, IReadOnlyList<string> server) =>
        client.FirstOrDefault(server.Contains);

    private sealed record DirectionAlgorithms(string Cipher, string? Mac, string Compression);
}
