using System.Globalization;
using Curl.Protocol.Abstractions;

namespace Curl.Extended.Protocol.Nats;

/// <summary>
/// A <c>MSG &lt;subject&gt; &lt;sid&gt; [reply-to] &lt;#bytes&gt;</c> or
/// <c>HMSG &lt;subject&gt; &lt;sid&gt; [reply-to] &lt;#header bytes&gt; &lt;#total bytes&gt;</c> line (NATS client protocol,
/// "MSG" and "HMSG"): the subject and subscription the message arrived on and the lengths of what follows.
/// </summary>
/// <param name="Subject">The subject it was published to.</param>
/// <param name="SubscriptionId">The <c>sid</c> of the subscription it matched.</param>
/// <param name="ReplyTo">The reply subject, if the publisher gave one.</param>
/// <param name="PayloadLength">The payload's length in bytes, not counting the headers.</param>
/// <param name="HeaderLength">The headers' length in bytes: 0 for a MSG.</param>
internal sealed record NatsMessageLine(string Subject, string SubscriptionId, string? ReplyTo, int PayloadLength, int HeaderLength = 0)
{
    /// <summary>Reads a MSG or HMSG line.</summary>
    /// <param name="line">The line, without its CRLF.</param>
    /// <param name="maxPayload">The largest total length (headers and payload) accepted.</param>
    /// <returns>The message line.</returns>
    /// <exception cref="NatsException">The line is not a MSG with four or five fields, or an HMSG with five or six, and a length up to <paramref name="maxPayload"/>.</exception>
    public static NatsMessageLine Parse(string line, long maxPayload)
    {
        string[] parts = line.Split(NatsOperations.Separators, StringSplitOptions.RemoveEmptyEntries);
        bool hasHeaders = NatsOperations.Is(parts.FirstOrDefault() ?? string.Empty, NatsOperations.HeaderMessage);
        int fixedFields = hasHeaders ? 5 : 4;
        return IsMessage(parts, hasHeaders, fixedFields)
            && TryReadLengths(parts, hasHeaders, maxPayload, out int headerLength, out int total)
            ? new NatsMessageLine(parts[1], parts[2], parts.Length == fixedFields + 1 ? parts[3] : null, total - headerLength, headerLength)
            : throw new NatsException(CurlExitCode.WeirdServerReply, $"NATS server sent an unexpected line: {line}");
    }

    private static bool IsMessage(string[] parts, bool hasHeaders, int fixedFields) =>
        parts.Length >= fixedFields && parts.Length <= fixedFields + 1 && (hasHeaders || NatsOperations.Is(parts[0], NatsOperations.Message));

    private static bool TryReadLengths(string[] parts, bool hasHeaders, long maxPayload, out int headerLength, out int total)
    {
        headerLength = 0;
        total = 0;
        return (!hasHeaders || int.TryParse(parts[^2], NumberStyles.None, CultureInfo.InvariantCulture, out headerLength))
            && int.TryParse(parts[^1], NumberStyles.None, CultureInfo.InvariantCulture, out total)
            && total <= maxPayload
            && headerLength <= total;
    }
}
