using System.Text.Json;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.Protocol.Nats;

/// <summary>
/// The fields of a server's <c>INFO</c> the client acts on (NATS client protocol, "INFO"): whether it
/// requires TLS and the largest payload it accepts.
/// </summary>
/// <param name="TlsRequired">Whether <c>tls_required</c> is true.</param>
/// <param name="MaxPayload">The <c>max_payload</c>, or <see cref="DefaultMaxPayload"/> when absent.</param>
internal sealed record NatsServerInfo(bool TlsRequired, long MaxPayload)
{
    /// <summary>The server's default <c>max_payload</c>, 1 MB, used when INFO leaves it out.</summary>
    public const long DefaultMaxPayload = 1048576;

    /// <summary>Reads an <c>INFO {json}</c> line.</summary>
    /// <param name="line">The line, without its CRLF.</param>
    /// <returns>The fields.</returns>
    /// <exception cref="NatsException">The line is not INFO, or its JSON is not an object.</exception>
    public static NatsServerInfo Parse(string line)
    {
        string[] parts = line.Split(NatsOperations.Separators, 2, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2 || !NatsOperations.Is(parts[0], NatsOperations.Info))
        {
            throw new NatsException(CurlExitCode.WeirdServerReply, NatsMessages.NoInfo);
        }

        try
        {
            using var document = JsonDocument.Parse(parts[1]);
            return document.RootElement.ValueKind == JsonValueKind.Object
                ? Of(document.RootElement)
                : throw new NatsException(CurlExitCode.WeirdServerReply, NatsMessages.BadInfo);
        }
        catch (JsonException)
        {
            throw new NatsException(CurlExitCode.WeirdServerReply, NatsMessages.BadInfo);
        }
    }

    private static NatsServerInfo Of(JsonElement info) =>
        new(IsTrue(info, "tls_required"), MaxPayloadOf(info));

    private static bool IsTrue(JsonElement info, string name) =>
        info.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.True;

    private static long MaxPayloadOf(JsonElement info) =>
        info.TryGetProperty("max_payload", out JsonElement value)
        && value.ValueKind == JsonValueKind.Number
        && value.TryGetInt64(out long maxPayload)
        && maxPayload > 0
            ? maxPayload
            : DefaultMaxPayload;
}
