using System.Text;

namespace Curl.Api;

/// <summary>
/// The <c>CURLOPT_HEADERFUNCTION</c> of one <see cref="CurlClient.SendAsync"/> transfer: splits the
/// header lines it is given into the final response's status code, version, reason phrase, header fields and trailer
/// fields. A status line starts a new response and drops what the previous one collected; a field
/// after the blank line that ends a response's headers is a trailer.
/// </summary>
internal sealed class ResponseHeaderCollector
{
    private List<KeyValuePair<string, string>> _headers = [];
    private List<KeyValuePair<string, string>> _trailers = [];
    private bool _headersEnded;

    /// <summary>The <c>CURL_HTTP_VERSION_*</c> value of each status line's protocol word.</summary>
    private static readonly Dictionary<string, long> HttpVersions = new(StringComparer.Ordinal)
    {
        ["HTTP/1.0"] = 1,
        ["HTTP/1.1"] = 2,
        ["HTTP/2"] = 3,
        ["HTTP/3"] = 30,
    };

    /// <summary>The last status line's status code; 0 when no status line came or its code is not a number.</summary>
    public int StatusCode { get; private set; }

    /// <summary>The last status line's version as a <c>CURL_HTTP_VERSION_*</c> value; 0 when none came or it is not one of 1.0, 1.1, 2 and 3.</summary>
    public long HttpVersion { get; private set; }

    /// <summary>The last status line's reason phrase; empty when it had none.</summary>
    public string ReasonPhrase { get; private set; } = "";

    /// <summary>The last response's header fields, as received.</summary>
    public IReadOnlyList<KeyValuePair<string, string>> Headers => _headers;

    /// <summary>The last response's trailer fields, as received.</summary>
    public IReadOnlyList<KeyValuePair<string, string>> Trailers => _trailers;

    /// <summary>Takes one header line, end of line included; a <see cref="Callbacks.HeaderCallback"/>.</summary>
    /// <param name="line">The line's bytes, read as Latin-1 so every byte is kept.</param>
    /// <returns>The line's length: every byte is taken.</returns>
    public long Take(ReadOnlySpan<byte> line)
    {
        string text = Encoding.Latin1.GetString(line).TrimEnd('\r', '\n');
        if (text.StartsWith("HTTP/", StringComparison.Ordinal))
        {
            StartResponse(text);
        }
        else if (text.Length == 0)
        {
            _headersEnded = true;
        }
        else
        {
            (_headersEnded ? _trailers : _headers).Add(ParseField(text));
        }

        return line.Length;
    }

    private void StartResponse(string statusLine)
    {
        _headers = [];
        _trailers = [];
        _headersEnded = false;
        string[] parts = statusLine.Split(' ', 3);
        HttpVersion = HttpVersions.GetValueOrDefault(parts[0]);
        StatusCode = parts.Length > 1 && int.TryParse(parts[1], System.Globalization.CultureInfo.InvariantCulture, out int code) ? code : 0;
        ReasonPhrase = parts.Length == 3 ? parts[2].Trim() : "";
    }

    private static KeyValuePair<string, string> ParseField(string text)
    {
        int colon = text.IndexOf(':', StringComparison.Ordinal);
        return colon < 0
            ? new(text, "")
            : new(text[..colon], text[(colon + 1)..].Trim(' ', '\t'));
    }
}
