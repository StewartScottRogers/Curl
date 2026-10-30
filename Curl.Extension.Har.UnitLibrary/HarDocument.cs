using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Curl.Extension.Har;

/// <summary>
/// Writes an HTTP Archive, HAR 1.2 (W3C draft, ADR-1127): the <c>log</c> object with its <c>creator</c> and one
/// entry per transfer, each with the <c>request</c>, <c>response</c>, <c>cache</c> and <c>timings</c> objects
/// the specification requires. A response the server never sent is written with status 0.
/// </summary>
internal static class HarDocument
{
    /// <summary>The HAR format version written, the <c>log.version</c> field.</summary>
    internal const string Version = "1.2";

    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    /// <summary>Writes the archive of <paramref name="exchanges" />, in the order given, as indented UTF-8 JSON.</summary>
    /// <param name="exchanges">The transfers, one entry each.</param>
    /// <returns>The archive's bytes.</returns>
    internal static byte[] Write(IEnumerable<HarExchange> exchanges)
    {
        using MemoryStream bytes = new();
        using (Utf8JsonWriter json = new(bytes, new JsonWriterOptions { Indented = true }))
        {
            json.WriteStartObject();
            json.WriteStartObject("log");
            json.WriteString("version", Version);
            json.WriteStartObject("creator");
            json.WriteString("name", "curl");
            json.WriteString("version", typeof(HarDocument).Assembly.GetName().Version!.ToString(3));
            json.WriteEndObject();
            json.WriteStartArray("entries");
            foreach (var exchange in exchanges)
            {
                WriteEntry(json, exchange);
            }

            json.WriteEndArray();
            json.WriteEndObject();
            json.WriteEndObject();
        }

        return bytes.ToArray();
    }

    private static void WriteEntry(Utf8JsonWriter json, HarExchange exchange)
    {
        var report = exchange.Result.Report;
        var timings = HarTimings.Of(report?.Timings, exchange.TimestampFrequency);
        json.WriteStartObject();
        json.WriteString("startedDateTime", exchange.StartedDateTime.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture));
        json.WriteNumber("time", timings.Total);
        WriteRequest(json, exchange, report);
        WriteResponse(json, exchange, report);
        json.WriteStartObject("cache");
        json.WriteEndObject();
        timings.Write(json);
        if (report?.RemoteEndPoint is { } remote)
        {
            json.WriteString("serverIPAddress", remote.Address.ToString());
        }

        json.WriteEndObject();
    }

    private static void WriteRequest(Utf8JsonWriter json, HarExchange exchange, TransferReport? report)
    {
        json.WriteStartObject("request");
        json.WriteString("method", exchange.Method);
        json.WriteString("url", exchange.Url);
        json.WriteString("httpVersion", HttpVersionOf(report));
        WriteEmptyArray(json, "cookies");
        WriteNameValues(json, "headers", exchange.RequestHeaders);
        WriteNameValues(json, "queryString", QueryOf(exchange.Url));
        if (exchange.PostData is { } body)
        {
            json.WriteStartObject("postData");
            json.WriteString("mimeType", body.ContentType);
            json.WriteString("text", Encoding.UTF8.GetString(body.Content.Span));
            json.WriteEndObject();
        }

        json.WriteNumber("headersSize", -1);
        json.WriteNumber("bodySize", exchange.PostData?.Content.Length ?? 0);
        json.WriteEndObject();
    }

    private static void WriteResponse(Utf8JsonWriter json, HarExchange exchange, TransferReport? report)
    {
        var headers = report?.ResponseHeaders ?? [];
        json.WriteStartObject("response");
        WriteStatusLine(json, report);
        WriteEmptyArray(json, "cookies");
        WriteNameValues(json, "headers", headers);
        WriteContent(json, exchange.ResponseBody, MimeTypeOf(report, headers));
        json.WriteString("redirectURL", report?.RedirectUrl ?? string.Empty);
        WriteSizes(json, report?.HeaderSize, exchange.ResponseBody.Length);
        json.WriteEndObject();
    }

    /// <summary>Writes the response's <c>status</c>, <c>statusText</c> and <c>httpVersion</c>, 0 and empty when no response arrived.</summary>
    private static void WriteStatusLine(Utf8JsonWriter json, TransferReport? report)
    {
        json.WriteNumber("status", report?.ResponseCode ?? 0);
        json.WriteString("statusText", string.Empty);
        json.WriteString("httpVersion", HttpVersionOf(report));
    }

    /// <summary>Writes the <c>content</c> object: the body as text when it is UTF-8, otherwise base64 with <c>encoding</c> saying so.</summary>
    private static void WriteContent(Utf8JsonWriter json, byte[] body, string mimeType)
    {
        json.WriteStartObject("content");
        json.WriteNumber("size", body.Length);
        json.WriteString("mimeType", mimeType);
        if (TextOf(body) is { } text)
        {
            json.WriteString("text", text);
        }
        else
        {
            json.WriteString("text", Convert.ToBase64String(body));
            json.WriteString("encoding", "base64");
        }

        json.WriteEndObject();
    }

    private static string MimeTypeOf(TransferReport? report, IReadOnlyList<KeyValuePair<string, string>> headers) =>
        report?.ContentType ?? FieldValueOf(headers, "Content-Type") ?? string.Empty;

    /// <summary>Writes the response's <c>headersSize</c> and <c>bodySize</c>, both -1 when no response arrived.</summary>
    private static void WriteSizes(Utf8JsonWriter json, long? headersSize, long bodySize)
    {
        json.WriteNumber("headersSize", headersSize ?? -1);
        json.WriteNumber("bodySize", headersSize is null ? -1 : bodySize);
    }

    private static string? TextOf(byte[] body)
    {
        try
        {
            return StrictUtf8.GetString(body);
        }
        catch (DecoderFallbackException)
        {
            return null;
        }
    }

    private static string HttpVersionOf(TransferReport? report) => report?.HttpVersion switch
    {
        null => string.Empty,
        { Major: >= 2 } version => "HTTP/" + version.Major.ToString(CultureInfo.InvariantCulture),
        var version => "HTTP/" + version.ToString(2),
    };

    private static string? FieldValueOf(IReadOnlyList<KeyValuePair<string, string>> headers, string name) =>
        headers.FirstOrDefault(header => header.Key.Equals(name, StringComparison.OrdinalIgnoreCase)).Value;

    /// <summary>Reads the URL's query as its name and value pairs, each percent-decoded (HAR 1.2, "queryString").</summary>
    internal static List<KeyValuePair<string, string>> QueryOf(string url)
    {
        var query = url.Split('#')[0];
        var mark = query.IndexOf('?', StringComparison.Ordinal);
        return mark < 0
            ? []
            : [.. query[(mark + 1)..].Split('&', StringSplitOptions.RemoveEmptyEntries).Select(ParameterOf)];
    }

    private static KeyValuePair<string, string> ParameterOf(string parameter)
    {
        var parts = parameter.Split('=', 2);
        return new(Decode(parts[0]), parts.Length == 2 ? Decode(parts[1]) : string.Empty);
    }

    private static string Decode(string text) => Uri.UnescapeDataString(text.Replace('+', ' '));

    private static void WriteEmptyArray(Utf8JsonWriter json, string name)
    {
        json.WriteStartArray(name);
        json.WriteEndArray();
    }

    private static void WriteNameValues(Utf8JsonWriter json, string name, IEnumerable<KeyValuePair<string, string>> pairs)
    {
        json.WriteStartArray(name);
        foreach (var (key, value) in pairs)
        {
            json.WriteStartObject();
            json.WriteString("name", key);
            json.WriteString("value", value);
            json.WriteEndObject();
        }

        json.WriteEndArray();
    }
}
