namespace Curl.Protocol.Telnet;

/// <summary>
/// Where <see cref="TelnetReceiver" /> stands between two received bytes. A command
/// sequence split across reads resumes from here.
/// </summary>
internal enum TelnetReceiveState
{
    /// <summary>Plain data.</summary>
    Data,

    /// <summary>Plain data, just after a carriage return.</summary>
    AfterCarriageReturn,

    /// <summary>Just after <c>IAC</c>.</summary>
    Command,

    /// <summary>Just after <c>IAC WILL</c>.</summary>
    Will,

    /// <summary>Just after <c>IAC WONT</c>.</summary>
    Wont,

    /// <summary>Just after <c>IAC DO</c>.</summary>
    Do,

    /// <summary>Just after <c>IAC DONT</c>.</summary>
    Dont,

    /// <summary>Inside <c>IAC SB</c> ... <c>IAC SE</c>.</summary>
    Subnegotiation,

    /// <summary>Just after an <c>IAC</c> inside a subnegotiation.</summary>
    SubnegotiationCommand,
}
