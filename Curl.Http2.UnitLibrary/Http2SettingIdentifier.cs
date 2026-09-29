namespace Curl.Http2;

/// <summary>
/// The SETTINGS parameters of RFC 9113 section 6.5.2, by their wire identifier. An unknown
/// identifier keeps its raw number and is ignored.
/// </summary>
public enum Http2SettingIdentifier : ushort
{
    /// <summary>SETTINGS_HEADER_TABLE_SIZE: the largest HPACK dynamic table the sender's decoder allows.</summary>
    HeaderTableSize = 0x1,

    /// <summary>SETTINGS_ENABLE_PUSH: 0 refuses server push, 1 allows it.</summary>
    EnablePush = 0x2,

    /// <summary>SETTINGS_MAX_CONCURRENT_STREAMS: how many streams the sender lets the peer open.</summary>
    MaxConcurrentStreams = 0x3,

    /// <summary>SETTINGS_INITIAL_WINDOW_SIZE: the sender's initial stream receive window.</summary>
    InitialWindowSize = 0x4,

    /// <summary>SETTINGS_MAX_FRAME_SIZE: the largest frame payload the sender accepts.</summary>
    MaxFrameSize = 0x5,

    /// <summary>SETTINGS_MAX_HEADER_LIST_SIZE: advisory limit on a header list's size.</summary>
    MaxHeaderListSize = 0x6,
}
