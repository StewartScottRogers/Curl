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
    public List<(CurlUrl Url, bool Secure, DateTimeOffset Now)> Requests { get; } = [];

    /// <summary>
    /// Gets every <see cref="StoreFromResponse" /> call made, in order.
    /// </summary>
    public List<(CurlUrl Url, IReadOnlyList<string> SetCookieHeaders, DateTimeOffset Now)> Responses { get; } = [];

    /// <inheritdoc />
    public string? GetCookieHeader(CurlUrl url, bool secure, DateTimeOffset now)
    {
        Requests.Add((url, secure, now));
        return answers.TryDequeue(out string? answer) ? answer : null;
    }

    /// <inheritdoc />
    public void StoreFromResponse(CurlUrl url, IReadOnlyList<string> setCookieHeaders, DateTimeOffset now) =>
        Responses.Add((url, setCookieHeaders, now));
}
