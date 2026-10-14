using Curl.Cli;

namespace Curl.Console;

/// <summary>
/// The rule for the tool's own <c>Note: </c> lines, as curl 8.22.0's <c>notef</c>
/// (<c>src/tool_msgs.c</c>) applies it: written under <c>-v</c> or a <c>--trace</c> option, and never
/// under <c>-s</c>, <c>-S</c> or not (measured 2026-10-13, BL-2173; upstream test1740).
/// </summary>
internal static class ToolNotes
{
    /// <summary>Whether a <c>Note: </c> line is written for a run with <paramref name="options" />.</summary>
    /// <param name="options">The run's options.</param>
    /// <returns><see langword="true" /> when a trace is on and <c>-s</c> is not.</returns>
    internal static bool AreWritten(CommandLineOptions options) =>
        options.Trace != TraceKind.None && !options.Silent;
}
