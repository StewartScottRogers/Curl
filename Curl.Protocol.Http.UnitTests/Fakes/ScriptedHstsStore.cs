using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Http.Fakes;

/// <summary>
/// An <see cref="IHstsStore" /> that answers every <see cref="StoreFromResponse" /> call with the
/// same scripted legality and records every call.
/// </summary>
/// <param name="legal">What each call returns: <see langword="false" /> for an illegal header.</param>
public sealed class ScriptedHstsStore(bool legal) : IHstsStore
{
    /// <summary>
    /// Gets every <see cref="StoreFromResponse" /> call made, in order.
    /// </summary>
    public List<(CurlUrl Origin, string HeaderValue, DateTimeOffset Now)> Responses { get; } = [];

    /// <inheritdoc />
    public bool StoreFromResponse(CurlUrl origin, string headerValue, DateTimeOffset now)
    {
        Responses.Add((origin, headerValue, now));
        return legal;
    }
}
