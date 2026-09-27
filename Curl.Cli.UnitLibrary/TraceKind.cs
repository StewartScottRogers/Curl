namespace Curl.Cli;

/// <summary>
/// Which of <c>-v</c> / <c>--verbose</c>, <c>--trace</c> and <c>--trace-ascii</c> is in effect: the last
/// of them read wins, as in curl 8.21.0, and <c>--no-verbose</c> turns every one of them off.
/// </summary>
public enum TraceKind
{
    /// <summary>None of them was given, or <c>--no-verbose</c> came after the last one.</summary>
    None = 0,

    /// <summary><c>-v</c> / <c>--verbose</c>: the verbose lines, written to standard error.</summary>
    Verbose,

    /// <summary><c>--trace</c>: a hex and text dump of everything sent and received, written to <see cref="CommandLineOptions.TraceFile"/>.</summary>
    HexDump,

    /// <summary><c>--trace-ascii</c>: a text-only dump of everything sent and received, written to <see cref="CommandLineOptions.TraceFile"/>.</summary>
    AsciiDump,
}
