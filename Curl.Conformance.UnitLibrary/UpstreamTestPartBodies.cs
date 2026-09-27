using System.Text;

namespace Curl.Conformance;

/// <summary>
/// Reads a parsed part's body the way <c>runtests.pl</c> at <c>curl-8_21_0</c> uses it: as lines of
/// text, as the bytes a server sends, or as the bytes an output is compared against.
/// </summary>
internal static class UpstreamTestPartBodies
{
    /// <summary>The body as text, one character per byte.</summary>
    /// <param name="part">The part, or <see langword="null"/> for an absent one.</param>
    /// <returns>The body; empty for an absent part.</returns>
    public static string Text(UpstreamTestSection? part) =>
        part is null ? string.Empty : Encoding.Latin1.GetString(part.Content.Span);

    /// <summary>The body's lines, trimmed, blank lines left out.</summary>
    /// <param name="part">The part, or <see langword="null"/> for an absent one.</param>
    /// <returns>The lines; empty for an absent part.</returns>
    public static string[] Lines(UpstreamTestSection? part) =>
        Text(part).Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    /// <summary>
    /// The body of a <c>&lt;data&gt;</c>-like part with the line endings <c>prepro</c> forces on it:
    /// every line CRLF under <c>crlf="yes"</c>, header lines under <c>crlf="headers"</c>. Upstream
    /// forces them on <c>&lt;data…&gt;</c> parts (<c>&lt;datacheck&gt;</c> and <c>&lt;dataN&gt;</c>
    /// included) and only for those two values.
    /// </summary>
    /// <param name="part">The part.</param>
    /// <returns>The body as the server sends it, before <c>base64</c> and <c>nonewline</c>.</returns>
    public static byte[] Served(UpstreamTestSection part) =>
        part.GetAttribute("crlf") switch
        {
            "yes" => UpstreamTestSectionLineEndings.ForceCrlf(part.Content.Span),
            "headers" => UpstreamTestSectionLineEndings.ForceHeaderCrlf(part.Content.Span),
            _ => part.Content.ToArray(),
        };

    /// <summary>
    /// The body with the <c>crlf</c> attribute applied the way the comparison of an output applies
    /// it: header lines under <c>crlf="headers"</c>, every line under any other value that is set.
    /// </summary>
    /// <param name="body">The body.</param>
    /// <param name="part">The part whose attributes apply.</param>
    /// <returns>The body with its line endings forced.</returns>
    public static byte[] WithCrlf(byte[] body, UpstreamTestSection part) =>
        part.GetAttribute("crlf") == "headers" ? UpstreamTestSectionLineEndings.ForceHeaderCrlf(body)
        : part.IsAttributeSet("crlf") ? UpstreamTestSectionLineEndings.ForceCrlf(body)
        : body;

    /// <summary>The body with its final line feed cut when the part says <c>nonewline</c>.</summary>
    /// <param name="body">The body.</param>
    /// <param name="part">The part whose attributes apply.</param>
    /// <returns>The body, cut or not.</returns>
    public static byte[] WithoutFinalNewline(byte[] body, UpstreamTestSection part) =>
        part.IsAttributeSet("nonewline") ? UpstreamTestSectionLineEndings.CutFinalNewline(body) : body;
}
