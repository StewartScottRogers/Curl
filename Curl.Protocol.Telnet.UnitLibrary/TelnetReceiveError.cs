namespace Curl.Protocol.Telnet;

/// <summary>
/// Why received bytes ended the session, as <see cref="TelnetReceiver.Receive" /> reports it.
/// </summary>
internal enum TelnetReceiveError
{
    /// <summary>Nothing went wrong; the session continues.</summary>
    None,

    /// <summary>
    /// The server asked, by subnegotiation, for a terminal type or X display location that
    /// no <c>-t</c> option supplied: curl 8.21.0 exits 43.
    /// </summary>
    SubnegotiationValueMissing,

    /// <summary>
    /// The server asked for the terminal type and the one <c>-t</c> supplied is over 1000
    /// characters: curl 8.21.0 exits 55.
    /// </summary>
    TerminalTypeTooLong,

    /// <summary>
    /// The server asked for the X display location and the one <c>-t</c> supplied is over
    /// 1000 characters: curl 8.21.0 exits 55.
    /// </summary>
    XDisplayLocationTooLong,

    /// <summary>
    /// A subnegotiation contained <c>IAC</c> followed by something other than <c>SE</c> or
    /// <c>IAC</c>: curl 8.21.0 exits 56.
    /// </summary>
    MalformedSubnegotiation,
}
