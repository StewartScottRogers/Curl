using System.Globalization;
using System.Text;
using Curl.Http2;
using Curl.Protocol.Abstractions;

namespace Curl.Conformance.Http2Server;

/// <summary>
/// Relays one HTTP/2 request to the backend as HTTP/1.1, as nghttpx does in front of upstream's
/// <c>sws</c> (ADR-0491): the request line and <c>Host</c> from the pseudo-headers, the other
/// fields with capitalised names, then <c>X-Forwarded-Proto: http</c> and <c>Via: 2 nghttpx</c>;
/// and turns the backend's reply into HTTP/2 fields with <c>server: nghttpx</c> and
/// <c>via: 1.1 nghttpx</c>.
/// </summary>
internal static class Http2ServerRequestRelay
{
    // Hop-by-hop fields nghttpx never forwards, either way, and fields it writes itself.
    private static readonly HashSet<string> RequestFieldsNotForwarded =
        ["connection", "keep-alive", "proxy-connection", "transfer-encoding", "upgrade", "te", "http2-settings", "host", "via", "x-forwarded-proto"];

    private static readonly HashSet<string> ReplyFieldsNotForwarded =
        ["connection", "keep-alive", "proxy-connection", "transfer-encoding", "upgrade", "te", "server", "via"];

    /// <summary>
    /// Sends the request to a new connection of <paramref name="backend"/> and reads its reply;
    /// <c>502</c> with no body when the backend cannot be reached.
    /// </summary>
    internal static async Task<Http2ServerReply> RelayAsync(
        IConnector backend, ConnectTarget target, IReadOnlyList<HeaderField> request, byte[] body, CancellationToken cancellationToken)
    {
        ConnectResult connected = await backend.ConnectAsync(target, cancellationToken).ConfigureAwait(false);
        if (connected.Connection is not { } connection)
        {
            return new Http2ServerReply([new(":status", "502"), new("server", "nghttpx"), new("via", "1.1 nghttpx")], []);
        }

        await using (connection.ConfigureAwait(false))
        {
            await connection.WriteAsync(FormatRequest(request, body), cancellationToken).ConfigureAwait(false);
            await connection.FlushAsync(cancellationToken).ConfigureAwait(false);
            return await ReadReplyAsync(new Http2ServerBackendReader(connection), Field(request, ":method") == "HEAD", cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Formats the HTTP/1.1 request nghttpx sends for an HTTP/2 request: a body with no
    /// <c>content-length</c> goes as one chunk.
    /// </summary>
    internal static byte[] FormatRequest(IReadOnlyList<HeaderField> request, byte[] body)
    {
        var text = new StringBuilder();
        text.Append(CultureInfo.InvariantCulture, $"{Field(request, ":method")} {Field(request, ":path")} HTTP/1.1\r\nHost: {Field(request, ":authority")}\r\n");
        foreach (HeaderField field in request.Where(IsForwarded))
        {
            text.Append(CultureInfo.InvariantCulture, $"{Capitalize(field.Name)}: {field.Value}\r\n");
        }

        bool chunked = body.Length > 0 && Field(request, "content-length") is null;
        text.Append(chunked ? "Transfer-Encoding: chunked\r\n" : string.Empty);
        text.Append(CultureInfo.InvariantCulture, $"X-Forwarded-Proto: http\r\nVia: {AppendVia(Field(request, "via"), "2 nghttpx")}\r\n\r\n");
        byte[] head = Encoding.Latin1.GetBytes(text.ToString());
        return [.. head, .. (chunked ? ToOneChunk(body) : body)];
    }

    private static bool IsForwarded(HeaderField field) => !field.Name.StartsWith(':') && !RequestFieldsNotForwarded.Contains(field.Name);

    private static byte[] ToOneChunk(byte[] body) =>
        [.. Encoding.ASCII.GetBytes(body.Length.ToString("x", CultureInfo.InvariantCulture) + "\r\n"), .. body, .. "\r\n0\r\n\r\n"u8];

    // nghttpx appends its own via entry to the one the message came with.
    private static string AppendVia(string? incoming, string own) => incoming is null ? own : $"{incoming}, {own}";

    /// <summary>
    /// Reads the backend's reply, skipping any <c>1xx</c> interim reply, and turns it into HTTP/2
    /// fields. A reply to <c>HEAD</c> and a <c>204</c> or <c>304</c> have no body.
    /// </summary>
    internal static async Task<Http2ServerReply> ReadReplyAsync(Http2ServerBackendReader reader, bool isHead, CancellationToken cancellationToken)
    {
        (int status, List<HeaderField> fields) = await ReadHeadAsync(reader, cancellationToken).ConfigureAwait(false);
        while (status / 100 == 1)
        {
            (status, fields) = await ReadHeadAsync(reader, cancellationToken).ConfigureAwait(false);
        }

        byte[] body = HasNoBody(isHead, status) ? [] : await ReadBodyAsync(reader, fields, cancellationToken).ConfigureAwait(false);
        return new Http2ServerReply(ToHttp2Fields(status, fields), body);
    }

    private static bool HasNoBody(bool isHead, int status) => isHead || status is 204 or 304;

    // The status line and fields up to the empty line; a close before the status line reads as 502.
    private static async Task<(int Status, List<HeaderField> Fields)> ReadHeadAsync(Http2ServerBackendReader reader, CancellationToken cancellationToken)
    {
        string? statusLine = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
        string[] parts = (statusLine ?? string.Empty).Split(' ', 3);
        int status = parts.Length > 1 && int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out int parsed) ? parsed : 502;
        var fields = new List<HeaderField>();
        while (await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false) is { Length: > 0 } line)
        {
            int colon = line.IndexOf(':', StringComparison.Ordinal);
            if (colon > 0)
            {
                fields.Add(new(line[..colon].Trim().ToLowerInvariant(), line[(colon + 1)..].Trim()));
            }
        }

        return (status, fields);
    }

    private static async Task<byte[]> ReadBodyAsync(Http2ServerBackendReader reader, List<HeaderField> fields, CancellationToken cancellationToken)
    {
        if (Field(fields, "transfer-encoding") is { } encoding && encoding.Contains("chunked", StringComparison.OrdinalIgnoreCase))
        {
            return await ReadChunkedAsync(reader, cancellationToken).ConfigureAwait(false);
        }

        return long.TryParse(Field(fields, "content-length"), NumberStyles.None, CultureInfo.InvariantCulture, out long length)
            ? await reader.ReadBytesAsync(length, cancellationToken).ConfigureAwait(false)
            : await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
    }

    // Chunks until the last (zero-size or unreadable) one, then the trailer lines, which are dropped.
    private static async Task<byte[]> ReadChunkedAsync(Http2ServerBackendReader reader, CancellationToken cancellationToken)
    {
        var body = new MemoryStream();
        while (await ReadChunkSizeAsync(reader, cancellationToken).ConfigureAwait(false) is > 0 and var size)
        {
            body.Write(await reader.ReadBytesAsync(size, cancellationToken).ConfigureAwait(false));
            await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
        }

        while (await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false) is { Length: > 0 })
        {
        }

        return body.ToArray();
    }

    private static async Task<long> ReadChunkSizeAsync(Http2ServerBackendReader reader, CancellationToken cancellationToken)
    {
        string line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false) ?? string.Empty;
        return long.TryParse(line.Split(';')[0].Trim(), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out long size) ? size : 0;
    }

    private static List<HeaderField> ToHttp2Fields(int status, List<HeaderField> fields)
    {
        string? via = Field(fields, "via");
        return
        [
            new(":status", status.ToString(CultureInfo.InvariantCulture)),
            .. fields.Where(f => !ReplyFieldsNotForwarded.Contains(f.Name)),
            new("server", "nghttpx"),
            new("via", AppendVia(via, "1.1 nghttpx")),
        ];
    }

    private static string? Field(IEnumerable<HeaderField> fields, string name) =>
        fields.Where(f => f.Name == name).Select(f => f.Value).FirstOrDefault();

    // nghttpx's http2::capitalize: the first letter and each letter after '-' upper case.
    private static string Capitalize(string name) =>
        string.Create(name.Length, name, static (span, source) =>
        {
            for (int i = 0; i < source.Length; i++)
            {
                span[i] = i == 0 || source[i - 1] == '-' ? char.ToUpperInvariant(source[i]) : source[i];
            }
        });
}
