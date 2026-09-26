namespace Curl.Protocol.Telnet;

/// <summary>
/// The values the <c>-t</c>/<c>--telnet-option</c> options supplied, as
/// <see cref="TelnetOptionParser" /> read them: what this side answers a <c>TTYPE</c>,
/// <c>XDISPLOC</c> or <c>NEW-ENVIRON</c> subnegotiation with.
/// </summary>
internal sealed class TelnetOptionValues
{
    /// <summary>
    /// Gets or sets the terminal type <c>TTYPE=</c> gave, or <see langword="null" /> when
    /// none did.
    /// </summary>
    public string? TerminalType { get; set; }

    /// <summary>
    /// Gets or sets the X display location <c>XDISPLOC=</c> gave, or
    /// <see langword="null" /> when none did.
    /// </summary>
    public string? XDisplayLocation { get; set; }

    /// <summary>
    /// Gets each <c>NEW_ENV=</c> value in command-line order: <c>NAME,VALUE</c>, or
    /// <c>NAME</c> alone for a variable sent without a value.
    /// </summary>
    public List<string> EnvironmentVariables { get; } = [];
}
