using System.Globalization;
using System.Text;
using Curl.Conformance.Http2Server;
using Curl.Http2;
using Curl.Protocol.Abstractions;

namespace Curl.Conformance.Http3Server;

/// <summary>
/// Relays one HTTP/3 request to the backend as HTTP/1.1, as upstream's nghttpx-h3 does in front
/// of <c>sws</c> (cases 2500 and 2501 at <c>curl-8_22_0</c>): the request line in absolute form
/// (<c>GET https://localhost:port/path HTTP/1.1</c>), <c>Host</c> from <c>:authority</c>, the other
/// fields with capitalised names, then <c>Via: 3 nghttpx</c> and no <c>X-Forwarded-Proto</c>. The
/// reply is read as the h2c stand-in reads it, into fields with <c>server: nghttpx</c> and
/// <c>via: 1.1 nghttpx</c>.
/// </summary>
internal static class Http3ServerRequestRelay
{
    // Hop-by-hop fields nghttpx never forwards, and fields it writes itself.
    private static readonly HashSet<string> RequestFieldsNotForwarded =
        ["connection", "keep-alive", "proxy-connection", "transfer-encoding", "upgrade", "te", "host", "via"];

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
            return await Http2ServerRequestRelay.ReadReplyAsync(new Http2ServerBackendReader(connection), Field(request, ":method") == "HEAD", cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Formats the HTTP/1.1 request nghttpx-h3 sends for an HTTP/3 request: a body with no
    /// <c>content-length</c> goes as one chunk.
    /// </summary>
    internal static byte[] FormatRequest(IReadOnlyList<HeaderField> request, byte[] body)
    {
        var text = new StringBuilder(RequestLineAndHost(request));
        foreach (HeaderField field in request.Where(IsForwarded))
        {
            text.Append(CultureInfo.InvariantCulture, $"{Capitalize(field.Name)}: {field.Value}\r\n");
        }

        bool chunked = body.Length > 0 && Field(request, "content-length") is null;
        text.Append(chunked ? "Transfer-Encoding: chunked\r\n" : string.Empty);
        text.Append(CultureInfo.InvariantCulture, $"Via: {ViaPrefix(Field(request, "via"))}3 nghttpx\r\n\r\n");
        byte[] head = Encoding.Latin1.GetBytes(text.ToString());
        return [.. head, .. (chunked ? ToOneChunk(body) : body)];
    }

    // The absolute-form request line and the Host field, both from :authority.
    private static string RequestLineAndHost(IReadOnlyList<HeaderField> request)
    {
        string authority = Field(request, ":authority") ?? string.Empty;
        return $"{Field(request, ":method")} {Field(request, ":scheme")}://{authority}{Field(request, ":path")} HTTP/1.1\r\nHost: {authority}\r\n";
    }

    // nghttpx appends its own via entry to the one the request came with.
    private static string ViaPrefix(string? incoming) => incoming is null ? string.Empty : incoming + ", ";

    private static bool IsForwarded(HeaderField field) => !field.Name.StartsWith(':') && !RequestFieldsNotForwarded.Contains(field.Name);

    private static byte[] ToOneChunk(byte[] body) =>
        [.. Encoding.ASCII.GetBytes(body.Length.ToString("x", CultureInfo.InvariantCulture) + "\r\n"), .. body, .. "\r\n0\r\n\r\n"u8];

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
