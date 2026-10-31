using System.Text;
using Curl.Api.Callbacks;
using Curl.Api.Codes;
using Curl.Api.EngineContracts;
using Curl.Api.EngineContracts.ConnectOnly;
using Curl.Api.EngineContracts.Headers;
using Curl.Api.EngineContracts.Multi;
using Curl.Api.EngineContracts.WebSocket;
using Curl.Api.Info;
using Curl.Api.Options;
using Curl.Api.Options.Authentication;
using Curl.Api.Options.Proxy;
using Curl.Core;
using Curl.Core.Hsts;
using Curl.Core.Multipart;
using Curl.Protocol.Abstractions;
using Curl.Original.Protocol.Http;
using Curl.Original.Protocol.Ws;

namespace Curl.Engine;

// The handle's lifecycle: duplicate, reset, pause and dispose.
internal sealed partial class ManagedEasyTransfer
{
    /// <inheritdoc />
    public IEasyTransfer Duplicate() => new ManagedEasyTransfer(createDispatcher, shared.DuplicatedWithOwnCaches()) { cookieListItemsApplied = cookieListItemsApplied, Options = Options, ToolSettings = ToolSettings, Host = Host, TraceComponents = TraceComponents, CreateFormBoundary = CreateFormBoundary, CreateConnectOnlyConnector = CreateConnectOnlyConnector, WebSocketRandomSource = WebSocketRandomSource };

    /// <inheritdoc />
    /// <remarks>
    /// <see cref="Info" /> starts again as a fresh handle's, keeping only the <see cref="TransferInfo.TransferId" /> and
    /// <see cref="TransferInfo.CookieList" />, as curl 8.22.0's <c>curl_easy_reset</c> calls <c>Curl_initinfo</c> (response
    /// code 0, file time -1) and keeps the handle's id and cookies (upstream tests 1532 and 1534).
    /// </remarks>
    public void Reset()
    {
        headers.Clear();
        keptDigestChallenges = [];
        keptDigestStaysAvailable = false;
        keptAuthSchemePicked = HttpAuthSchemes.None;
        Options = new EasyOptions();
        Info = new TransferInfo { TransferId = Info.TransferId, CookieList = Info.CookieList };
        ToolSettings = null;
        ToolInputs = null;
        ToolOutputs = null;
        ToolEvents = null;
        ToolProgress = null;
        ToolDispatch = null;
    }

    /// <summary>
    /// <c>curl_easy_pause</c>: records the directions to leave paused, and a transfer waiting on a callback's
    /// pause in a direction now clear hands the callback its data again (ADR-0573). A handle with no perform running has no
    /// connection to pause, which libcurl refuses (CGR-246).
    /// </summary>
    /// <param name="directions">The directions to leave paused.</param>
    /// <returns><see cref="CurlCode.Ok" />, or <see cref="CurlCode.BadFunctionArgument" /> when no perform is running.</returns>
    public CurlCode Pause(CurlPause directions)
    {
        if (!ConnectOnly.PerformRunning)
        {
            return CurlCode.BadFunctionArgument;
        }

        pauseGate.Set(directions);
        return CurlCode.Ok;
    }

    /// <summary>
    /// <c>curl_easy_upkeep</c>: the engine's pooled connections need no keep-alive frames of their own
    /// (no HTTP/2 PING is sent on idle connections yet), so there is nothing to do.
    /// </summary>
    /// <param name="cancellationToken">Stops the upkeep.</param>
    /// <returns><see cref="CurlCode.Ok" />.</returns>
    public ValueTask<CurlCode> UpkeepAsync(CancellationToken cancellationToken) => ValueTask.FromResult(CurlCode.Ok);

    /// <summary>
    /// <c>curl_easy_cleanup</c>: removes the handle from the multi handle it is still in, cancelling its perform, as
    /// libcurl's <c>curl_easy_cleanup</c> calls <c>curl_multi_remove_handle</c> (upstream test753, BL-2636), hands the HSTS
    /// cache to <c>CURLOPT_HSTSWRITEFUNCTION</c> (<see cref="SaveHstsToCallback" />),
    /// takes the attached share handle's cookie lock for libcurl's cookie flush and detaches from it, then closes the
    /// connection a connect-only perform left open or upgraded, if any, and the handle's own idle pooled connections
    /// (<see cref="OwnTheConnectionsOpened" />), as libcurl closes the handle's connection cache, FTP's sending <c>QUIT</c>.
    /// </summary>
    /// <returns>A task that completes once they are closed.</returns>
    public async ValueTask DisposeAsync()
    {
        if (shared.MultiOwners.TryGetValue(this, out ManagedMultiTransfer? multi))
        {
            _ = multi.Remove(this);
        }

        SaveHstsToCallback();
        await SaveAltSvcAsync().ConfigureAwait(false);
        await SaveCookieJarAsync().ConfigureAwait(false);
        optionShare?.LockCookies(this, 1);
        optionShare = ManagedShareHandle.Reattach(optionShare, named: null, this);
        await (connectOnly?.CloseAsync() ?? ValueTask.CompletedTask).ConfigureAwait(false);
        await (webSocket?.CloseAsync() ?? ValueTask.CompletedTask).ConfigureAwait(false);
        await (ToolSettings is not null ? shared.EngineWide : shared).Connections.CloseIdleOwnedByAsync(this).ConfigureAwait(false);
    }

    /// <summary>Writes the cookies of the last HTTP perform to its <c>CURLOPT_COOKIEJAR</c> file, as libcurl does at cleanup (CGR-258, BL-3787); nothing when no perform had a jar.</summary>
    /// <returns>A task that completes once the file is written.</returns>
    private Task SaveCookieJarAsync() => handleCookieJar?.WriteCookieJarAsync(Host.FileSystem, Host.JarPrintTarget(), shared.TimeProvider.GetUtcNow()) ?? Task.CompletedTask;

    /// <summary>Writes the handle's alt-svc cache to its <c>CURLOPT_ALTSVC</c> file, as libcurl does at cleanup (BL-2671); nothing when no perform opened one.</summary>
    /// <returns>A task that completes once the file is written.</returns>
    private Task SaveAltSvcAsync() => handleAltSvc?.WriteAsync(Host.FileSystem) ?? Task.CompletedTask;
}
