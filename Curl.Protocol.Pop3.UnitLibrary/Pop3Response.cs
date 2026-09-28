namespace Curl.Protocol.Pop3;

/// <summary>
/// One status line read from a POP3 connection: a line starting <c>+</c> or <c>-ERR</c>.
/// </summary>
/// <param name="Line">The line without its line end, such as <c>+OK POP3 ready</c>.</param>
internal sealed record Pop3Response(string Line)
{
    /// <summary>
    /// Gets a value indicating whether the line starts with <c>+OK</c>, in capitals, as
    /// curl 8.21.0 requires (<c>+ok</c> and a bare <c>+</c> are refusals, measured in BL-547).
    /// </summary>
    public bool IsOk => Line.StartsWith("+OK", StringComparison.Ordinal);
}
