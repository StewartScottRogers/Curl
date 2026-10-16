namespace Curl.Mcp;

/// <summary>
/// How one curl command-line option uses a local path, read or written, so the MCP server can
/// confine it to <c>CURL_MCP_ROOT</c> (ADR-0548). <see cref="McpOptionPathTable" /> holds the
/// classification of every option.
/// </summary>
public enum McpOptionPathUse
{
    /// <summary>The option names no local path.</summary>
    None,

    /// <summary>The option reads a local file or directory: its value, or one the option implies (<c>--netrc</c>).</summary>
    ReadsFile,

    /// <summary>The option writes a local file: its value, or one named from the URL or a header (<c>-O</c>, <c>-J</c>).</summary>
    WritesFile,

    /// <summary>The option's value is a directory curl writes files into (<c>--output-dir</c>).</summary>
    WritesDirectory,

    /// <summary>The option's value is a file curl reads at the start and writes at the end (<c>--hsts</c>, <c>--alt-svc</c>).</summary>
    ReadsOrWritesFile,

    /// <summary>
    /// The option's value is either a file curl reads or a literal, decided by its shape
    /// (<c>-b</c>: a value with no <c>=</c> is a file).
    /// </summary>
    ReadsFileOrLiteral,

    /// <summary>
    /// The option's value may carry a local path inside it - <c>@file</c>, <c>&lt;file</c>,
    /// <c>name=@file</c>, <c>%output{file}</c> or a <c>file://</c> or socket URL - which the
    /// whole-argument check finds (BL-2379).
    /// </summary>
    ValueEmbedded,

    /// <summary>The option's value is the path of a Unix domain socket curl connects to.</summary>
    UnixSocketPath,
}
