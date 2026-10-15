using Curl.Api.Codes;
using Curl.Api.Info;
using Curl.Api.Options;

namespace Curl.Api.EngineContracts;

/// <summary>
/// One easy handle of an <see cref="ICurlEngine"/> (ADR-0506): its options, its transfer and what
/// the transfer reported. <see cref="IAsyncDisposable.DisposeAsync"/> is <c>curl_easy_cleanup</c>.
/// </summary>
public interface IEasyTransfer : IAsyncDisposable
{
    /// <summary>
    /// <c>curl_easy_setopt</c>: every option of the handle. Setting it replaces them all; one option
    /// changes with a <see langword="with"/> expression on the current value.
    /// </summary>
    EasyOptions Options { get; set; }

    /// <summary><c>curl_easy_getinfo</c>: what the handle's last transfer reported.</summary>
    TransferInfo Info { get; }

    /// <summary>
    /// <c>CURLOPT_ERRORBUFFER</c>'s content: the text of the last error, empty when the last call
    /// succeeded or gave no detail.
    /// </summary>
    string ErrorText { get; }

    /// <summary><c>curl_easy_perform</c>: run the transfer the options describe.</summary>
    /// <param name="cancellationToken">Stops the transfer; it then ends with an error code.</param>
    /// <returns>The transfer's result.</returns>
    ValueTask<CurlCode> PerformAsync(CancellationToken cancellationToken);

    /// <summary><c>curl_easy_duphandle</c>: a new handle with this one's options and none of its state.</summary>
    /// <returns>The new easy handle; the caller disposes it.</returns>
    IEasyTransfer Duplicate();

    /// <summary><c>curl_easy_reset</c>: every option back to its default (<c>new EasyOptions()</c>); connections and caches stay.</summary>
    void Reset();

    /// <summary><c>curl_easy_pause</c>: pause or unpause receiving and sending.</summary>
    /// <param name="directions">The directions to leave paused; a clear bit unpauses that direction.</param>
    /// <returns>The call's result.</returns>
    CurlCode Pause(CurlPause directions);

    /// <summary><c>curl_easy_upkeep</c>: keep the handle's idle connections alive.</summary>
    /// <param name="cancellationToken">Stops the upkeep.</param>
    /// <returns>The upkeep's result.</returns>
    ValueTask<CurlCode> UpkeepAsync(CancellationToken cancellationToken);
}
