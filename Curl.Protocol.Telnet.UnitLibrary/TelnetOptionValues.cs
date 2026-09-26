namespace Curl.Protocol.Telnet;

/// <summary>
/// The values the <c>-t</c>/<c>--telnet-option</c> options and the <c>-u</c> user name
/// supplied, as <see cref="TelnetOptionParser" /> read them: what this side answers a <c>TTYPE</c>,
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
    /// Gets <c>USER,</c> and the <c>-u</c> user name first when one was given, then each
    /// <c>NEW_ENV=</c> value in command-line order: <c>NAME,VALUE</c>, or
    /// <c>NAME</c> alone for a variable sent without a value.
    /// </summary>
    public List<string> EnvironmentVariables { get; } = [];

    /// <summary>
    /// Gets or sets a value indicating whether a <c>BINARY=</c> value read as zero, so
    /// this side neither offers nor accepts BINARY in either direction. A later
    /// <c>BINARY=1</c> does not undo it, as in curl 8.21.0.
    /// </summary>
    public bool BinaryRefused { get; set; }
}
