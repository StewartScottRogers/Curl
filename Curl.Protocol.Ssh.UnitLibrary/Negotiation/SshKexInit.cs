using Curl.Protocol.Ssh.Transport;

namespace Curl.Protocol.Ssh.Negotiation;

/// <summary>
/// One side's <c>SSH_MSG_KEXINIT</c> (RFC 4253 section 7.1): its cookie and the ten
/// algorithm name-lists, in that side's order of preference.
/// </summary>
/// <param name="Cookie">The 16 random bytes that start the message.</param>
/// <param name="KeyExchange">The key-exchange methods, with any signal names such as <c>ext-info-c</c>.</param>
/// <param name="ServerHostKey">The host-key algorithms.</param>
/// <param name="CipherClientToServer">The ciphers for client-to-server packets.</param>
/// <param name="CipherServerToClient">The ciphers for server-to-client packets.</param>
/// <param name="MacClientToServer">The MACs for client-to-server packets.</param>
/// <param name="MacServerToClient">The MACs for server-to-client packets.</param>
/// <param name="CompressionClientToServer">The compression methods for client-to-server packets.</param>
/// <param name="CompressionServerToClient">The compression methods for server-to-client packets.</param>
/// <param name="LanguagesClientToServer">The language tags for client-to-server text.</param>
/// <param name="LanguagesServerToClient">The language tags for server-to-client text.</param>
/// <param name="FirstKexPacketFollows">Whether a guessed key-exchange packet follows at once.</param>
internal sealed record SshKexInit(
    byte[] Cookie,
    IReadOnlyList<string> KeyExchange,
    IReadOnlyList<string> ServerHostKey,
    IReadOnlyList<string> CipherClientToServer,
    IReadOnlyList<string> CipherServerToClient,
    IReadOnlyList<string> MacClientToServer,
    IReadOnlyList<string> MacServerToClient,
    IReadOnlyList<string> CompressionClientToServer,
    IReadOnlyList<string> CompressionServerToClient,
    IReadOnlyList<string> LanguagesClientToServer,
    IReadOnlyList<string> LanguagesServerToClient,
    bool FirstKexPacketFollows)
{
    /// <summary>The length of <see cref="Cookie" /> in bytes.</summary>
    internal const int CookieLength = 16;

    /// <summary>
    /// Builds the client's <c>KEXINIT</c> as curl sends it (ADR-0122): each list of
    /// <paramref name="preferences" /> in its order, keeping only the names
    /// <paramref name="catalogue" /> implements, the same lists in both directions, no
    /// languages and no guessed packet.
    /// </summary>
    /// <param name="preferences">The platform preset, after compression and known-hosts narrowing.</param>
    /// <param name="catalogue">The algorithms this build implements.</param>
    /// <param name="randomSource">Where the cookie comes from.</param>
    /// <returns>The message.</returns>
    internal static SshKexInit ForClient(
        SshAlgorithmPreferences preferences,
        SshAlgorithmCatalogue catalogue,
        ISshRandomSource randomSource)
    {
        byte[] cookie = new byte[CookieLength];
        randomSource.Fill(cookie);
        IReadOnlyList<string> ciphers = catalogue.KeepImplemented(preferences.Cipher);
        IReadOnlyList<string> macs = catalogue.KeepImplemented(preferences.Mac);
        IReadOnlyList<string> compressions = catalogue.KeepImplemented(preferences.Compression);
        return new SshKexInit(
            cookie,
            catalogue.KeepImplemented(preferences.KeyExchange),
            catalogue.KeepImplemented(preferences.ServerHostKey),
            ciphers,
            ciphers,
            macs,
            macs,
            compressions,
            compressions,
            [],
            [],
            FirstKexPacketFollows: false);
    }

    /// <summary>
    /// Reads a <c>KEXINIT</c> payload, message number first.
    /// </summary>
    /// <param name="payload">The packet payload.</param>
    /// <returns>The message.</returns>
    /// <exception cref="InvalidDataException">The payload ends early.</exception>
    internal static SshKexInit Parse(ReadOnlyMemory<byte> payload)
    {
        SshWireReader reader = new(payload);
        reader.ReadByte();
        return new SshKexInit(
            reader.ReadBytes(CookieLength).ToArray(),
            reader.ReadNameList(),
            reader.ReadNameList(),
            reader.ReadNameList(),
            reader.ReadNameList(),
            reader.ReadNameList(),
            reader.ReadNameList(),
            reader.ReadNameList(),
            reader.ReadNameList(),
            reader.ReadNameList(),
            reader.ReadNameList(),
            FirstKexPacketFollows: reader.ReadBoolean())
        {
            Reserved = reader.ReadUInt32(),
        };
    }

    /// <summary>
    /// Gets the reserved <c>uint32</c> that ends the message: 0 when written, ignored when read.
    /// </summary>
    internal uint Reserved { get; init; }

    /// <summary>
    /// Writes the message as a packet payload, message number first.
    /// </summary>
    /// <returns>The payload.</returns>
    internal byte[] ToPayload()
    {
        SshWireWriter writer = new();
        writer.WriteByte(SshMessageNumber.KeyExchangeInit);
        writer.WriteBytes(Cookie);
        foreach (IReadOnlyList<string> names in new[]
        {
            KeyExchange, ServerHostKey, CipherClientToServer, CipherServerToClient, MacClientToServer,
            MacServerToClient, CompressionClientToServer, CompressionServerToClient, LanguagesClientToServer,
            LanguagesServerToClient,
        })
        {
            writer.WriteNameList(names);
        }

        writer.WriteBoolean(FirstKexPacketFollows);
        writer.WriteUInt32(Reserved);
        return writer.ToArray();
    }
}
