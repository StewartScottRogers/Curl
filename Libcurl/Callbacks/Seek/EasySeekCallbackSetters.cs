using Curl.Api.Callbacks;
using Curl.Api.Codes;
using Curl.Api.Options;
using Libcurl.Options;

namespace Libcurl.Callbacks.Seek;

/// <summary>
/// The setter rows of <c>CURLOPT_SEEKFUNCTION</c> and <c>CURLOPT_SEEKDATA</c> (ADR-0573). The function row takes a
/// <see cref="SeekCallback"/> or <see langword="null"/> (libcurl's default) and answers
/// <see cref="CurlCode.BadFunctionArgument"/> for any other value. The engine calls it as libcurl 8.22.0's
/// <c>cr_in_rewind</c> does: with offset 0 from <see cref="SeekOrigin.Begin"/> when an upload already read from the
/// read callback must be sent again (a followed redirect, an authentication retry), any result but
/// <see cref="SeekResult.Ok"/> failing the transfer with <see cref="CurlCode.SendFailRewind"/> and
/// <c>seek callback returned error &lt;n&gt;</c>. The managed callback carries its user data in its closure, so
/// <c>CURLOPT_SEEKDATA</c> takes any value and changes nothing.
/// </summary>
internal static class EasySeekCallbackSetters
{
    /// <summary>Adds the seek rows to <paramref name="setters"/>.</summary>
    /// <param name="setters">The rows to add to.</param>
    internal static void AddTo(Dictionary<CurlOption, EasyOptionSetter> setters)
    {
        setters[CurlOption.Seekfunction] = new(SetSeekFunction);
        setters[CurlOption.Seekdata] = new(TakeUserData);
    }

    private static CurlCode SetSeekFunction(ref EasyOptions options, object? value)
    {
        if (value is not (null or SeekCallback))
        {
            return CurlCode.BadFunctionArgument;
        }

        options = options with { Callbacks = options.Callbacks with { Seek = (SeekCallback?)value } };
        return CurlCode.Ok;
    }

    private static CurlCode TakeUserData(ref EasyOptions options, object? value) => CurlCode.Ok;
}
