using Curl.Http3;
using Curl.HttpDatagram;

namespace Curl.Extended.Protocol.WebTransport.Wire;

/// <summary>
/// The codepoints of WebTransport over HTTP/3 (draft-ietf-webtrans-http3-16, ADR-1089) and the
/// stream headers it puts in front of a session's stream data.
/// </summary>
internal static class WebTransportWire
{
    /// <summary>The extended CONNECT <c>:protocol</c> token (section 3.2).</summary>
    public const string ProtocolToken = "webtransport-h3";

    /// <summary><c>SETTINGS_WT_ENABLED</c> (sections 3.1 and 9.2); 1 offers WebTransport.</summary>
    public const long EnabledSetting = 0x2c7cf000;

    /// <summary>The unidirectional stream type of a WebTransport stream (section 4.2).</summary>
    public const long UnidirectionalStreamType = 0x54;

    /// <summary>The signal value that opens a WebTransport bidirectional stream (section 4.3).</summary>
    public const long BidirectionalSignal = 0x41;

    /// <summary>The <c>WT_CLOSE_SESSION</c> capsule type (section 6).</summary>
    public const ulong CloseSessionCapsuleType = 0x2843;

    /// <summary><c>WT_BUFFERED_STREAM_REJECTED</c> (section 9.5), which stops a stream of another session.</summary>
    public const long BufferedStreamRejected = 0x3994bd84;

    /// <summary>The longest application error message a <c>WT_CLOSE_SESSION</c> capsule carries (section 6).</summary>
    public const int MaximumCloseMessageLength = 1024;

    /// <summary>
    /// Gets the client's SETTINGS: curl's own, <c>SETTINGS_H3_DATAGRAM</c> 1 (RFC 9297 section 2.1.1)
    /// and <c>SETTINGS_WT_ENABLED</c> 1 (section 3.1).
    /// </summary>
    public static IReadOnlyList<Http3Setting> ClientSettings { get; } =
    [
        .. Http3LocalUnidirectionalStreams.CurlSettings,
        new(Http3SettingIdentifier.H3Datagram, 1),
        new(EnabledSetting, 1),
    ];

    /// <summary>Writes the header of a stream of the session <paramref name="sessionId" />: its type or signal, then the session ID (sections 4.2 and 4.3).</summary>
    /// <param name="typeOrSignal"><see cref="UnidirectionalStreamType" /> or <see cref="BidirectionalSignal" />.</param>
    /// <param name="sessionId">The session's CONNECT stream ID.</param>
    /// <param name="data">The stream data that follows the header.</param>
    /// <returns>The header followed by <paramref name="data" />.</returns>
    public static byte[] StreamHeaderAndData(long typeOrSignal, long sessionId, ReadOnlySpan<byte> data)
    {
        int typeLength = HttpDatagramVariableLengthInteger.GetEncodedLength((ulong)typeOrSignal);
        int headerLength = typeLength + HttpDatagramVariableLengthInteger.GetEncodedLength((ulong)sessionId);
        byte[] bytes = new byte[headerLength + data.Length];
        HttpDatagramVariableLengthInteger.Write((ulong)typeOrSignal, bytes);
        HttpDatagramVariableLengthInteger.Write((ulong)sessionId, bytes.AsSpan(typeLength));
        data.CopyTo(bytes.AsSpan(headerLength));
        return bytes;
    }

    /// <summary>
    /// Reports whether the peer's SETTINGS allow a WebTransport session: <c>SETTINGS_WT_ENABLED</c>,
    /// <c>SETTINGS_ENABLE_CONNECT_PROTOCOL</c> and <c>SETTINGS_H3_DATAGRAM</c> all 1 (section 3.1).
    /// </summary>
    /// <param name="settings">The peer's SETTINGS.</param>
    /// <returns>Whether a session may be opened.</returns>
    public static bool AllowsSessions(Http3SettingsFrame settings) =>
        settings.GetValueOrDefault(EnabledSetting, 0) == 1
        && settings.GetValueOrDefault(Http3SettingIdentifier.EnableConnectProtocol, 0) == 1
        && settings.GetValueOrDefault(Http3SettingIdentifier.H3Datagram, 0) == 1;
}
