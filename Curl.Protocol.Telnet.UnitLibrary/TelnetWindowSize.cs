namespace Curl.Protocol.Telnet;

/// <summary>
/// The window size a <c>-t WS=COLUMNSxROWS</c> option gave, sent to the server in a NAWS
/// subnegotiation (RFC 1073).
/// </summary>
/// <param name="columns">The width in characters.</param>
/// <param name="rows">The height in lines.</param>
internal readonly struct TelnetWindowSize(ushort columns, ushort rows)
{
    /// <summary>Gets the width in characters.</summary>
    public ushort Columns { get; } = columns;

    /// <summary>Gets the height in lines.</summary>
    public ushort Rows { get; } = rows;
}
