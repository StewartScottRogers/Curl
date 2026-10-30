using System.Globalization;
using Curl.Api.Codes;
using Curl.Engine;
using Curl.Protocol.Abstractions;
using Libcurl.Easy;
using Libcurl.Options;

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
    // The calls whose CURLE_NOT_BUILT_IN curl's config2setopts ignores and carries on past (ADR-0975, ADR-1036).
    private static readonly HashSet<CurlOption> IgnoredWhenNotBuiltIn =
    [
        CurlOption.Capath,
        CurlOption.ProxyCapath,
        CurlOption.Tls13Ciphers,
        CurlOption.ProxyTls13Ciphers,
        CurlOption.Crlfile,
        CurlOption.ProxyCrlfile,
        CurlOption.SslEcCurves,
        CurlOption.SslSignatureAlgorithms,
        CurlOption.TcpFastopen,
    ];

    /// <summary>
    /// Makes <paramref name="setopts" />' <c>curl_easy_setopt</c> calls on the handle, as
    /// <see cref="SetOptions(IEnumerable{EasySetopt}, Func{EasySetopt, CurlCode})" /> makes them.
    /// </summary>
    /// <param name="setopts">The calls to make.</param>
    /// <returns><see langword="null" />, or the transfer's failure.</returns>
    internal TransferResult? SetOptions(IEnumerable<EasySetopt> setopts) => SetOptions(setopts, s => Easy.SetOption(s.Option, s.Value));

    /// <summary>
    /// Makes <paramref name="setopts" />' <c>curl_easy_setopt</c> calls through <paramref name="setOption" /> in order,
    /// stopping at the first that does not answer <c>CURLE_OK</c>, as the curl tool's <c>config2setopts</c> stops
    /// (BL-2491, ADR-0631) - except a <c>CURLE_NOT_BUILT_IN</c> answer to <c>CURLOPT_CAPATH</c>,
    /// <c>CURLOPT_PROXY_CAPATH</c>, <c>CURLOPT_TLS13_CIPHERS</c> or <c>CURLOPT_PROXY_TLS13_CIPHERS</c>, which curl
    /// ignores and carries on past. Its warning is one of the run's lines before each transfer
    /// (<see cref="CurlComposition.WarningLinesBeforeEachTransfer" />), so it is not written here (ADR-0975). curl
    /// also carries on, silently, past a <c>CURLE_NOT_BUILT_IN</c> answer to <c>CURLOPT_CRLFILE</c>,
    /// <c>CURLOPT_PROXY_CRLFILE</c>, <c>CURLOPT_SSL_EC_CURVES</c>, <c>CURLOPT_SSL_SIGNATURE_ALGORITHMS</c> or
    /// <c>CURLOPT_TCP_FASTOPEN</c>, so this does too (ADR-1036).
    /// </summary>
    /// <param name="setopts">The calls to make.</param>
    /// <param name="setOption">Makes one call and answers its code.</param>
    /// <returns>
    /// <see langword="null" /> when every call answered <c>CURLE_OK</c> or an ignored <c>CURLE_NOT_BUILT_IN</c>; else
    /// the transfer's failure, with the call's code as the exit code and curl's
    /// <c>setopt 0x&lt;option&gt; got bad argument</c> as its message.
    /// </returns>
    internal static TransferResult? SetOptions(IEnumerable<EasySetopt> setopts, Func<EasySetopt, CurlCode> setOption)
    {
        foreach (EasySetopt setopt in setopts)
        {
            CurlCode code = setOption(setopt);
            if (code != CurlCode.Ok && !IsIgnoredNotBuiltIn(setopt.Option, code))
            {
                return TransferResult.Failure((CurlExitCode)(int)code, string.Create(CultureInfo.InvariantCulture, $"setopt 0x{(int)setopt.Option:x} got bad argument"));
            }
        }

        return null;
    }

    /// <summary><c>curl_easy_cleanup</c>: disposes the handle, and with it the engine transfer.</summary>
    /// <returns>A task that completes once the handle is cleaned up.</returns>
    public ValueTask DisposeAsync() => Easy.DisposeAsync();

    // Whether curl's config2setopts ignores this answer to this call.
    private static bool IsIgnoredNotBuiltIn(CurlOption option, CurlCode code) => code == CurlCode.NotBuiltIn && IgnoredWhenNotBuiltIn.Contains(option);
}
