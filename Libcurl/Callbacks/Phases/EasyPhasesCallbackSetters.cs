using Curl.Api.Callbacks;
using Curl.Api.Codes;
using Curl.Api.Options;
using Libcurl.Options;

namespace Libcurl.Callbacks.Phases;

/// <summary>
/// The setter rows of <c>CURLOPT_PREREQFUNCTION</c> and <c>CURLOPT_RESOLVER_START_FUNCTION</c> and their <c>*DATA</c>
/// options (ADR-0573). Each function row takes its delegate (<see cref="PrerequestCallback"/>,
/// <see cref="ResolverStartCallback"/>) or <see langword="null"/> (libcurl's default) and answers
/// <see cref="CurlCode.BadFunctionArgument"/> for any other value. The engine calls the prerequest callback each time
/// a request is given its connection, before anything is sent, <see cref="PrerequestResult.Abort"/> failing with
/// <see cref="CurlCode.AbortedByCallback"/>; and the resolver-start callback before each name look-up its DNS cache
/// does not answer, any answer but 0 failing with <see cref="CurlCode.CouldntResolveHost"/> (ADR-0611). The managed
/// callbacks carry their user data in their closures, so the <c>*DATA</c> options take any value and change nothing.
/// <c>CURLOPT_TRAILERFUNCTION</c> joins them once the engine sends request trailers (BL-2475).
/// </summary>
internal static class EasyPhasesCallbackSetters
{
    /// <summary>Adds the prerequest and resolver-start rows to <paramref name="setters"/>.</summary>
    /// <param name="setters">The rows to add to.</param>
    internal static void AddTo(Dictionary<CurlOption, EasyOptionSetter> setters)
    {
        setters[CurlOption.Prereqfunction] = new(SetPrerequestFunction);
        setters[CurlOption.ResolverStartFunction] = new(SetResolverStartFunction);
        setters[CurlOption.Prereqdata] = new(TakeUserData);
        setters[CurlOption.ResolverStartData] = new(TakeUserData);
    }

    private static CurlCode SetPrerequestFunction(ref EasyOptions options, object? value)
    {
        if (value is not (null or PrerequestCallback))
        {
            return CurlCode.BadFunctionArgument;
        }

        options = options with { Callbacks = options.Callbacks with { Prerequest = (PrerequestCallback?)value } };
        return CurlCode.Ok;
    }

    private static CurlCode SetResolverStartFunction(ref EasyOptions options, object? value)
    {
        if (value is not (null or ResolverStartCallback))
        {
            return CurlCode.BadFunctionArgument;
        }

        options = options with { Callbacks = options.Callbacks with { ResolverStart = (ResolverStartCallback?)value } };
        return CurlCode.Ok;
    }

    private static CurlCode TakeUserData(ref EasyOptions options, object? value) => CurlCode.Ok;
}
