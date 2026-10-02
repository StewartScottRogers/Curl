using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Http.Fakes;

/// <summary>
/// An <see cref="ICookieStore" /> that answers each <see cref="GetCookieHeader" /> call with
/// the next scripted value, <see langword="null" /> once they run out, and records every call.
/// </summary>
/// <param name="cookieHeaders">The <c>Cookie</c> values to give, one per request, in order.</param>
public sealed class ScriptedCookieStore(params string?[] cookieHeaders) : ICookieStore
{
    private readonly Queue<string?> answers = new(cookieHeaders);

    /// <summary>
    /// Gets every <see cref="GetCookieHeader" /> call made, in order.
    /// </summary>
    public List<(CurlUrl Url, bool Secure, DateTimeOffset Now, ITransferEvents Events)> Requests { get; } = [];

    /// <summary>
    /// Gets or sets the <c>-v</c> line each <see cref="GetCookieHeader" /> call reports;
    /// <see langword="null" /> reports nothing.
    /// </summary>
    public string? RequestLine { get; set; }

    /// <summary>
    /// Gets every <see cref="StoreFromResponse" /> call made, in order.
    /// </summary>
    public List<(CurlUrl Url, string SetCookieHeader, int StoredFromResponse, DateTimeOffset Now, ITransferEvents Events)> Responses { get; } = [];

    /// <summary>
    /// Gets or sets the <c>-v</c> line each <see cref="StoreFromResponse" /> call reports, made
    /// from the header it was given; <see langword="null" /> reports nothing.
    /// </summary>
    public Func<string, string>? ReportedLine { get; set; }

    /// <inheritdoc />
    public string? GetCookieHeader(CurlUrl url, bool secure, DateTimeOffset now, ITransferEvents events)
    {
        Requests.Add((url, secure, now, events));
        if (RequestLine is { } line)
        {
            events.ReportInfo(line);
        }

        return answers.TryDequeue(out string? answer) ? answer : null;
    }

    /// <inheritdoc />
    public int StoreFromResponse(CurlUrl url, string setCookieHeader, int storedFromResponse, DateTimeOffset now, ITransferEvents events)
    {
        Responses.Add((url, setCookieHeader, storedFromResponse, now, events));
        if (ReportedLine is { } line)
        {
            events.ReportInfo(line(setCookieHeader));
        }

        return storedFromResponse + 1;
    }
}
