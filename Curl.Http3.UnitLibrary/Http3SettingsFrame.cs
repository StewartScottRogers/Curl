namespace Curl.Http3;

/// <summary>
/// A <c>SETTINGS</c> frame (RFC 9114 section 7.2.4): the sender's configuration, the first
/// frame on each control stream.
/// </summary>
/// <param name="settings">The parameters, in the order they are written.</param>
public sealed class Http3SettingsFrame(IReadOnlyList<Http3Setting> settings) : Http3Frame
{
    /// <inheritdoc />
    public override Http3FrameType Type => Http3FrameType.Settings;

    /// <summary>
    /// Gets the parameters. A frame that was read leaves out the identifiers reserved for
    /// greasing.
    /// </summary>
    public IReadOnlyList<Http3Setting> Settings { get; } = settings ?? throw new ArgumentNullException(nameof(settings));

    /// <summary>
    /// Gets the value of the setting with <paramref name="identifier" />, or
    /// <paramref name="defaultValue" /> when the frame does not carry it.
    /// </summary>
    /// <param name="identifier">The setting identifier.</param>
    /// <param name="defaultValue">The setting's default (RFC 9114 section 7.2.4.1, RFC 9204 section 5).</param>
    /// <returns>The value.</returns>
    public long GetValueOrDefault(long identifier, long defaultValue)
    {
        foreach (var setting in Settings)
        {
            if (setting.Identifier == identifier)
            {
                return setting.Value;
            }
        }

        return defaultValue;
    }

    /// <summary>
    /// Interprets a <c>SETTINGS</c> payload, leaving out identifiers reserved for greasing.
    /// </summary>
    /// <param name="payload">The whole payload.</param>
    /// <returns>The frame.</returns>
    internal static Http3SettingsFrame ParsePayload(ReadOnlySpan<byte> payload)
    {
        List<Http3Setting> settings = [];
        HashSet<long> identifiers = [];
        var position = 0;
        while (position < payload.Length)
        {
            var identifier = ReadInteger(payload, ref position, Http3FrameType.Settings);
            var value = ReadInteger(payload, ref position, Http3FrameType.Settings);
            ThrowIfForbidden(identifier, identifiers);
            if (!Http3ReservedIdentifier.IsReserved(identifier))
            {
                settings.Add(new Http3Setting(identifier, value));
            }
        }

        return new Http3SettingsFrame(settings);
    }

    /// <inheritdoc />
    private protected override void WritePayload(List<byte> payload)
    {
        foreach (var setting in Settings)
        {
            Http3VariableLengthInteger.Write(payload, setting.Identifier);
            Http3VariableLengthInteger.Write(payload, setting.Value);
        }
    }

    private static void ThrowIfForbidden(long identifier, HashSet<long> identifiers)
    {
        if (Http3SettingIdentifier.IsReservedHttp2Setting(identifier))
        {
            throw new Http3Exception(Http3ErrorCode.SettingsError, $"setting 0x{identifier:x} is an HTTP/2 setting");
        }

        if (!identifiers.Add(identifier))
        {
            throw new Http3Exception(Http3ErrorCode.SettingsError, $"setting 0x{identifier:x} appears twice");
        }
    }
}
