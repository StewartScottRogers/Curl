using System.Globalization;
using Curl.Protocol.Abstractions;

namespace Curl.Extended.Protocol.Nats;

/// <summary>
/// What a NATS URL and <c>-d</c> ask for (ADR-1065): the subject, the payload, and the URL query's
/// fields, <c>request</c> to wait for one reply and <c>count=N</c> to end a subscription after N messages.
/// </summary>
/// <param name="Subject">The subject the URL's path names.</param>
/// <param name="Payload">The <c>-d</c> data, if any.</param>
/// <param name="IsRequest">Whether the query holds <c>request</c>.</param>
/// <param name="MaxMessages">The query's <c>count=N</c>, if any; a subscription ends after that many messages.</param>
internal sealed record NatsRequest(string Subject, ReadOnlyMemory<byte>? Payload, bool IsRequest, int? MaxMessages)
{
    private const string RequestField = "request";

    private const string CountField = "count=";

    /// <summary>Gets what the transfer does.</summary>
    public NatsRequestKind Kind =>
        IsRequest ? NatsRequestKind.Request : Payload is null ? NatsRequestKind.Subscribe : NatsRequestKind.Publish;

    /// <summary>Reads a URL and its <c>-d</c> data.</summary>
    /// <param name="url">The URL.</param>
    /// <param name="postData">The <c>-d</c> data, if any.</param>
    /// <returns>The request.</returns>
    /// <exception cref="NatsException">The subject is not valid, or a query field is neither <c>request</c> nor <c>count=N</c>.</exception>
    public static NatsRequest Of(CurlUrl url, ReadOnlyMemory<byte>? postData)
    {
        string subject = NatsSubject.Of(url);
        bool isRequest = false;
        int? maxMessages = null;
        foreach (string field in (url.Query ?? string.Empty).Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            if (field == RequestField)
            {
                isRequest = true;
                continue;
            }

            maxMessages = CountOf(field);
        }

        return new NatsRequest(subject, postData, isRequest, maxMessages);
    }

    private static int CountOf(string field) =>
        field.StartsWith(CountField, StringComparison.Ordinal)
        && int.TryParse(field.AsSpan(CountField.Length), NumberStyles.None, CultureInfo.InvariantCulture, out int count)
        && count > 0
            ? count
            : throw new NatsException(CurlExitCode.UrlMalformat, $"NATS URL query field \"{field}\" is neither request nor count=N");
}
