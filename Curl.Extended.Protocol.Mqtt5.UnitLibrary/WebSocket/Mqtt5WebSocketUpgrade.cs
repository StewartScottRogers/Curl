using System.Buffers;
using System.Globalization;
using System.Security.Cryptography;
using Curl.Extended.Protocol.Mqtt5.Client;
using Curl.Protocol.Abstractions;
using Curl.WebSocket;

namespace Curl.Extended.Protocol.Mqtt5.WebSocket;

/// <summary>
/// The WebSocket opening handshake (RFC 6455 section 4) for MQTT 5 (OASIS MQTT Version 5.0 section 6):
/// <c>GET /mqtt</c> offering the <c>mqtt</c> subprotocol, and a reply that must accept the key and select it.
/// </summary>
internal static class Mqtt5WebSocketUpgrade
{
    /// <summary>The request path every <c>mqtt5+ws://</c> upgrade asks for (ADR-1054).</summary>
    internal const string RequestPath = "/mqtt";

    /// <summary>The subprotocol section 6 requires.</summary>
    internal const string Subprotocol = "mqtt";

    /// <summary>The longest reply head read before the transfer fails.</summary>
    internal const int LongestHead = 65536;

    private const string DefaultUserAgent = "curl/8.22.0";

    private const int KeyLength = 16;

    private const int ReadSize = 4096;

    private const string AcceptGuid = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";

    private const string AcceptHeader = "Sec-WebSocket-Accept:";

    private const string ProtocolHeader = "Sec-WebSocket-Protocol:";

    private const string UpgradeHeader = "Upgrade:";

    private const string ConnectionHeader = "Connection:";

    private static readonly byte[] HeadEnd = "\r\n\r\n"u8.ToArray();

    /// <summary>Gives a new <c>Sec-WebSocket-Key</c>: sixteen bytes from <paramref name="randomSource"/>, base64 encoded.</summary>
    internal static string NewKey(IWebSocketRandomSource randomSource)
    {
        Span<byte> nonce = stackalloc byte[KeyLength];
        randomSource.Fill(nonce);
        return Convert.ToBase64String(nonce);
    }

    /// <summary>Gives the upgrade request to <paramref name="url"/>'s host with <paramref name="key"/>.</summary>
    /// <param name="url">The <c>mqtt5+ws</c> URL.</param>
    /// <param name="key">The <c>Sec-WebSocket-Key</c>.</param>
    /// <param name="context">The transfer whose <c>-H</c>, <c>-A</c>, <c>-b</c>, <c>-u</c> and <c>--oauth2-bearer</c> add header fields, or <see langword="null"/> for none.</param>
    internal static byte[] Request(CurlUrl url, string key, ITransferContext? context = null)
    {
        string host = url.IdnHost.Contains(':', StringComparison.Ordinal) ? $"[{url.IdnHost}]" : url.IdnHost;
        string port = url.Port < 0 ? string.Empty : ":" + url.Port.ToString(CultureInfo.InvariantCulture);
        List<KeyValuePair<string, string>> fields =
        [
            new("Host", host + port),
            new("Upgrade", "websocket"),
            new("Connection", "Upgrade"),
            new("Sec-WebSocket-Key", key),
            new("Sec-WebSocket-Version", "13"),
            new("Sec-WebSocket-Protocol", Subprotocol),
        ];
        AddOptionFields(fields, context);
        return System.Text.Encoding.ASCII.GetBytes(
            $"GET {RequestPath} HTTP/1.1\r\n" + string.Concat(fields.Select(field => $"{field.Key}: {field.Value}\r\n")) + "\r\n");
    }

    private static void AddOptionFields(List<KeyValuePair<string, string>> fields, ITransferContext? context)
    {
        if (context is null)
        {
            return;
        }

        if (AuthorizationOf(context) is { } authorization)
        {
            fields.Add(new("Authorization", authorization));
        }

        AddUserAgentAndCookie(fields, context.Http);
        foreach (string header in context.Http?.Headers ?? [])
        {
            ApplyCustomHeader(fields, header.Split('\r', '\n')[0]);
        }
    }

    private static void AddUserAgentAndCookie(List<KeyValuePair<string, string>> fields, Curl.Protocol.Abstractions.HttpRequestOptions? http)
    {
        string agent = http?.UserAgent ?? DefaultUserAgent;
        if (agent.Length > 0)
        {
            fields.Add(new("User-Agent", agent));
        }

        if (!string.IsNullOrEmpty(http?.CookieStrings))
        {
            fields.Add(new("Cookie", http.CookieStrings));
        }
    }
    private static string? AuthorizationOf(ITransferContext context)
    {
        if (!string.IsNullOrEmpty(context.Http?.BearerToken))
        {
            return "Bearer " + context.Http.BearerToken;
        }

        return context.Credentials is { } credentials
            ? "Basic " + Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{credentials.UserName}:{credentials.Password}"))
            : null;
    }
    // As curl does: "Name: value" replaces a field of that name (even one the upgrade sets), "Name:" removes it, "Name;" sends it empty.
    private static void ApplyCustomHeader(List<KeyValuePair<string, string>> fields, string header)
    {
        int colon = header.IndexOf(':', StringComparison.Ordinal);
        if (colon > 0)
        {
            string name = header[..colon];
            string value = header[(colon + 1)..].Trim(' ', '\t');
            fields.RemoveAll(field => field.Key.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (value.Length > 0)
            {
                fields.Add(new(name, value));
            }
        }
        else if (header.EndsWith(';') && header.Length > 1)
        {
            fields.Add(new(header[..^1], string.Empty));
        }
    }
    /// <summary>Gives the <c>Sec-WebSocket-Accept</c> a server must answer <paramref name="key"/> with (RFC 6455 section 4.2.2).</summary>
    internal static string AcceptFor(string key) =>
        Convert.ToBase64String(SHA1.HashData(System.Text.Encoding.ASCII.GetBytes(key + AcceptGuid)));

    /// <summary>Sends <paramref name="request"/>; a failed write ends the transfer with a send error.</summary>
    internal static async ValueTask SendRequestAsync(IConnection connection, byte[] request, CancellationToken cancellationToken)
    {
        try
        {
            await connection.WriteAsync(request, cancellationToken).ConfigureAwait(false);
            await connection.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (IOException)
        {
            throw new Mqtt5ClientException(CurlExitCode.SendError, Mqtt5ClientMessages.SendFailed);
        }
    }

    /// <summary>Reads the reply head and checks it accepts the upgrade; gives the bytes after the head, the first frames.</summary>
    internal static async ValueTask<ReadOnlyMemory<byte>> ReadReplyAsync(IConnection connection, string key, CancellationToken cancellationToken)
    {
        ArrayBufferWriter<byte> received = new();
        int headEnd;
        while ((headEnd = received.WrittenSpan.IndexOf(HeadEnd)) < 0)
        {
            if (received.WrittenCount > LongestHead)
            {
                throw new Mqtt5ClientException(CurlExitCode.WeirdServerReply, Mqtt5WebSocketMessages.ReplyHeadTooLong);
            }

            received.Advance(await ReadSomeAsync(connection, received.GetMemory(ReadSize), cancellationToken).ConfigureAwait(false));
        }

        CheckHead(System.Text.Encoding.Latin1.GetString(received.WrittenSpan[..headEnd]), key);
        return received.WrittenMemory[(headEnd + HeadEnd.Length)..].ToArray();
    }

    private static async ValueTask<int> ReadSomeAsync(IConnection connection, Memory<byte> destination, CancellationToken cancellationToken)
    {
        int read;
        try
        {
            read = await connection.ReadAsync(destination, cancellationToken).ConfigureAwait(false);
        }
        catch (IOException)
        {
            throw new Mqtt5ClientException(CurlExitCode.RecvError, Mqtt5ClientMessages.ReceiveFailed);
        }

        return read > 0 ? read : throw new Mqtt5ClientException(CurlExitCode.GotNothing, Mqtt5WebSocketMessages.EmptyReply);
    }

    private static void CheckHead(string head, string key)
    {
        string[] lines = head.Split("\r\n");
        string[] statusLine = lines[0].Split(' ');
        if (statusLine.Length < 2 || !statusLine[0].StartsWith("HTTP/", StringComparison.Ordinal))
        {
            throw new Mqtt5ClientException(CurlExitCode.WeirdServerReply, Mqtt5WebSocketMessages.NoStatusLine);
        }

        if (statusLine[1] != "101")
        {
            throw new Mqtt5ClientException(CurlExitCode.HttpReturnedError, Mqtt5WebSocketMessages.RefusedUpgrade(statusLine[1]));
        }

        CheckUpgradeHeaders(lines);

        if (!HasHeader(lines, AcceptHeader, AcceptFor(key)))
        {
            throw new Mqtt5ClientException(CurlExitCode.WeirdServerReply, Mqtt5WebSocketMessages.AcceptMismatch);
        }

        if (!HasHeader(lines, ProtocolHeader, Subprotocol))
        {
            throw new Mqtt5ClientException(CurlExitCode.WeirdServerReply, Mqtt5WebSocketMessages.SubprotocolNotSelected);
        }
    }

    private static void CheckUpgradeHeaders(string[] lines)
    {
        if (!lines.Any(line => HasValue(line, UpgradeHeader, value => value.Equals("websocket", StringComparison.OrdinalIgnoreCase))))
        {
            throw new Mqtt5ClientException(CurlExitCode.WeirdServerReply, Mqtt5WebSocketMessages.NoUpgradeWebsocket);
        }

        if (!lines.Any(line => HasValue(line, ConnectionHeader, value => value.Split(",").Any(token => token.Trim().Equals("Upgrade", StringComparison.OrdinalIgnoreCase)))))
        {
            throw new Mqtt5ClientException(CurlExitCode.WeirdServerReply, Mqtt5WebSocketMessages.NoConnectionUpgrade);
        }
    }

    private static bool HasValue(string line, string name, Func<string, bool> matches) =>
        line.StartsWith(name, StringComparison.OrdinalIgnoreCase) && matches(line[name.Length..].Trim());

    private static bool HasHeader(string[] lines, string name, string value) =>
        lines.Any(line => HasValue(line, name, found => found == value));
}
