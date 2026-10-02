namespace Curl.Http3;

/// <summary>
/// The setting identifiers of RFC 9114 section 7.2.4.1 and RFC 9204 section 5.
/// </summary>
public static class Http3SettingIdentifier
{
    /// <summary><c>SETTINGS_QPACK_MAX_TABLE_CAPACITY</c> (<c>0x01</c>), default 0.</summary>
    public const long QpackMaximumTableCapacity = 0x01;

    /// <summary><c>SETTINGS_MAX_FIELD_SECTION_SIZE</c> (<c>0x06</c>), default unlimited.</summary>
    public const long MaximumFieldSectionSize = 0x06;

    /// <summary><c>SETTINGS_QPACK_BLOCKED_STREAMS</c> (<c>0x07</c>), default 0.</summary>
    public const long QpackBlockedStreams = 0x07;

    /// <summary>
    /// <c>SETTINGS_ENABLE_CONNECT_PROTOCOL</c> (<c>0x08</c>, RFC 8441 section 3 applied to HTTP/3
    /// by RFC 9220 section 3), default 0; any value but 0 or 1 is <see cref="Http3ErrorCode.SettingsError" />.
    /// </summary>
    public const long EnableConnectProtocol = 0x08;

    /// <summary>
    /// <c>SETTINGS_H3_DATAGRAM</c> (<c>0x33</c>, RFC 9297 section 2.1.1), default 0; any value
    /// but 0 or 1 is <see cref="Http3ErrorCode.SettingsError" />.
    /// </summary>
    public const long H3Datagram = 0x33;

    /// <summary>
    /// Gets whether <paramref name="identifier" /> is one of the HTTP/2 settings RFC 9114
    /// section 7.2.4.1 forbids in HTTP/3: <c>0x02</c>, <c>0x03</c>, <c>0x04</c> and <c>0x05</c>.
    /// </summary>
    /// <param name="identifier">A setting identifier.</param>
    /// <returns><see langword="true" /> when receiving it is <see cref="Http3ErrorCode.SettingsError" />.</returns>
    public static bool IsReservedHttp2Setting(long identifier) => identifier is >= 0x02 and <= 0x05;

    /// <summary>
    /// Gets whether <paramref name="identifier" /> is a setting whose only legal values are 0
    /// and 1: <see cref="EnableConnectProtocol" /> and <see cref="H3Datagram" />.
    /// </summary>
    /// <param name="identifier">A setting identifier.</param>
    /// <returns><see langword="true" /> when receiving it with another value is <see cref="Http3ErrorCode.SettingsError" />.</returns>
    public static bool IsZeroOrOneSetting(long identifier) => identifier is EnableConnectProtocol or H3Datagram;
}
