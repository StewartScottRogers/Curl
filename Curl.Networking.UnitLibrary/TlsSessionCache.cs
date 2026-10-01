using System.Security.Cryptography;
using System.Text;

using Curl.Tls;

namespace Curl.Networking;

/// <summary>
/// The run's TLS session cache behind <c>--ssl-sessions</c> (ADR-0319), kept as curl 8.21.0's
/// <c>lib/vtls/vtls_scache.c</c> keeps it: sessions per peer, at most
/// <see cref="MaximumSessionsPerPeer" /> each, a TLS 1.3 session taken out when offered, and
/// the file read before the transfers and written after them in curl's format.
/// </summary>
/// <remarks>
/// <para>
/// The file (<c>src/tool_ssls.c</c>): two <c>#</c> comment lines, then one line per session,
/// <c>base64(salt ‖ hmac):base64(packed session)</c>. The salt is 32 random bytes and the
/// hmac is HMAC-SHA256 under that salt of the peer key, so the host names cannot be read back;
/// the packed session is <see cref="TlsSessionPacking" />'s, holding OpenSSL's
/// <c>SSL_SESSION</c> DER encoding (<see cref="TlsSessionCodec" />). A blank line or one whose
/// first non-blank is <c>#</c> is skipped; the others are numbered from 1 for the warnings.
/// </para>
/// <para>
/// The peer key is <see cref="PeerKey" />'s. curl's keys also name its TLS library and version
/// (<c>IMPL-OpenSSL/3.0.13</c>), so a line one tool writes never matches a peer of the other;
/// it is kept, unused, and written back, as curl keeps a line whose peer it never meets.
/// </para>
/// </remarks>
public sealed class TlsSessionCache
{
    /// <summary>The most sessions kept for one peer, as curl's cache is created with.</summary>
    public const int MaximumSessionsPerPeer = 2;

    private const int SaltLength = 32;

    private const ushort Tls13ProtocolId = 0x0304;

    private readonly TimeProvider _timeProvider;

    private readonly Func<byte[]> _newSalt;

    private readonly List<Peer> _peers = [];

    private readonly List<(string PeerKey, Func<IReadOnlyList<TlsSessionRecord>> Received)> _tracked = [];

    /// <summary>Creates an empty cache whose salts are random.</summary>
    /// <param name="timeProvider">The clock sessions expire on.</param>
    public TlsSessionCache(TimeProvider timeProvider)
        : this(timeProvider, NewRandomSalt)
    {
    }

    /// <summary>Returns a fresh random 32-byte salt, as curl salts each peer's key hash.</summary>
    /// <returns>The salt.</returns>
    internal static byte[] NewRandomSalt() => RandomNumberGenerator.GetBytes(SaltLength);

    /// <summary>Creates an empty cache with the given salt source, so a test can pin the file bytes.</summary>
    /// <param name="timeProvider">The clock sessions expire on.</param>
    /// <param name="newSalt">Returns a fresh 32-byte salt for each peer written to the file.</param>
    internal TlsSessionCache(TimeProvider timeProvider, Func<byte[]> newSalt)
    {
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _newSalt = newSalt;
    }

    /// <summary>
    /// Returns curl's peer key for a TLS connection: <c>host:port</c>, then
    /// <c>:NO-VRFY-PEER</c> and <c>:NO-VRFY-HOST</c> under <c>-k</c>, <c>:VRFY-STATUS</c> under
    /// <c>--cert-status</c>, <c>:CA-</c> and the full path of a <c>--cacert</c> file when the peer
    /// is verified, and <c>:IMPL-Curl:G</c>.
    /// </summary>
    /// <param name="host">The host name the connection was made to.</param>
    /// <param name="port">The port it was made to.</param>
    /// <param name="options">The connection's TLS options.</param>
    /// <returns>The key.</returns>
    public static string PeerKey(string host, int port, TlsClientOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        StringBuilder key = new($"{host}:{port}");
        key.Append(options.Insecure ? ":NO-VRFY-PEER:NO-VRFY-HOST" : string.Empty);
        key.Append(options.RequireCertificateStatus ? ":VRFY-STATUS" : string.Empty);
        key.Append(!options.Insecure && options.CaCertificateFile is { } caFile ? $":CA-{Path.GetFullPath(caFile)}" : string.Empty);
        return key.Append(":IMPL-Curl:G").ToString();
    }

    /// <summary>
    /// Reads a session file's text into the cache as curl's <c>tool_ssls_load</c> does, and
    /// returns its warning lines: <c>Warning: unrecognized line N in SSL session file F</c> for
    /// a line without <c>:</c>, <c>Warning: invalid shmax base64 encoding in line N</c> and
    /// <c>Warning: invalid sdata base64 encoding in line N: TEXT</c> for bad base64, and
    /// <c>Warning: import of session from line N rejected(C)</c> with curl's code 26 for a
    /// session that does not unpack and 43 for a salt and hmac that are not 64 bytes.
    /// </summary>
    /// <param name="text">The file's text.</param>
    /// <param name="fileName">The file name, as the warnings print it.</param>
    /// <returns>The warning lines, without line ends.</returns>
    public IReadOnlyList<string> Import(string text, string fileName)
    {
        ArgumentNullException.ThrowIfNull(text);
        List<string> warnings = [];
        int lineNumber = 0;
        foreach (string line in text.Split('\n').Where(IsSessionLine))
        {
            lineNumber++;
            if (ImportLine(line.TrimEnd('\r'), lineNumber, fileName) is { } warning)
            {
                warnings.Add(warning);
            }
        }

        return warnings;
    }

    /// <summary>
    /// Takes the newest unexpired TLS 1.3 session for <paramref name="peerKey" /> out of the
    /// cache, as curl takes one to offer; a session whose bytes do not decode as a TLS 1.3
    /// ticket is dropped, as the OpenSSL build drops one it cannot read.
    /// </summary>
    /// <param name="peerKey">The connection's <see cref="PeerKey" />.</param>
    /// <returns>The session, or <see langword="null" /> when none is held.</returns>
    public TlsSessionRecord? Take(string peerKey)
    {
        if (FindPeer(peerKey) is not { } peer)
        {
            return null;
        }

        RemoveExpired(peer);
        while (peer.Sessions.Count > 0)
        {
            PackedTlsSession newest = peer.Sessions[^1];
            peer.Sessions.RemoveAt(peer.Sessions.Count - 1);
            if (newest.ProtocolId == Tls13ProtocolId && TlsSessionCodec.Decode(newest.SessionData) is { } session)
            {
                return session;
            }
        }

        return null;
    }

    /// <summary>
    /// Remembers where a connection's session tickets arrive, so the ones it receives up to
    /// the end of the run go into the cache when it is exported.
    /// </summary>
    /// <param name="peerKey">The connection's <see cref="PeerKey" />.</param>
    /// <param name="receivedSessions">Returns the sessions the connection has received so far.</param>
    public void Track(string peerKey, Func<IReadOnlyList<TlsSessionRecord>> receivedSessions) =>
        _tracked.Add((peerKey, receivedSessions));

    /// <summary>
    /// Returns the cache as a session file's text, as curl's <c>tool_ssls_save</c> writes it:
    /// empty when it holds no session, otherwise the two comment lines and one line per
    /// unexpired session, each line ended with <c>\n</c>.
    /// </summary>
    /// <returns>The file's text.</returns>
    public string Export()
    {
        foreach (var (peerKey, received) in _tracked)
        {
            foreach (TlsSessionRecord session in received())
            {
                Add(PeerFor(peerKey), Pack(session));
            }
        }

        _tracked.Clear();
        StringBuilder file = new();
        foreach (Peer peer in _peers)
        {
            RemoveExpired(peer);
            foreach (PackedTlsSession session in peer.Sessions)
            {
                file.Append(file.Length == 0 ? "# Your SSL session cache. https://curl.se/docs/ssl-sessions.html\n# This file was generated by libcurl! Edit at your own risk.\n" : string.Empty);
                file.Append(Convert.ToBase64String(peer.SaltAndHmac(_newSalt))).Append(':')
                    .Append(Convert.ToBase64String(TlsSessionPacking.Pack(session))).Append('\n');
            }
        }

        return file.ToString();
    }

    private static bool IsSessionLine(string line)
    {
        string content = line.TrimStart(' ', '\t').TrimEnd('\r');
        return content.Length > 0 && content[0] != '#';
    }

    // curl's line order: the colon, the salt and hmac's base64, the session's base64, the import.
    private string? ImportLine(string line, int lineNumber, string fileName)
    {
        int colon = line.IndexOf(':', StringComparison.Ordinal);
        if (colon < 0)
        {
            return $"Warning: unrecognized line {lineNumber} in SSL session file {fileName}";
        }

        if (StrictBase64(line[..colon]) is not { } saltAndHmac)
        {
            return $"Warning: invalid shmax base64 encoding in line {lineNumber}";
        }

        string sessionText = line[(colon + 1)..];
        if (StrictBase64(sessionText) is not { } packed)
        {
            return $"Warning: invalid sdata base64 encoding in line {lineNumber}: {sessionText}";
        }

        if (TlsSessionPacking.Unpack(packed) is not { } session)
        {
            return $"Warning: import of session from line {lineNumber} rejected(26)";
        }

        if (saltAndHmac.Length != SaltLength * 2)
        {
            return $"Warning: import of session from line {lineNumber} rejected(43)";
        }

        Add(PeerForSaltAndHmac(saltAndHmac), session);
        return null;
    }

    // curl's decoder: a non-empty multiple of four characters from the base64 alphabet, with
    // at most two '=' and only at the end; .NET's would also skip white space.
    private static byte[]? StrictBase64(string text)
    {
        string unpadded = text.TrimEnd('=');
        bool valid = text.Length > 0
            && text.Length % 4 == 0
            && text.Length - unpadded.Length <= 2
            && unpadded.All(c => char.IsAsciiLetterOrDigit(c) || c is '+' or '/');
        return valid ? Convert.FromBase64String(text) : null;
    }

    private PackedTlsSession Pack(TlsSessionRecord session) =>
        new(
            TlsSessionCodec.Encode(session),
            session.Version,
            session.ReceivedAt.ToUnixTimeSeconds() + session.TicketLifetime,
            session.ApplicationProtocol,
            session.MaxEarlyDataSize,
            null);

    // curl's cf_scache_peer_add_session: a session not from TLS 1.3 replaces the others; a TLS
    // 1.3 one drops the expired and the older versions' and the oldest beyond the limit.
    private void Add(Peer peer, PackedTlsSession session)
    {
        if (session.ProtocolId != Tls13ProtocolId)
        {
            peer.Sessions.Clear();
        }
        else
        {
            RemoveExpired(peer);
            peer.Sessions.RemoveAll(held => held.ProtocolId != Tls13ProtocolId);
        }

        peer.Sessions.Add(session);
        if (peer.Sessions.Count > MaximumSessionsPerPeer)
        {
            peer.Sessions.RemoveAt(0);
        }
    }

    private void RemoveExpired(Peer peer)
    {
        long now = _timeProvider.GetUtcNow().ToUnixTimeSeconds();
        peer.Sessions.RemoveAll(session => session.ValidUntil < now);
    }

    private Peer PeerFor(string peerKey)
    {
        if (FindPeer(peerKey) is { } peer)
        {
            return peer;
        }

        Peer added = new() { Key = peerKey };
        _peers.Add(added);
        return added;
    }

    // A peer read from the file is known by its salt and hmac until a connection's key matches it.
    private Peer? FindPeer(string peerKey)
    {
        Peer? peer = _peers.Find(held => held.Key == peerKey)
            ?? _peers.Find(held => held.Key is null && held.Matches(peerKey));
        peer?.Key = peerKey;
        return peer;
    }

    private Peer PeerForSaltAndHmac(byte[] saltAndHmac)
    {
        Peer? peer = _peers.Find(held => held.SaltAndHmacValue.AsSpan().SequenceEqual(saltAndHmac));
        if (peer is null)
        {
            peer = new Peer { SaltAndHmacValue = saltAndHmac };
            _peers.Add(peer);
        }

        return peer;
    }

    private sealed class Peer
    {
        internal string? Key { get; set; }

        internal byte[]? SaltAndHmacValue { get; set; }

        internal List<PackedTlsSession> Sessions { get; } = [];

        internal bool Matches(string peerKey) =>
            CryptographicOperations.FixedTimeEquals(Hmac(SaltAndHmacValue.AsSpan(0, SaltLength).ToArray(), peerKey), SaltAndHmacValue.AsSpan(SaltLength));

        // The salt and hmac the file names this peer by: those it was read with, or new ones.
        internal byte[] SaltAndHmac(Func<byte[]> newSalt)
        {
            if (SaltAndHmacValue is null)
            {
                byte[] salt = newSalt();
                SaltAndHmacValue = [.. salt, .. Hmac(salt, Key!)];
            }

            return SaltAndHmacValue;
        }

        private static byte[] Hmac(byte[] salt, string peerKey) => HMACSHA256.HashData(salt, Encoding.ASCII.GetBytes(peerKey));
    }
}
