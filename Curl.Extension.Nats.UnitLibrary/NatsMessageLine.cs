using System.Globalization;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.Nats;

/// <summary>
/// A <c>MSG &lt;subject&gt; &lt;sid&gt; [reply-to] &lt;#bytes&gt;</c> line (NATS client protocol, "MSG"):
/// the subject and subscription the message arrived on and the length of the payload that follows.
/// </summary>
/// <param name="Subject">The subject it was published to.</param>
/// <param name="SubscriptionId">The <c>sid</c> of the subscription it matched.</param>
/// <param name="ReplyTo">The reply subject, if the publisher gave one.</param>
/// <param name="PayloadLength">The payload's length in bytes.</param>
internal sealed record NatsMessageLine(string Subject, string SubscriptionId, string? ReplyTo, int PayloadLength)
{
    /// <summary>Reads a MSG line.</summary>
    /// <param name="line">The line, without its CRLF.</param>
    /// <param name="maxPayload">The largest payload length accepted.</param>
    /// <returns>The message line.</returns>
    /// <exception cref="NatsException">The line is not a MSG with four or five fields and a length up to <paramref name="maxPayload"/>.</exception>
    public static NatsMessageLine Parse(string line, long maxPayload)
    {
        string[] parts = line.Split(NatsOperations.Separators, StringSplitOptions.RemoveEmptyEntries);
        return IsMessage(parts) && int.TryParse(parts[^1], NumberStyles.None, CultureInfo.InvariantCulture, out int length) && length <= maxPayload
            ? new NatsMessageLine(parts[1], parts[2], parts.Length == 5 ? parts[3] : null, length)
            : throw new NatsException(CurlExitCode.WeirdServerReply, $"NATS server sent an unexpected line: {line}");
    }

    private static bool IsMessage(string[] parts) =>
        parts.Length is 4 or 5 && NatsOperations.Is(parts[0], NatsOperations.Message);
}
