using System.Globalization;
using Curl.Api.Codes;
using Curl.Engine;
using Curl.Protocol.Abstractions;
using Libcurl.Easy;

namespace Curl.Console;

/// <summary>
/// One serial transfer's easy handle (ADR-0624): the <c>Libcurl</c> <see cref="CurlEasy" /> the runner sets
/// options on and performs, and the engine's <see cref="IToolEasyTransfer" /> under it, which carries the
/// curl tool's own hooks and the attempt's whole result.
/// </summary>
/// <param name="Easy">The handle <c>curl_easy_init</c> returned.</param>
/// <param name="Hooks">The engine transfer the handle wraps.</param>
internal sealed record ToolEasyHandle(CurlEasy Easy, IToolEasyTransfer Hooks) : IAsyncDisposable
{
    /// <summary>
    /// Makes <paramref name="setopts" />' <c>curl_easy_setopt</c> calls in order, stopping at the first that does not
    /// answer <c>CURLE_OK</c>, as the curl tool's <c>config2setopts</c> stops (BL-2491, ADR-0631).
    /// </summary>
    /// <param name="setopts">The calls to make.</param>
    /// <returns>
    /// <see langword="null" /> when every call answered <c>CURLE_OK</c>; else the transfer's failure, with the
    /// call's code as the exit code and curl's <c>setopt 0x&lt;option&gt; got bad argument</c> as its message.
    /// </returns>
    internal TransferResult? SetOptions(IEnumerable<EasySetopt> setopts)
    {
        foreach (EasySetopt setopt in setopts)
        {
            CurlCode code = Easy.SetOption(setopt.Option, setopt.Value);
            if (code != CurlCode.Ok)
            {
                return TransferResult.Failure((CurlExitCode)(int)code, string.Create(CultureInfo.InvariantCulture, $"setopt 0x{(int)setopt.Option:x} got bad argument"));
            }
        }

        return null;
    }

    /// <summary><c>curl_easy_cleanup</c>: disposes the handle, and with it the engine transfer.</summary>
    /// <returns>A task that completes once the handle is cleaned up.</returns>
    public ValueTask DisposeAsync() => Easy.DisposeAsync();
}
