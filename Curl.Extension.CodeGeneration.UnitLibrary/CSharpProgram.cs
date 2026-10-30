using System.Globalization;
using System.Text;

namespace Curl.Extension.CodeGeneration;

/// <summary>
/// Writes <c>--csharp</c>'s file (ADR-1138): a C# file-based program sending each request with <c>HttpClient</c>, as
/// its documentation shows. One <c>HttpClientHandler</c> takes curl's redirect and decompression settings
/// (<c>AllowAutoRedirect</c>, <c>MaxAutomaticRedirections</c>, <c>AutomaticDecompression</c>); each request is an
/// <c>HttpRequestMessage</c> whose fields go on <c>request.Headers</c>, or on <c>request.Content.Headers</c> for the
/// content fields <c>HttpContentHeaders</c> holds, with <c>TryAddWithoutValidation</c> so each is sent as given; the
/// body is a <c>ByteArrayContent</c> of curl's exact bytes, and the response body is copied to standard output.
/// </summary>
internal static class CSharpProgram
{
    private const char LineSeparator = (char)0x2028;

    private const char ParagraphSeparator = (char)0x2029;

    /// <summary>The simple escape sequences a regular string literal writes these characters with.</summary>
    private static readonly Dictionary<char, string> SimpleEscapes = new()
    {
        ['\\'] = "\\\\",
        ['"'] = "\\\"",
        ['\n'] = "\\n",
        ['\r'] = "\\r",
        ['\t'] = "\\t",
    };

    /// <summary>Writes the program for the requests.</summary>
    /// <param name="requests">The requests, in the order curl sent them; at least one.</param>
    /// <returns>The program's text.</returns>
    internal static string For(IReadOnlyList<GeneratedRequest> requests)
    {
        var text = new StringBuilder();
        text.Append("// Written by curl --csharp: the requests this command line sends, with HttpClient.\n")
            .Append("using System.Net;\nusing System.Text;\n\n");
        AppendHandler(text, requests[0]);
        text.Append("using var client = new HttpClient(handler);\n")
            .Append("using var output = Console.OpenStandardOutput();\n");
        foreach (var request in requests)
        {
            AppendRequest(text, request);
        }

        return text.ToString();
    }

    private static void AppendHandler(StringBuilder text, GeneratedRequest first)
    {
        var follows = first.FollowRedirects && first.MaxRedirects != 0;
        text.Append("using var handler = new HttpClientHandler\n{\n")
            .Append("    AllowAutoRedirect = ").Append(follows ? "true" : "false").Append(",\n");
        if (follows && first.MaxRedirects > 0)
        {
            text.Append("    MaxAutomaticRedirections = ").Append(first.MaxRedirects.ToString(CultureInfo.InvariantCulture)).Append(",\n");
        }

        text.Append("    AutomaticDecompression = DecompressionMethods.").Append(first.Compressed ? "All" : "None").Append(",\n};\n");
    }

    /// <summary>Gives <paramref name="value" /> as a C# string literal.</summary>
    /// <param name="value">The text.</param>
    /// <returns>The literal, quotes included.</returns>
    internal static string Literal(string value)
    {
        var literal = new StringBuilder("\"");
        foreach (var character in value)
        {
            literal.Append(Escaped(character));
        }

        return literal.Append('"').ToString();
    }

    private static string Escaped(char character)
    {
        if (SimpleEscapes.TryGetValue(character, out var escape))
        {
            return escape;
        }

        return char.IsControl(character) || character is LineSeparator or ParagraphSeparator
            ? "\\u" + ((int)character).ToString("x4", CultureInfo.InvariantCulture)
            : character.ToString();
    }

    private static void AppendRequest(StringBuilder text, GeneratedRequest request)
    {
        text.Append("\nusing (var request = new HttpRequestMessage(new HttpMethod(").Append(Literal(request.Method)).Append("), ").Append(Literal(request.Url)).Append("))\n{\n");
        foreach (var header in request.RequestHeaders)
        {
            text.Append("    request.Headers.TryAddWithoutValidation(").Append(Literal(header.Key)).Append(", ").Append(Literal(header.Value)).Append(");\n");
        }

        var contentHeaders = request.ContentHeaders.ToArray();
        if (request.Body is not null || contentHeaders.Length > 0)
        {
            text.Append("    request.Content = new ByteArrayContent(").Append(BodyExpression(request)).Append(");\n");
        }

        foreach (var header in contentHeaders)
        {
            text.Append("    request.Content!.Headers.TryAddWithoutValidation(").Append(Literal(header.Key)).Append(", ").Append(Literal(header.Value)).Append(");\n");
        }

        text.Append("    using var response = await client.SendAsync(request);\n")
            .Append("    await response.Content.CopyToAsync(output);\n}\n");
    }

    private static string BodyExpression(GeneratedRequest request)
    {
        if (request.Body is null)
        {
            return "[]";
        }

        var bodyText = request.BodyText;
        return bodyText is null
            ? "Convert.FromBase64String(" + Literal(Convert.ToBase64String(request.Body)) + ")"
            : "Encoding.UTF8.GetBytes(" + Literal(bodyText) + ")";
    }
}
