namespace Curl.Protocol.Telnet;

/// <summary>
/// The telnet command and option bytes this library reads or writes: RFC 854 for the
/// commands, RFC 855 for negotiation, and the option numbers curl 8.21.0 acts on.
/// </summary>
internal static class TelnetByte
{
    /// <summary>Interpret As Command: every command sequence starts with it.</summary>
    public const byte InterpretAsCommand = 0xFF;

    /// <summary>Refuses, or confirms the refusal of, an option the peer performs.</summary>
    public const byte Dont = 0xFE;

    /// <summary>Asks, or agrees, that the peer perform an option.</summary>
    public const byte Do = 0xFD;

    /// <summary>Refuses, or confirms the refusal of, an option this side performs.</summary>
    public const byte Wont = 0xFC;

    /// <summary>Offers, or agrees, that this side perform an option.</summary>
    public const byte Will = 0xFB;

    /// <summary>Starts a subnegotiation.</summary>
    public const byte SubnegotiationBegin = 0xFA;

    /// <summary>Ends a subnegotiation.</summary>
    public const byte SubnegotiationEnd = 0xF0;

    /// <summary>The carriage return a following NUL is dropped after.</summary>
    public const byte CarriageReturn = 0x0D;

    /// <summary>The NUL dropped after a carriage return.</summary>
    public const byte Nul = 0x00;

    /// <summary>Option 0, binary transmission (RFC 856).</summary>
    public const byte BinaryOption = 0;

    /// <summary>Option 1, echo (RFC 857).</summary>
    public const byte EchoOption = 1;

    /// <summary>Option 3, suppress go-ahead (RFC 858).</summary>
    public const byte SuppressGoAheadOption = 3;

    /// <summary>Option 24, terminal type (RFC 1091).</summary>
    public const byte TerminalTypeOption = 24;

    /// <summary>Option 31, negotiate about window size, NAWS (RFC 1073).</summary>
    public const byte WindowSizeOption = 31;

    /// <summary>Option 35, X display location (RFC 1096).</summary>
    public const byte XDisplayLocationOption = 35;

    /// <summary>Option 39, new environment (RFC 1572).</summary>
    public const byte NewEnvironmentOption = 39;

    /// <summary>The subnegotiation qualifier that carries a value: <c>IS</c>.</summary>
    public const byte IsQualifier = 0;

    /// <summary>Starts a variable's name in a <c>NEW-ENVIRON</c> list: <c>VAR</c> (RFC 1572).</summary>
    public const byte EnvironmentVariable = 0;

    /// <summary>Starts a variable's value in a <c>NEW-ENVIRON</c> list: <c>VALUE</c> (RFC 1572).</summary>
    public const byte EnvironmentValue = 1;
}
