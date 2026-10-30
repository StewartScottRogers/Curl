using System.Globalization;
using System.Text;

namespace Curl.Extension.CodeGeneration;

/// <summary>
/// Writes <c>--powershell</c>'s file (ADR-1128): a PowerShell 7 script sending each request with
/// <c>Invoke-RestMethod</c>, by the parameters its documentation names. <c>-Uri</c>; <c>-Method</c> for a
/// <c>WebRequestMethod</c> name, else <c>-CustomMethod</c>; <c>-Headers</c> for the fields, with
/// <c>-SkipHeaderValidation</c> so each is sent as given and fields of one name joined with <c>", "</c>;
/// <c>-ContentType</c> and <c>-Body</c> as a byte array of curl's exact bytes; <c>-MaximumRedirection</c> for curl's
/// redirect setting; and <c>-SkipHttpErrorCheck</c>, since curl does not fail on an error status.
/// </summary>
internal static class PowerShellScript
{
    /// <summary>The characters PowerShell reads as a single quote: the apostrophe and U+2018 to U+201B (about_Quoting_Rules).</summary>
    private static readonly string QuoteCharacters = new(['\x27', (char)0x2018, (char)0x2019, (char)0x201a, (char)0x201b]);

    /// <summary>The <c>WebRequestMethod</c> enumeration's names, by the method each sends.</summary>
    private static readonly Dictionary<string, string> WebRequestMethods = new(StringComparer.Ordinal)
    {
        ["GET"] = "Get",
        ["HEAD"] = "Head",
        ["POST"] = "Post",
        ["PUT"] = "Put",
        ["DELETE"] = "Delete",
        ["TRACE"] = "Trace",
        ["OPTIONS"] = "Options",
        ["MERGE"] = "Merge",
        ["PATCH"] = "Patch",
    };

    /// <summary>Writes the script for the requests.</summary>
    /// <param name="requests">The requests, in the order curl sent them.</param>
    /// <returns>The script's text.</returns>
    internal static string For(IReadOnlyList<GeneratedRequest> requests)
    {
        var text = new StringBuilder("# Written by curl --powershell: the requests this command line sends, with Invoke-RestMethod.\n");
        foreach (var request in requests)
        {
            AppendRequest(text, request);
        }

        return text.ToString();
    }

    /// <summary>Gives <paramref name="value" /> as a PowerShell single-quoted string.</summary>
    /// <param name="value">The text.</param>
    /// <returns>The string, quotes included; every quote character PowerShell reads as one is doubled.</returns>
    internal static string Literal(string value)
    {
        var literal = new StringBuilder("'");
        foreach (var character in value)
        {
            literal.Append(character);
            if (QuoteCharacters.Contains(character, StringComparison.Ordinal))
            {
                literal.Append(character);
            }
        }

        return literal.Append('\'').ToString();
    }

    private static void AppendRequest(StringBuilder text, GeneratedRequest request)
    {
        text.Append("\nInvoke-RestMethod -Uri ").Append(Literal(request.Url));
        text.Append(WebRequestMethods.TryGetValue(request.Method, out var name) ? " -Method " + name : " -CustomMethod " + Literal(request.Method));
        AppendHeaders(text, request);
        AppendBody(text, request);
        text.Append(" -MaximumRedirection ").Append(MaximumRedirection(request).ToString(CultureInfo.InvariantCulture))
            .Append(" -SkipHeaderValidation -SkipHttpErrorCheck\n");
    }

    private static int MaximumRedirection(GeneratedRequest request) =>
        !request.FollowRedirects ? 0 : request.MaxRedirects < 0 ? int.MaxValue : request.MaxRedirects;

    private static void AppendHeaders(StringBuilder text, GeneratedRequest request)
    {
        var fields = request.Headers
            .Where(header => !header.Key.Equals("Content-Type", StringComparison.OrdinalIgnoreCase))
            .GroupBy(header => header.Key, StringComparer.OrdinalIgnoreCase)
            .Select(field => Literal(field.Key) + " = " + Literal(string.Join(", ", field.Select(header => header.Value))))
            .ToArray();
        if (fields.Length > 0)
        {
            text.Append(" -Headers @{ ").Append(string.Join("; ", fields)).Append(" }");
        }
    }

    private static void AppendBody(StringBuilder text, GeneratedRequest request)
    {
        foreach (var contentType in request.Headers.Where(header => header.Key.Equals("Content-Type", StringComparison.OrdinalIgnoreCase)).Take(1))
        {
            text.Append(" -ContentType ").Append(Literal(contentType.Value));
        }

        if (request.Body is null)
        {
            return;
        }

        var bodyText = request.BodyText;
        text.Append(bodyText is null
            ? " -Body ([System.Convert]::FromBase64String(" + Literal(Convert.ToBase64String(request.Body)) + "))"
            : " -Body ([System.Text.Encoding]::UTF8.GetBytes(" + Literal(bodyText) + "))");
    }
}
