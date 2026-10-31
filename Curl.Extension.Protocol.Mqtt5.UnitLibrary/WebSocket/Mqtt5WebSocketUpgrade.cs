using System.Buffers;
using System.Globalization;
using System.Security.Cryptography;
using Curl.Extension.Protocol.Mqtt5.Client;
using Curl.Protocol.Abstractions;
using Curl.WebSocket;

namespace Curl.Extension.Protocol.Mqtt5.WebSocket;

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

    private const int KeyLength = 16;

    private const int ReadSize = 4096;

    private const string AcceptGuid = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";

    private const string AcceptHeader = "Sec-WebSocket-Accept:";

    private const string ProtocolHeader = "Sec-WebSocket-Protocol:";

    private static readonly byte[] HeadEnd = "\r\n\r\n"u8.ToArray();

    /// <summary>Gives a new <c>Sec-WebSocket-Key</c>: sixteen bytes from <paramref name="randomSource"/>, base64 encoded.</summary>
    internal static string NewKey(IWebSocketRandomSource randomSource)
    {
        Span<byte> nonce = stackalloc byte[KeyLength];
        randomSource.Fill(nonce);
        return Convert.ToBase64String(nonce);
    }

    /// <summary>Gives the upgrade request to <paramref name="url"/>'s host with <paramref name="key"/>.</summary>
    internal static byte[] Request(CurlUrl url, string key)
    {
        string host = url.IdnHost.Contains(':', StringComparison.Ordinal) ? $"[{url.IdnHost}]" : url.IdnHost;
        string port = url.Port < 0 ? string.Empty : ":" + url.Port.ToString(CultureInfo.InvariantCulture);
        return System.Text.Encoding.ASCII.GetBytes(
            $"GET {RequestPath} HTTP/1.1\r\n" +
            $"Host: {host}{port}\r\n" +
            "Upgrade: websocket\r\n" +
            "Connection: Upgrade\r\n" +
            $"Sec-WebSocket-Key: {key}\r\n" +
            "Sec-WebSocket-Version: 13\r\n" +
            $"Sec-WebSocket-Protocol: {Subprotocol}\r\n\r\n");
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

        if (!HasHeader(lines, AcceptHeader, AcceptFor(key)))
        {
            throw new Mqtt5ClientException(CurlExitCode.WeirdServerReply, Mqtt5WebSocketMessages.AcceptMismatch);
        }

        if (!HasHeader(lines, ProtocolHeader, Subprotocol))
        {
            throw new Mqtt5ClientException(CurlExitCode.WeirdServerReply, Mqtt5WebSocketMessages.SubprotocolNotSelected);
        }
    }

    private static bool HasHeader(string[] lines, string name, string value) =>
        lines.Any(line => line.StartsWith(name, StringComparison.OrdinalIgnoreCase) && line[name.Length..].Trim() == value);
}
