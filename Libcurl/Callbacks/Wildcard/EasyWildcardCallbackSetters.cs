using Curl.Api.Callbacks;
using Curl.Api.Codes;
using Curl.Api.Options;
using Libcurl.Options;

namespace Libcurl.Callbacks.Wildcard;

/// <summary>
/// The setter rows of <c>CURLOPT_CHUNK_BGN_FUNCTION</c>, <c>CURLOPT_CHUNK_END_FUNCTION</c>,
/// <c>CURLOPT_FNMATCH_FUNCTION</c> and <c>CURLOPT_INTERLEAVEFUNCTION</c> and their <c>*DATA</c> options (ADR-0573).
/// Each function row takes its delegate (<see cref="ChunkBeginCallback"/>, <see cref="ChunkEndCallback"/>,
/// <see cref="FileNameMatchCallback"/>, <see cref="InterleaveCallback"/>) or <see langword="null"/> (libcurl's
/// default) and answers <see cref="CurlCode.BadFunctionArgument"/> for any other value. Under
/// <c>CURLOPT_WILDCARDMATCH</c> the engine calls the match callback for each file of an FTP wildcard URL's listing
/// and the chunk callbacks around each match; it hands each interleaved RTP frame of an RTSP transfer to the
/// interleave callback (ADR-0619). The managed callbacks carry their user data in their closures, so
/// <c>CURLOPT_CHUNK_DATA</c>, <c>CURLOPT_FNMATCH_DATA</c> and <c>CURLOPT_INTERLEAVEDATA</c> take any value and change
/// nothing.
/// </summary>
internal static class EasyWildcardCallbackSetters
{
    /// <summary>Adds the chunk, fnmatch and interleave rows to <paramref name="setters"/>.</summary>
    /// <param name="setters">The rows to add to.</param>
    internal static void AddTo(Dictionary<CurlOption, EasyOptionSetter> setters)
    {
        setters[CurlOption.ChunkBgnFunction] = new(SetChunkBeginFunction);
        setters[CurlOption.ChunkEndFunction] = new(SetChunkEndFunction);
        setters[CurlOption.FnmatchFunction] = new(SetFileNameMatchFunction);
        setters[CurlOption.Interleavefunction] = new(SetInterleaveFunction);
        setters[CurlOption.ChunkData] = new(TakeUserData);
        setters[CurlOption.FnmatchData] = new(TakeUserData);
        setters[CurlOption.Interleavedata] = new(TakeUserData);
    }

    private static CurlCode SetChunkBeginFunction(ref EasyOptions options, object? value)
    {
        if (value is not (null or ChunkBeginCallback))
        {
            return CurlCode.BadFunctionArgument;
        }

        options = options with { Callbacks = options.Callbacks with { ChunkBegin = (ChunkBeginCallback?)value } };
        return CurlCode.Ok;
    }

    private static CurlCode SetChunkEndFunction(ref EasyOptions options, object? value)
    {
        if (value is not (null or ChunkEndCallback))
        {
            return CurlCode.BadFunctionArgument;
        }

        options = options with { Callbacks = options.Callbacks with { ChunkEnd = (ChunkEndCallback?)value } };
        return CurlCode.Ok;
    }

    private static CurlCode SetFileNameMatchFunction(ref EasyOptions options, object? value)
    {
        if (value is not (null or FileNameMatchCallback))
        {
            return CurlCode.BadFunctionArgument;
        }

        options = options with { Callbacks = options.Callbacks with { FileNameMatch = (FileNameMatchCallback?)value } };
        return CurlCode.Ok;
    }

    private static CurlCode SetInterleaveFunction(ref EasyOptions options, object? value)
    {
        if (value is not (null or InterleaveCallback))
        {
            return CurlCode.BadFunctionArgument;
        }

        options = options with { Callbacks = options.Callbacks with { Interleave = (InterleaveCallback?)value } };
        return CurlCode.Ok;
    }

    private static CurlCode TakeUserData(ref EasyOptions options, object? value) => CurlCode.Ok;
}
