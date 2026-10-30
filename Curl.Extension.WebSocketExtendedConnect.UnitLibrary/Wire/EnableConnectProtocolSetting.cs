using Curl.Http2;
using Curl.Http3;

namespace Curl.Extension.WebSocketExtendedConnect.Wire;

/// <summary>
/// Reads SETTINGS_ENABLE_CONNECT_PROTOCOL from a server's SETTINGS: identifier 0x8 on HTTP/2 (RFC 8441 section 3)
/// and 0x08 on HTTP/3 (RFC 9220 section 3). Only the value 1 allows extended CONNECT; an absent setting means 0.
/// </summary>
internal static class EnableConnectProtocolSetting
{
    /// <summary>The setting's identifier on HTTP/2 (RFC 8441 section 3).</summary>
    internal const ushort Http2Identifier = 0x8;

    /// <summary>The setting's identifier on HTTP/3 (RFC 9220 section 3).</summary>
    internal const long Http3Identifier = 0x08;

    /// <summary>Gives whether an HTTP/2 SETTINGS frame's settings allow extended CONNECT.</summary>
    /// <param name="settings">The settings the server sent; when one repeats, the last one counts (RFC 9113 section 6.5.3).</param>
    /// <returns><see langword="true" /> when the last SETTINGS_ENABLE_CONNECT_PROTOCOL is 1.</returns>
    internal static bool IsEnabled(IReadOnlyList<Http2Setting> settings) =>
        settings.LastOrDefault(setting => (ushort)setting.Identifier == Http2Identifier).Value == 1;

    /// <summary>Gives whether an HTTP/3 SETTINGS frame's settings allow extended CONNECT.</summary>
    /// <param name="settings">The settings the server sent.</param>
    /// <returns><see langword="true" /> when SETTINGS_ENABLE_CONNECT_PROTOCOL is 1.</returns>
    internal static bool IsEnabled(IReadOnlyList<Http3Setting> settings) =>
        settings.LastOrDefault(setting => setting.Identifier == Http3Identifier).Value == 1;
}
