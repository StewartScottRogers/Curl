namespace Curl.Extension.NamedPipe;

/// <summary>
/// A Windows named pipe's name, <c>\\&lt;server&gt;\pipe\&lt;name&gt;</c>, as Microsoft's "Pipe Names" defines it:
/// the server part is <c>.</c> for this machine, the pipe part any characters but a backslash, the whole at most
/// 256 characters, and <c>pipe</c> matched without regard to case.
/// </summary>
/// <param name="ServerName">The server part, <c>.</c> for this machine.</param>
/// <param name="PipeName">The pipe part, after <c>\pipe\</c>.</param>
public sealed record NamedPipeName(string ServerName, string PipeName)
{
    /// <summary>The longest pipe name string "Pipe Names" allows.</summary>
    public const int MaximumLength = 256;

    private const string Prefix = @"\\";

    private const string PipeSegment = @"\pipe\";

    /// <summary>Parses <paramref name="text" /> as a pipe name.</summary>
    /// <param name="text">The text, such as <c>\\.\pipe\docker_engine</c>.</param>
    /// <param name="name">The name, or <see langword="null" /> when the text is not one.</param>
    /// <returns><see langword="true" /> when <paramref name="text" /> is a pipe name.</returns>
    public static bool TryParse(string text, out NamedPipeName? name)
    {
        ArgumentNullException.ThrowIfNull(text);
        name = text.Length <= MaximumLength && text.StartsWith(Prefix, StringComparison.Ordinal) && TrySplit(text, out string server, out string pipe)
            ? new NamedPipeName(server, pipe)
            : null;
        return name is not null;
    }

    private static bool TrySplit(string text, out string server, out string pipe)
    {
        int pipeSegment = text.IndexOf(PipeSegment, Prefix.Length, StringComparison.OrdinalIgnoreCase);
        server = pipeSegment > Prefix.Length ? text[Prefix.Length..pipeSegment] : string.Empty;
        pipe = pipeSegment > Prefix.Length ? text[(pipeSegment + PipeSegment.Length)..] : string.Empty;
        return server.Length > 0 && !server.Contains('\\', StringComparison.Ordinal) && pipe.Length > 0 && !pipe.Contains('\\', StringComparison.Ordinal);
    }

    /// <summary>Gives the name as "Pipe Names" writes it.</summary>
    /// <returns><c>\\&lt;server&gt;\pipe\&lt;name&gt;</c>.</returns>
    public override string ToString() => $@"{Prefix}{ServerName}{PipeSegment}{PipeName}";
}
