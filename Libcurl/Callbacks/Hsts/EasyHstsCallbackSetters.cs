using Curl.Api.Callbacks;
using Curl.Api.Codes;
using Curl.Api.Options;
using Libcurl.Options;

namespace Libcurl.Callbacks.Hsts;

/// <summary>
/// The setter rows of <c>CURLOPT_HSTSREADFUNCTION</c> and <c>CURLOPT_HSTSWRITEFUNCTION</c> and their <c>*DATA</c>
/// options (ADR-0573). Each function row takes its delegate (<see cref="HstsReadCallback"/>,
/// <see cref="HstsWriteCallback"/>) or <see langword="null"/> (libcurl's default) and answers
/// <see cref="CurlCode.BadFunctionArgument"/> for any other value. While <c>CURLOPT_HSTS_CTRL</c> enables HSTS or
/// <c>CURLOPT_HSTS</c> names a file, the engine asks the read callback for entries before each perform and hands
/// the write callback every entry at <c>curl_easy_cleanup</c> (ADR-0620). The managed callbacks carry their user
/// data in their closures, so <c>CURLOPT_HSTSREADDATA</c> and <c>CURLOPT_HSTSWRITEDATA</c> take any value and
/// change nothing.
/// </summary>
internal static class EasyHstsCallbackSetters
{
    /// <summary>Adds the HSTS read and write rows to <paramref name="setters"/>.</summary>
    /// <param name="setters">The rows to add to.</param>
    internal static void AddTo(Dictionary<CurlOption, EasyOptionSetter> setters)
    {
        setters[CurlOption.Hstsreadfunction] = new(SetHstsReadFunction);
        setters[CurlOption.Hstswritefunction] = new(SetHstsWriteFunction);
        setters[CurlOption.Hstsreaddata] = new(TakeUserData);
        setters[CurlOption.Hstswritedata] = new(TakeUserData);
    }

    private static CurlCode SetHstsReadFunction(ref EasyOptions options, object? value)
    {
        if (value is not (null or HstsReadCallback))
        {
            return CurlCode.BadFunctionArgument;
        }

        options = options with { Callbacks = options.Callbacks with { HstsRead = (HstsReadCallback?)value } };
        return CurlCode.Ok;
    }

    private static CurlCode SetHstsWriteFunction(ref EasyOptions options, object? value)
    {
        if (value is not (null or HstsWriteCallback))
        {
            return CurlCode.BadFunctionArgument;
        }

        options = options with { Callbacks = options.Callbacks with { HstsWrite = (HstsWriteCallback?)value } };
        return CurlCode.Ok;
    }

    private static CurlCode TakeUserData(ref EasyOptions options, object? value) => CurlCode.Ok;
}
