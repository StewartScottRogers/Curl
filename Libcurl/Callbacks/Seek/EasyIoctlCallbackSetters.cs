using Curl.Api.Callbacks;
using Curl.Api.Codes;
using Curl.Api.Options;
using Libcurl.Options;

namespace Libcurl.Callbacks.Seek;

/// <summary>
/// The setter rows of <c>CURLOPT_IOCTLFUNCTION</c> and <c>CURLOPT_IOCTLDATA</c> (BL-2607), the older way to rewind an
/// upload. The function row takes an <see cref="IoctlCallback"/> or <see langword="null"/> (libcurl's default) and
/// answers <see cref="CurlCode.BadFunctionArgument"/> for any other value. The engine calls it as libcurl 8.22.0's
/// <c>cr_in_rewind</c> does when no <c>CURLOPT_SEEKFUNCTION</c> is set: with
/// <see cref="IoctlCommand.RestartRead"/> when an upload already read from the read callback must be sent again,
/// any result but <see cref="IoctlResult.Ok"/> failing the transfer with <see cref="CurlCode.SendFailRewind"/> and
/// <c>ioctl callback returned error &lt;n&gt;</c>. The managed callback carries its user data in its closure, so
/// <c>CURLOPT_IOCTLDATA</c> takes any value and changes nothing.
/// </summary>
internal static class EasyIoctlCallbackSetters
{
    /// <summary>Adds the ioctl rows to <paramref name="setters"/>.</summary>
    /// <param name="setters">The rows to add to.</param>
    internal static void AddTo(Dictionary<CurlOption, EasyOptionSetter> setters)
    {
        setters[CurlOption.Ioctlfunction] = new(SetIoctlFunction);
        setters[CurlOption.Ioctldata] = new(TakeUserData);
    }

    private static CurlCode SetIoctlFunction(ref EasyOptions options, object? value)
    {
        if (value is not (null or IoctlCallback))
        {
            return CurlCode.BadFunctionArgument;
        }

        options = options with { Callbacks = options.Callbacks with { Ioctl = (IoctlCallback?)value } };
        return CurlCode.Ok;
    }

    private static CurlCode TakeUserData(ref EasyOptions options, object? value) => CurlCode.Ok;
}
