using Curl.Api.Codes;
using Curl.Api.EngineContracts.Headers;
using Libcurl.Easy;

namespace Libcurl.Headers;

/// <summary>
/// libcurl's header API on a <see cref="CurlEasy"/> (<c>include/curl/header.h</c>, ADR-0603):
/// <see cref="Header"/> is <c>curl_easy_header</c> and <see cref="NextHeader"/> is <c>curl_easy_nextheader</c>,
/// each over the engine's <see cref="ITransferHeaders"/>, which answers every <c>CURLHE_*</c> code. A
/// <see langword="null"/> or cleaned-up handle, or a <see langword="null"/> name, fails libcurl's argument check:
/// <see cref="Header"/> answers <see cref="CurlHeaderCode.BadArgument"/> and <see cref="NextHeader"/>
/// <see langword="null"/>, without calling the engine.
/// </summary>
public static class CurlEasyHeaders
{
    /// <summary><c>curl_easy_header</c>: finds one header the handle's last transfer received, by name.</summary>
    /// <param name="easy">The handle.</param>
    /// <param name="name">The header's name, matched without regard to case.</param>
    /// <param name="index">Which header of that name, from 0.</param>
    /// <param name="origin">The <c>CURLH_*</c> bits to search.</param>
    /// <param name="request">Which request of the transfer, from 0, or <see cref="ITransferHeaders.LastRequest"/>.</param>
    /// <param name="header"><c>*hout</c>: the header, or <see langword="null"/> unless the answer is <see cref="CurlHeaderCode.Ok"/>.</param>
    /// <returns>The engine's answer, or <see cref="CurlHeaderCode.BadArgument"/> for a <see langword="null"/> or cleaned-up handle or a <see langword="null"/> name.</returns>
    public static CurlHeaderCode Header(this CurlEasy? easy, string? name, int index, CurlHeaderOrigin origin, int request, out CurlHeader? header)
    {
        header = null;
        var headers = LiveHeaders(easy);
        return headers is null || name is null
            ? CurlHeaderCode.BadArgument
            : headers.TryGet(name, index, origin, request, out header);
    }

    /// <summary><c>curl_easy_nextheader</c>: the header after <paramref name="previous"/> the handle's last transfer received.</summary>
    /// <param name="easy">The handle.</param>
    /// <param name="origin">The <c>CURLH_*</c> bits to walk.</param>
    /// <param name="request">Which request of the transfer, from 0, or <see cref="ITransferHeaders.LastRequest"/>.</param>
    /// <param name="previous"><c>prev</c>: the header this function answered last, or <see langword="null"/> for the first.</param>
    /// <returns>The next header, or <see langword="null"/> when there are no more or the handle is <see langword="null"/> or cleaned up.</returns>
    public static CurlHeader? NextHeader(this CurlEasy? easy, CurlHeaderOrigin origin, int request, CurlHeader? previous) =>
        LiveHeaders(easy)?.Next(origin, request, previous);

    private static ITransferHeaders? LiveHeaders(CurlEasy? easy) =>
        easy is null || easy.IsCleanedUp ? null : easy.Transfer.Headers;
}
