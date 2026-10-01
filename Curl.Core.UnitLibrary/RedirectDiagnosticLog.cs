using System.Globalization;
using Curl.Protocol.Abstractions;

namespace Curl.Core;

/// <summary>
/// Writes <see cref="RedirectFollower" />'s decisions to Curl's own diagnostic log, component
/// <see cref="DiagnosticLogComponents.Redirect" /> (ADR-0222, BL-921): a redirect not followed as
/// <c>warning</c>, each hop followed as <c>info</c>, and the <c>--max-redirs</c> counters as
/// <c>verbose</c>.
/// </summary>
/// <param name="log">Where the lines go; <see cref="NoDiagnosticLog.Instance" /> writes nothing.</param>
/// <remarks>
/// Every method tests <see cref="IDiagnosticLog.IsEnabled" /> before it builds its message, so a
/// disabled level costs no formatting. A target URL is written without its user information,
/// which may hold a password (ADR-0222, decision 7).
/// </remarks>
internal sealed class RedirectDiagnosticLog(IDiagnosticLog log)
{
    /// <summary>Logs, at <c>verbose</c>, the <c>--max-redirs</c> limit and how many redirects were followed.</summary>
    /// <param name="maxRedirects">The <c>--max-redirs</c> limit; negative for none.</param>
    /// <param name="followed">The redirects followed so far.</param>
    public void Limit(int maxRedirects, int followed)
    {
        if (log.IsEnabled(DiagnosticLogLevel.Verbose))
        {
            Write(DiagnosticLogLevel.Verbose, string.Create(
                CultureInfo.InvariantCulture,
                $"--max-redirs {maxRedirects}, {followed} redirects followed"));
        }
    }

    /// <summary>Logs, at <c>info</c>, a hop followed: the status, the target and the method it is sent with.</summary>
    /// <param name="responseCode">The 3xx status that redirected.</param>
    /// <param name="target">The target URL, which may carry user information.</param>
    /// <param name="bodyDropped">Whether the request body is dropped, making the hop a GET.</param>
    /// <param name="methodDropped">Whether the <c>-X</c> method is dropped, making the hop a GET.</param>
    public void Followed(int responseCode, string target, bool bodyDropped, bool methodDropped)
    {
        if (log.IsEnabled(DiagnosticLogLevel.Info))
        {
            string method = bodyDropped || methodDropped ? "method changed to GET" : "method kept";
            Write(DiagnosticLogLevel.Info, string.Create(
                CultureInfo.InvariantCulture,
                $"following {responseCode} to {WithoutUserInformation(target)}, {method}"));
        }
    }

    /// <summary>Logs, at <c>warning</c>, a redirect not followed and the failure that ends the chain.</summary>
    /// <param name="responseCode">The 3xx status that redirected.</param>
    /// <param name="target">The target URL, which may carry user information.</param>
    /// <param name="failure">The failure that ends the chain.</param>
    public void Refused(int responseCode, string target, TransferResult failure)
    {
        if (log.IsEnabled(DiagnosticLogLevel.Warning))
        {
            Write(DiagnosticLogLevel.Warning, string.Create(
                CultureInfo.InvariantCulture,
                $"{responseCode} to {WithoutUserInformation(target)} not followed: exit {(int)failure.ExitCode} ({failure.ExitCode}), {failure.ErrorMessage}"));
        }
    }

    /// <summary>
    /// <paramref name="url" /> without the <c>user:password@</c> of its authority, or unchanged when
    /// it has none.
    /// </summary>
    /// <param name="url">A URL's text.</param>
    /// <returns>The text without user information.</returns>
    internal static string WithoutUserInformation(string url)
    {
        int schemeEnd = url.IndexOf("://", StringComparison.Ordinal);
        int authorityStart = schemeEnd < 0 ? 0 : schemeEnd + 3;
        int authorityEnd = url.IndexOfAny(['/', '?', '#'], authorityStart);
        int end = authorityEnd < 0 ? url.Length : authorityEnd;
        int at = url.AsSpan(authorityStart, end - authorityStart).LastIndexOf('@');
        return at < 0 ? url : url[..authorityStart] + url[(authorityStart + at + 1)..];
    }

    private void Write(DiagnosticLogLevel level, string message) => log.Write(level, DiagnosticLogComponents.Redirect, message);
}
