namespace Curl.Core.Globbing;

/// <summary>
/// Why a URL is not a well-formed glob, and the one-based column curl points its caret at;
/// column 0 means curl names no column.
/// </summary>
internal sealed class UrlGlobError(string reason, int column)
{
    /// <summary>
    /// Formats the error as curl 8.21.0 does after <c>curl: (3) </c>: the reason, then, when
    /// there is a column, <c> in position N:</c>, the URL, and a caret under column N. Like
    /// curl's <c>%*s^</c>, the caret line is never shorter than one space and the caret.
    /// </summary>
    public string ToMessage(string url) =>
        column == 0
            ? reason
            : $"{reason} in position {column}:\n{url}\n{new string(' ', Math.Max(1, column - 1))}^";
}
