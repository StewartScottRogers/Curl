using System.Globalization;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Cookies;

/// <summary>
/// Cookie-shaped <see cref="TestDiagnostics"/> lines for this project's tests (BL-1462): <c>Set-Cookie</c> headers
/// with the URL they arrived on and the jar's clock, stored cookies, and text with its control characters escaped
/// and long values shortened, so a log stays readable and reads the same on every platform.
/// </summary>
internal static class CookieTestDiagnostics
{
    /// <summary>The most characters <see cref="Shown(string?)"/> keeps before it says how many more there are.</summary>
    public const int ShownCharacterCap = 200;

    /// <summary>The most items <see cref="Shown(IEnumerable{string})"/> lists before it says how many more there are.</summary>
    public const int ShownItemCap = 20;

    /// <summary>Quotes text, escaping control characters and shortening it past <see cref="ShownCharacterCap"/>.</summary>
    /// <param name="text">The text, or null.</param>
    /// <returns>The quoted text, or <c>(null)</c>.</returns>
    public static string Shown(string? text)
    {
        if (text is null)
        {
            return "(null)";
        }

        StringBuilder shown = new("\"");
        foreach (char character in text.Length > ShownCharacterCap ? text[..ShownCharacterCap] : text)
        {
            shown.Append(character switch
            {
                '\t' => "\\t",
                '\r' => "\\r",
                '\n' => "\\n",
                '"' => "\\\"",
                < ' ' or '\u007F' => string.Create(CultureInfo.InvariantCulture, $"\\u{(int)character:X4}"),
                _ => character.ToString(),
            });
        }

        shown.Append('"');
        if (text.Length > ShownCharacterCap)
        {
            shown.Append(CultureInfo.InvariantCulture, $" ... ({text.Length - ShownCharacterCap} more characters, {text.Length} in all)");
        }

        return shown.ToString();
    }

    /// <summary>Lists texts, each <see cref="Shown(string?)"/>, shortening the list past <see cref="ShownItemCap"/>.</summary>
    /// <param name="texts">The texts.</param>
    /// <returns>The bracketed list.</returns>
    public static string Shown(IEnumerable<string> texts)
    {
        string[] all = [.. texts];
        string more = all.Length > ShownItemCap
            ? string.Create(CultureInfo.InvariantCulture, $", ... ({all.Length - ShownItemCap} more, {all.Length} in all)")
            : string.Empty;
        return "[" + string.Join(", ", all.Take(ShownItemCap).Select(text => Shown(text))) + more + "]";
    }

    /// <summary>Lists cookies as <c>domain|path|name=value</c> with their flags and expiry.</summary>
    /// <param name="cookies">The cookies.</param>
    /// <returns>The bracketed list.</returns>
    public static string ShownCookies(IEnumerable<Cookie> cookies) =>
        Shown(cookies.Select(cookie => string.Create(
            CultureInfo.InvariantCulture,
            $"{cookie.Domain}|{cookie.Path}|{cookie.Name}={cookie.Value}|subdomains {cookie.IncludesSubdomains}|secure {cookie.IsSecure}|httponly {cookie.IsHttpOnly}|expires {cookie.ExpiresUnixSeconds}")));

    /// <summary>Writes the ARRANGE line for <c>Set-Cookie</c> headers, the URL they arrived on and the jar's clock.</summary>
    /// <param name="diagnostics">The test's diagnostics.</param>
    /// <param name="url">The URL the response came from.</param>
    /// <param name="headers">The <c>Set-Cookie</c> header values.</param>
    /// <param name="now">The time the jar sees.</param>
    public static void ArrangeSetCookies(this TestDiagnostics diagnostics, CurlUrl url, IEnumerable<string> headers, DateTimeOffset now) =>
        diagnostics.Arrange(
            string.Create(CultureInfo.InvariantCulture, $"Set-Cookie headers from {url.OriginalString} at Unix {now.ToUnixTimeSeconds()}"),
            Shown(headers));

    /// <summary>Writes an ARRANGE line for text, shown with <see cref="Shown(string?)"/>.</summary>
    /// <param name="diagnostics">The test's diagnostics.</param>
    /// <param name="label">What the text is.</param>
    /// <param name="text">The text.</param>
    public static void ArrangeText(this TestDiagnostics diagnostics, string label, string? text) =>
        diagnostics.Arrange(label, Shown(text));

    /// <summary>Writes an ACT line for text, shown with <see cref="Shown(string?)"/>.</summary>
    /// <param name="diagnostics">The test's diagnostics.</param>
    /// <param name="label">What the text is.</param>
    /// <param name="text">The text.</param>
    public static void ActText(this TestDiagnostics diagnostics, string label, string? text) =>
        diagnostics.Act(label, Shown(text));

    /// <summary>Writes an ACT line listing the cookies a store or file holds.</summary>
    /// <param name="diagnostics">The test's diagnostics.</param>
    /// <param name="label">What holds them.</param>
    /// <param name="cookies">The cookies.</param>
    public static void ActCookies(this TestDiagnostics diagnostics, string label, IEnumerable<Cookie> cookies)
    {
        Cookie[] all = [.. cookies];
        diagnostics.Act(string.Create(CultureInfo.InvariantCulture, $"{label} ({all.Length})"), ShownCookies(all));
    }

    /// <summary>Writes an ASSERT line for text and, when both are present, a DIFF line.</summary>
    /// <param name="diagnostics">The test's diagnostics.</param>
    /// <param name="label">What is compared.</param>
    /// <param name="expected">The expected text.</param>
    /// <param name="actual">The actual text.</param>
    public static void AssertText(this TestDiagnostics diagnostics, string label, string? expected, string? actual)
    {
        diagnostics.Assert(label, Shown(expected), Shown(actual));
        if (expected is not null && actual is not null)
        {
            diagnostics.Diff(label, expected, actual);
        }
    }

    /// <summary>Writes an ASSERT line for two lists of texts and a DIFF line of them joined by line feeds.</summary>
    /// <param name="diagnostics">The test's diagnostics.</param>
    /// <param name="label">What is compared.</param>
    /// <param name="expected">The expected texts.</param>
    /// <param name="actual">The actual texts.</param>
    public static void AssertTexts(this TestDiagnostics diagnostics, string label, IEnumerable<string> expected, IEnumerable<string> actual)
    {
        string[] expectedAll = [.. expected];
        string[] actualAll = [.. actual];
        diagnostics.Assert(label, Shown(expectedAll), Shown(actualAll));
        diagnostics.Diff(label, string.Join('\n', expectedAll), string.Join('\n', actualAll));
    }
}
