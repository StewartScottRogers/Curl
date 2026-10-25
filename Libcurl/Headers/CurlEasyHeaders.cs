using System.Runtime.CompilerServices;
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
            : MarkedResult(headers.Find(name, index, origin, request, out header), ref header);
    }

    /// <summary><c>curl_easy_nextheader</c>: the header after <paramref name="previous"/> the handle's last transfer received.</summary>
    /// <param name="easy">The handle.</param>
    /// <param name="origin">The <c>CURLH_*</c> bits to walk.</param>
    /// <param name="request">Which request of the transfer, from 0, or <see cref="ITransferHeaders.LastRequest"/>.</param>
    /// <param name="previous"><c>prev</c>: the header this function answered last, or <see langword="null"/> for the first.</param>
    /// <returns>The next header, or <see langword="null"/> when there are no more or the handle is <see langword="null"/> or cleaned up.</returns>
    public static CurlHeader? NextHeader(this CurlEasy? easy, CurlHeaderOrigin origin, int request, CurlHeader? previous) =>
        Marked(LiveHeaders(easy)?.Next(origin, request, Original(previous)));

    /// <summary>libcurl 8.22.0's marker bit (<c>1 &lt;&lt; 27</c>), which <c>headers.c</c> sets in <c>origin</c> beside the <c>CURLH_*</c> bit.</summary>
    private const CurlHeaderOrigin MarkerBit = (CurlHeaderOrigin)(1 << 27);

    /// <summary>Each header handed to the caller with the marker bit, to the engine's header it was made from, which the engine finds its place by.</summary>
    private static readonly ConditionalWeakTable<CurlHeader, CurlHeader> Originals = new();

    private static CurlHeaderCode MarkedResult(CurlHeaderCode code, ref CurlHeader? header)
    {
        header = Marked(header);
        return code;
    }

    private static CurlHeader? Marked(CurlHeader? engineHeader)
    {
        if (engineHeader is null)
        {
            return null;
        }

        var marked = engineHeader with { Origin = engineHeader.Origin | MarkerBit };
        Originals.Add(marked, engineHeader);
        return marked;
    }

    private static CurlHeader? Original(CurlHeader? previous) =>
        previous is not null && Originals.TryGetValue(previous, out var engineHeader) ? engineHeader : previous;

    /// <summary>The easy handle's recorded headers, or <see langword="null"/> when <paramref name="easy"/> is <see langword="null"/> or cleaned up, which each caller answers as no header.</summary>
    private static ITransferHeaders? LiveHeaders(CurlEasy? easy) =>
        easy is null || easy.IsCleanedUp ? null : easy.Transfer.Headers;
}
