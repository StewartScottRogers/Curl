namespace Curl.Http2;

/// <summary>
/// One endpoint's SETTINGS parameters, starting at RFC 9113 section 6.5.2's initial values
/// and changed by each SETTINGS frame it sends. An unknown parameter is ignored.
/// </summary>
public sealed class Http2Settings
{
    /// <summary>SETTINGS_INITIAL_WINDOW_SIZE's initial value, and every window's starting size (RFC 9113 section 6.9.2).</summary>
    public const int DefaultInitialWindowSize = 65535;

    /// <summary>Gets SETTINGS_HEADER_TABLE_SIZE. Initially 4096.</summary>
    public uint HeaderTableSize { get; private set; } = HpackDecoder.DefaultMaximumTableSize;

    /// <summary>Gets whether SETTINGS_ENABLE_PUSH allows server push. Initially <see langword="true" />.</summary>
    public bool IsPushEnabled { get; private set; } = true;

    /// <summary>Gets SETTINGS_MAX_CONCURRENT_STREAMS, or <see langword="null" /> while unlimited, as it is initially.</summary>
    public uint? MaxConcurrentStreams { get; private set; }

    /// <summary>Gets SETTINGS_INITIAL_WINDOW_SIZE. Initially 65535.</summary>
    public int InitialWindowSize { get; private set; } = DefaultInitialWindowSize;

    /// <summary>Gets SETTINGS_MAX_FRAME_SIZE. Initially 16384.</summary>
    public int MaxFrameSize { get; private set; } = Http2FrameCodec.DefaultMaximumFrameSize;

    /// <summary>Gets SETTINGS_MAX_HEADER_LIST_SIZE, or <see langword="null" /> while unlimited, as it is initially.</summary>
    public uint? MaxHeaderListSize { get; private set; }

    /// <summary>
    /// Applies one parameter, checking its value against RFC 9113 section 6.5.2.
    /// </summary>
    /// <param name="setting">The parameter.</param>
    /// <exception cref="Http2ProtocolException">
    /// ENABLE_PUSH is neither 0 nor 1, or MAX_FRAME_SIZE is outside 16384 to 2^24 - 1
    /// (PROTOCOL_ERROR); INITIAL_WINDOW_SIZE is above 2^31 - 1 (FLOW_CONTROL_ERROR).
    /// </exception>
    public void Apply(Http2Setting setting)
    {
        switch (setting.Identifier)
        {
            case Http2SettingIdentifier.HeaderTableSize:
                HeaderTableSize = setting.Value;
                break;
            case Http2SettingIdentifier.EnablePush:
                IsPushEnabled = ReadEnablePush(setting.Value);
                break;
            case Http2SettingIdentifier.MaxConcurrentStreams:
                MaxConcurrentStreams = setting.Value;
                break;
            case Http2SettingIdentifier.InitialWindowSize:
                InitialWindowSize = ReadInitialWindowSize(setting.Value);
                break;
            case Http2SettingIdentifier.MaxFrameSize:
                MaxFrameSize = ReadMaxFrameSize(setting.Value);
                break;
            case Http2SettingIdentifier.MaxHeaderListSize:
                MaxHeaderListSize = setting.Value;
                break;
        }
    }

    private static bool ReadEnablePush(uint value) => value switch
    {
        0 => false,
        1 => true,
        _ => throw new Http2ProtocolException(Http2ErrorCode.ProtocolError, $"SETTINGS_ENABLE_PUSH of {value}"),
    };

    private static int ReadInitialWindowSize(uint value) => value <= int.MaxValue
        ? (int)value
        : throw new Http2ProtocolException(Http2ErrorCode.FlowControlError, $"SETTINGS_INITIAL_WINDOW_SIZE of {value}");

    private static int ReadMaxFrameSize(uint value) => value is >= Http2FrameCodec.DefaultMaximumFrameSize and <= Http2FrameCodec.LargestMaximumFrameSize
        ? (int)value
        : throw new Http2ProtocolException(Http2ErrorCode.ProtocolError, $"SETTINGS_MAX_FRAME_SIZE of {value}");
}
