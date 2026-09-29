using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Http.Fakes;

/// <summary>
/// An <see cref="IAltSvcStore" /> that answers every <see cref="StoreFromResponse" /> call with
/// the same scripted alternatives and records every call.
/// </summary>
/// <param name="added">The alternatives each call reports added.</param>
public sealed class ScriptedAltSvcStore(params AltSvcAlternative[] added) : IAltSvcStore
{
    /// <summary>
    /// Gets every <see cref="StoreFromResponse" /> call made, in order.
    /// </summary>
    public List<(CurlUrl Origin, string AltSvcHeader, DateTimeOffset Now)> Responses { get; } = [];

    /// <inheritdoc />
    public IReadOnlyList<AltSvcAlternative> StoreFromResponse(CurlUrl origin, string altSvcHeader, DateTimeOffset now)
    {
        Responses.Add((origin, altSvcHeader, now));
        return added;
    }
}
