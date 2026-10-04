using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Http.Fakes;

/// <summary>
/// An <see cref="IAltSvcStore" /> that answers every <see cref="StoreFromResponse" /> call with
/// the same scripted outcomes and records every call.
/// </summary>
/// <param name="outcomes">The outcomes each call reports, in order.</param>
public sealed class ScriptedAltSvcStore(IReadOnlyList<AltSvcHeaderOutcome> outcomes) : IAltSvcStore
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ScriptedAltSvcStore" /> class whose every call
    /// reports <paramref name="added" /> added.
    /// </summary>
    /// <param name="added">The alternatives each call reports added.</param>
    public ScriptedAltSvcStore(params AltSvcAlternative[] added)
        : this([.. added.Select(AltSvcHeaderOutcome.Adding)])
    {
    }

    /// <summary>
    /// Gets every <see cref="StoreFromResponse" /> call made, in order.
    /// </summary>
    public List<(CurlUrl Origin, string AltSvcHeader, Version ResponseVersion, DateTimeOffset Now)> Responses { get; } = [];

    /// <inheritdoc />
    public IReadOnlyList<AltSvcHeaderOutcome> StoreFromResponse(CurlUrl origin, string altSvcHeader, Version responseVersion, DateTimeOffset now)
    {
        Responses.Add((origin, altSvcHeader, responseVersion, now));
        return outcomes;
    }
}
