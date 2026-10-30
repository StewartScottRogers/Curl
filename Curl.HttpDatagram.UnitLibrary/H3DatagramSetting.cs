namespace Curl.HttpDatagram;

/// <summary>
/// The <c>SETTINGS_H3_DATAGRAM</c> setting (RFC 9297 section 2.1.1): its wire form in an
/// HTTP/3 SETTINGS frame, and the rule that HTTP/3 datagrams flow only when both endpoints
/// sent it with the value 1.
/// </summary>
public static class H3DatagramSetting
{
    /// <summary>
    /// The setting's identifier, 0x33.
    /// </summary>
    public const ulong Identifier = 0x33;

    /// <summary>
    /// Encodes the setting as one SETTINGS frame entry: Identifier (i), Value (i).
    /// </summary>
    /// <param name="enabled">Whether this endpoint supports HTTP/3 datagrams (value 1) or not (value 0).</param>
    /// <returns>The entry's wire bytes.</returns>
    public static byte[] EncodeEntry(bool enabled)
    {
        return [(byte)Identifier, enabled ? (byte)1 : (byte)0];
    }

    /// <summary>
    /// Reads the peer's value for the setting.
    /// </summary>
    /// <param name="value">The value the peer sent, or <see langword="null" /> when it did not send the setting.</param>
    /// <returns>Whether the peer supports HTTP/3 datagrams; an absent setting counts as 0.</returns>
    /// <exception cref="HttpDatagramException">H3_SETTINGS_ERROR: a value other than 0 or 1.</exception>
    public static bool IsEnabledByPeer(ulong? value)
    {
        return value switch
        {
            null or 0 => false,
            1 => true,
            _ => throw new HttpDatagramException(HttpDatagramErrorCode.SettingsError, $"SETTINGS_H3_DATAGRAM has the value {value}, not 0 or 1"),
        };
    }

    /// <summary>
    /// Decides whether HTTP/3 datagrams may be sent and received on the connection.
    /// </summary>
    /// <param name="sentByThisEndpoint">Whether this endpoint sent the setting with the value 1.</param>
    /// <param name="peerValue">The value the peer sent, or <see langword="null" /> when it did not send the setting.</param>
    /// <returns><see langword="true" /> only when both endpoints sent the value 1.</returns>
    /// <exception cref="HttpDatagramException">H3_SETTINGS_ERROR: the peer's value is other than 0 or 1.</exception>
    public static bool Negotiate(bool sentByThisEndpoint, ulong? peerValue)
    {
        var peerEnabled = IsEnabledByPeer(peerValue);
        return sentByThisEndpoint && peerEnabled;
    }

    /// <summary>
    /// Checks a received HTTP/3 datagram is allowed on the connection.
    /// </summary>
    /// <param name="negotiated">The result of <see cref="Negotiate" />.</param>
    /// <exception cref="HttpDatagramException">H3_DATAGRAM_ERROR: datagrams were not negotiated.</exception>
    public static void EnsureDatagramAllowed(bool negotiated)
    {
        if (!negotiated)
        {
            throw new HttpDatagramException(HttpDatagramErrorCode.DatagramError, "a datagram arrived though SETTINGS_H3_DATAGRAM was not negotiated");
        }
    }
}
