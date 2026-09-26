namespace Curl.Protocol.Telnet;

/// <summary>
/// Where one side of one option stands in RFC 1143's Q method, as curl 8.21.0 tracks it.
/// </summary>
/// <remarks>
/// RFC 1143 also has <c>WANTNO</c> and a queue bit. Both arise only when this side asks
/// to disable an option, and curl never does, so neither can be reached and neither is
/// modelled.
/// </remarks>
internal enum TelnetOptionState
{
    /// <summary>The option is disabled.</summary>
    No,

    /// <summary>The option is enabled.</summary>
    Yes,

    /// <summary>This side asked to enable the option and awaits the peer's answer.</summary>
    WantYes,
}
