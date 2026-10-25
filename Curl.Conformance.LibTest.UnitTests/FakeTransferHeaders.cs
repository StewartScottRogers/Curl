using Curl.Api.Codes;
using Curl.Api.EngineContracts.Headers;

namespace Curl.Conformance.LibTest.UnitTests;

/// <summary>
/// An <see cref="ITransferHeaders"/> over fixed requests, each a list of headers: a name is matched without regard to
/// case among the request's headers from the asked origins, <see cref="CurlHeaderOrigin.None"/> answers
/// <see cref="CurlHeaderCode.BadArgument"/>, and <see cref="ITransferHeaders.LastRequest"/> is the last request.
/// <see cref="Forced"/> makes every lookup answer that code instead, and <see cref="OkWithoutHeader"/> makes a found
/// header come back as <see langword="null"/>.
/// </summary>
internal sealed class FakeTransferHeaders(params CurlHeader[][] requests) : ITransferHeaders
{
    public CurlHeaderCode? Forced { get; init; }

    public bool OkWithoutHeader { get; init; }

    public List<int> RequestsAsked { get; } = [];

    public CurlHeaderCode Find(string name, int index, CurlHeaderOrigin origin, int request, out CurlHeader? header)
    {
        header = null;
        RequestsAsked.Add(request);
        if (Forced is { } forced)
        {
            return forced;
        }

        if (origin == CurlHeaderOrigin.None)
        {
            return CurlHeaderCode.BadArgument;
        }

        CurlHeader[] matches = [.. From(origin, request).Where(h => string.Equals(h.Name, name, StringComparison.OrdinalIgnoreCase))];
        if (index >= matches.Length)
        {
            return matches.Length == 0 ? CurlHeaderCode.Missing : CurlHeaderCode.BadIndex;
        }

        header = OkWithoutHeader ? null : matches[index];
        return CurlHeaderCode.Ok;
    }

    public CurlHeader? Next(CurlHeaderOrigin origin, int request, CurlHeader? previous)
    {
        List<CurlHeader> all = [.. From(origin, request)];
        int at = previous is null ? 0 : all.IndexOf(previous) + 1;
        return at < all.Count ? all[at] : null;
    }

    private IEnumerable<CurlHeader> From(CurlHeaderOrigin origin, int request)
    {
        int at = request == ITransferHeaders.LastRequest ? requests.Length - 1 : request;
        return at < requests.Length ? requests[at].Where(h => (h.Origin & origin) != 0) : [];
    }
}
