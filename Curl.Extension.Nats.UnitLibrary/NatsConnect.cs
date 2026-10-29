using System.Net;
using System.Text;
using System.Text.Json;

namespace Curl.Extension.Nats;

/// <summary>Writes the lines the client sends (NATS client protocol, "CONNECT", "PUB", and the SUB, UNSUB, PING and PONG lines).</summary>
internal static class NatsConnect
{
    /// <summary>The <c>name</c> and <c>lang</c> the client gives in CONNECT.</summary>
    public const string ClientName = "curl";

    /// <summary>The <c>version</c> the client gives in CONNECT.</summary>
    public const string ClientVersion = "1.0.0";

    /// <summary>
    /// Gives the CONNECT line with its CRLF: <c>verbose</c> and <c>pedantic</c> off, <c>tls_required</c>
    /// as the URL's scheme asks, protocol 1, and the credentials as <c>user</c> and <c>pass</c>, or as
    /// <c>auth_token</c> when there is a user name and no password.
    /// </summary>
    /// <param name="tlsRequired">Whether the connection is TLS.</param>
    /// <param name="credentials">The user name and password, if any.</param>
    /// <returns>The line's bytes.</returns>
    public static byte[] LineFor(bool tlsRequired, NetworkCredential? credentials)
    {
        var json = new MemoryStream();
        using (var writer = new Utf8JsonWriter(json))
        {
            writer.WriteStartObject();
            writer.WriteBoolean("verbose", false);
            writer.WriteBoolean("pedantic", false);
            writer.WriteBoolean("tls_required", tlsRequired);
            writer.WriteString("name", ClientName);
            writer.WriteString("lang", ClientName);
            writer.WriteString("version", ClientVersion);
            writer.WriteNumber("protocol", 1);
            WriteCredentials(writer, credentials);
            writer.WriteEndObject();
        }

        return [.. "CONNECT "u8, .. json.ToArray(), .. "\r\n"u8];
    }

    /// <summary>Gives a line's bytes with its CRLF.</summary>
    /// <param name="line">The line.</param>
    /// <returns>The bytes.</returns>
    public static byte[] Line(string line) => Encoding.UTF8.GetBytes(line + "\r\n");

    /// <summary>Gives <c>PUB &lt;subject&gt; [reply-to] &lt;#bytes&gt;</c>, the payload and their CRLFs.</summary>
    /// <param name="subject">The subject.</param>
    /// <param name="replyTo">The reply subject, if any.</param>
    /// <param name="payload">The payload.</param>
    /// <returns>The bytes.</returns>
    public static byte[] Publish(string subject, string? replyTo, ReadOnlySpan<byte> payload)
    {
        string head = replyTo is null ? $"PUB {subject} {payload.Length}" : $"PUB {subject} {replyTo} {payload.Length}";
        return [.. Line(head), .. payload, .. "\r\n"u8];
    }

    private static void WriteCredentials(Utf8JsonWriter writer, NetworkCredential? credentials)
    {
        if (credentials is not { UserName.Length: > 0 })
        {
            return;
        }

        if (credentials.Password.Length == 0)
        {
            writer.WriteString("auth_token", credentials.UserName);
            return;
        }

        writer.WriteString("user", credentials.UserName);
        writer.WriteString("pass", credentials.Password);
    }
}
