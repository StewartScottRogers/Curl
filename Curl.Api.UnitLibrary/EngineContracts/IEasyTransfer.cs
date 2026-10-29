using Curl.Api.Codes;
using Curl.Api.EngineContracts.ConnectOnly;
using Curl.Api.EngineContracts.Headers;
using Curl.Api.EngineContracts.Tls;
using Curl.Api.EngineContracts.WebSocket;
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

    /// <summary>The header API (<c>curl_easy_header</c>, <c>curl_easy_nextheader</c>) over the headers this handle received.</summary>
    ITransferHeaders Headers { get; }

    /// <summary>The WebSocket API (<c>curl_ws_recv</c>, <c>curl_ws_send</c>, <c>curl_ws_start_frame</c>, <c>curl_ws_meta</c>) on this handle.</summary>
    IEasyWebSocket WebSocket { get; }

    /// <summary>
    /// <c>curl_easy_recv</c> and <c>curl_easy_send</c>: the raw connection left open by a perform with
    /// <c>CURLOPT_CONNECT_ONLY</c>.
    /// </summary>
    IConnectOnlyConnection Connection { get; }

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

    /// <summary>
    /// <c>curl_easy_ssls_import</c>: add one packed TLS session to the session cache the handle uses, its share
    /// handle's while that shares <c>CURL_LOCK_DATA_SSL_SESSION</c>, else its own.
    /// </summary>
    /// <param name="sessionKey">The peer key to hold the session under, or <see langword="null" /> to use <paramref name="saltAndHmac" />.</param>
    /// <param name="saltAndHmac">The 64-byte salt and hmac of the peer, read when <paramref name="sessionKey" /> is <see langword="null" />.</param>
    /// <param name="packedSession">The session as <see cref="ExportTlsSessions" /> handed it out.</param>
    /// <returns>
    /// The import's result: <see cref="CurlCode.ReadError" /> for a packet that does not unpack,
    /// <see cref="CurlCode.BadFunctionArgument" /> for neither a key nor a 64-byte salt and hmac, and
    /// <see cref="CurlCode.NotBuiltIn" /> where the build has no <c>SSLS-EXPORT</c>, which is all this default answers.
    /// </returns>
    CurlCode ImportTlsSession(string? sessionKey, byte[]? saltAndHmac, byte[] packedSession) => CurlCode.NotBuiltIn;

    /// <summary>
    /// <c>curl_easy_ssls_export</c>: hand every session the handle's session cache holds (its share handle's while that
    /// shares <c>CURL_LOCK_DATA_SSL_SESSION</c>, else its own) to <paramref name="export" />, one call each.
    /// </summary>
    /// <param name="export">The <c>curl_ssls_export_cb</c>; an answer other than <see cref="CurlCode.Ok" /> stops the export with it.</param>
    /// <returns>
    /// <see cref="CurlCode.Ok" />, the callback's first answer other than it, or <see cref="CurlCode.NotBuiltIn" />
    /// where the build has no <c>SSLS-EXPORT</c>, which is all this default answers.
    /// </returns>
    CurlCode ExportTlsSessions(Func<ExportedTlsSession, CurlCode> export) => CurlCode.NotBuiltIn;

    /// <summary>
    /// The handle whose <see cref="PerformAsync" /> does this handle's transfer: this handle itself, which is all this
    /// default answers, or the one a wrapper forwards its perform to, so a multi handle gives the wrapped engine
    /// transfer the socket wait it tells the application's socket callback of (ADR-0986).
    /// </summary>
    IEasyTransfer PerformingTransfer => this;
}
