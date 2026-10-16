using Curl.Api.Codes;
using Curl.Api.Info;
using Curl.Api.Options;

namespace Libcurl.Info;

/// <summary>
/// <c>curl_easy_getinfo</c>'s dispatch: checks the info is one upstream's <c>curl.h</c> names and
/// hands the handle's <see cref="TransferInfo"/> and options to the info group's reader.
/// </summary>
/// <param name="readers">The readers to dispatch through, by info.</param>
internal sealed class EasyInfoDispatch(IReadOnlyDictionary<CurlInfo, EasyInfoReader> readers)
{
    /// <summary>The dispatch over every reader the info groups have added (<see cref="EasyInfoReaders"/>).</summary>
    internal static EasyInfoDispatch Default { get; } = new(EasyInfoReaders.Create());

    /// <summary>Reads <paramref name="info"/> from a handle.</summary>
    /// <param name="info">The info to read.</param>
    /// <param name="transferInfo">What the handle's last transfer reported.</param>
    /// <param name="options">The handle's options.</param>
    /// <param name="value">The value read, or <see langword="null"/> when the answer is not <see cref="CurlCode.Ok"/>.</param>
    /// <returns>
    /// <see cref="CurlCode.UnknownOption"/> for an info <c>curl.h</c> lacks, as <c>curl_easy_getinfo</c>
    /// answers, <see cref="CurlCode.NotBuiltIn"/> for one no group reads yet, else <see cref="CurlCode.Ok"/>.
    /// </returns>
    internal CurlCode GetInfo(CurlInfo info, TransferInfo transferInfo, EasyOptions options, out object? value)
    {
        value = null;
        if (!Enum.IsDefined(info))
        {
            return CurlCode.UnknownOption;
        }

        if (!readers.TryGetValue(info, out var reader))
        {
            return CurlCode.NotBuiltIn;
        }

        value = reader(transferInfo, options);
        return CurlCode.Ok;
    }
}
