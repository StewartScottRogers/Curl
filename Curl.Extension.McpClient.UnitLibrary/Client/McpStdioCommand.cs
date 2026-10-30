using Curl.Protocol.Abstractions;

namespace Curl.Extension.McpClient.Client;

/// <summary>
/// The local server an <c>mcp+stdio://localhost/command?arg1&amp;arg2</c> URL names (ADR-1074): the percent-decoded path is
/// the program, without its leading slash when it is one segment (a name looked up in <c>PATH</c>, <c>mcp+stdio://localhost/npx</c>)
/// or starts with a drive letter (<c>mcp+stdio://localhost/C:/servers/x.exe</c>); a longer path is absolute
/// (<c>mcp+stdio://localhost/usr/local/bin/server</c>). Each <c>&amp;</c>-separated part of the query, percent-decoded, is one
/// argument. The host must be <c>localhost</c>: curl's URL parser reads <c>mcp+stdio:///npx</c> as the host <c>npx</c>.
/// </summary>
/// <param name="FileName">The program to start.</param>
/// <param name="Arguments">Its arguments, in order.</param>
internal sealed record McpStdioCommand(string FileName, IReadOnlyList<string> Arguments)
{
    /// <summary>Reads the command from <paramref name="url"/>, or gives <see langword="null"/> when it names a remote host or no program.</summary>
    public static McpStdioCommand? From(CurlUrl url)
    {
        string path = Uri.UnescapeDataString(url.AbsolutePath);
        bool isLocal = string.Equals(url.Host, "localhost", StringComparison.OrdinalIgnoreCase);
        if (!isLocal || path.Length <= 1)
        {
            return null;
        }

        return new McpStdioCommand(FileNameOf(path), ArgumentsOf(url.Query));
    }

    /// <summary>
    /// Gives the program a decoded path of two or more characters names. A second slash at index 1 stops the drive check at
    /// its letter test, and one further on means the path has at least three characters, so <c>path[2]</c> is always there.
    /// </summary>
    private static string FileNameOf(string path) =>
        path.IndexOf('/', 1) < 0 || (char.IsAsciiLetter(path[1]) && path[2] == ':') ? path[1..] : path;

    private static string[] ArgumentsOf(string? query) =>
        string.IsNullOrEmpty(query) ? [] : [.. query.Split('&').Select(Uri.UnescapeDataString)];
}
