namespace Curl.Extended.Protocol.McpClient.Stdio;

/// <summary>
/// Refuses, for an MCP server that is a <c>.cmd</c> or <c>.bat</c> file, an argument holding a cmd.exe metacharacter
/// (<c>&amp; | &lt; &gt; ^ " %</c>) or a control character, because cmd.exe would run the rest of the argument as another
/// command or expand it (ADR-1248, the rule the Plugin extension applies to its helpers). Other servers are unchanged.
/// </summary>
internal static class McpBatchArgumentRefusal
{
    /// <summary>Throws when <paramref name="program"/> is a batch file and an argument is unsafe for cmd.exe.</summary>
    /// <exception cref="InvalidOperationException">The program is a batch file and an argument holds such a character.</exception>
    public static void RefuseUnsafeArguments(string program, IReadOnlyList<string> arguments)
    {
        var name = program.TrimEnd('.', ' ');
        var isBatch = name.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith(".bat", StringComparison.OrdinalIgnoreCase);
        if (isBatch && arguments.Any(IsUnsafe))
        {
            throw new InvalidOperationException(
                "an argument holds a character cmd.exe would run as a command (& | < > ^ \" % or a control character), so it is not passed to a .cmd or .bat server");
        }
    }

    private static bool IsUnsafe(string argument) =>
        argument.AsSpan().IndexOfAny("&|<>^\"%") >= 0 || argument.Any(char.IsControl);
}
