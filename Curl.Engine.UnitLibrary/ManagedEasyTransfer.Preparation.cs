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

// What a perform prepares: HSTS callbacks, proxy choice, looked-up credentials and connect-only checks.
internal sealed partial class ManagedEasyTransfer
{
    /// <summary>
    /// The HSTS cache the last perform from the options used - the engine's, or its share handle's - which
    /// <see cref="DisposeAsync" /> hands to <c>CURLOPT_HSTSWRITEFUNCTION</c>; <see langword="null" /> before one.
    /// </summary>
    private HstsTransferPolicy? performedHsts;

    /// <summary>
    /// <c>CURLOPT_HSTSREADFUNCTION</c> before the transfer, as libcurl's <c>Curl_pretransfer</c> calls it: the entries
    /// it hands over loaded into <paramref name="hsts" /> (<see cref="CallbackHstsCache.Load" />) while the handle's HSTS
    /// cache is enabled (<see cref="CallbackHstsCache.IsEnabled" />); nothing without the callback (ADR-0620).
    /// </summary>
    /// <param name="options">The handle's options.</param>
    /// <param name="hsts">The HSTS cache the perform uses.</param>
    /// <returns><see cref="CurlCode.Ok" />, or the code that fails the perform.</returns>
    private static CurlCode LoadHstsFromCallback(EasyOptions options, HstsTransferPolicy hsts) =>
        options.Callbacks.HstsRead is { } read && CallbackHstsCache.IsEnabled(options.Http)
            ? CallbackHstsCache.Load(read, hsts)
            : CurlCode.Ok;

    /// <summary>
    /// <c>CURLOPT_HSTSWRITEFUNCTION</c> at cleanup, as libcurl's <c>Curl_close</c> calls it: every entry of the HSTS
    /// cache the last perform used (the engine's before any) handed to it (<see cref="CallbackHstsCache.Save" />) while
    /// the handle's HSTS cache is enabled; nothing without the callback (ADR-0620).
    /// </summary>
    private void SaveHstsToCallback()
    {
        EasyOptions options = Options;
        if (options.Callbacks.HstsWrite is { } write && CallbackHstsCache.IsEnabled(options.Http))
        {
            CallbackHstsCache.Save(write, performedHsts ?? PerformShared.Hsts);
        }
    }

    /// <summary>The proxy <see cref="TrySelectProxy" /> chose for the running perform, which <see cref="OwnInputsOf" /> puts on the context.</summary>
    private ProxyEndpoint? selectedProxy;

    /// <summary>
    /// Chooses the perform's proxy: the curl tool's runner's (<see cref="EngineToolTransferInputs.Proxy" />) when it gave
    /// <see cref="ToolInputs" />; else <see cref="TransferProxySelection" />'s choice from <see cref="EasyOptions.Proxy" />
    /// and the proxy environment variables the engine's <see cref="ManagedEngineSharedState.ProxySelector" /> reads, as
    /// libcurl reads them for every handle, connect-only performs included (BL-2519, ADR-0608).
    /// </summary>
    /// <param name="options">The handle's options.</param>
    /// <param name="settings">The tool settings, for the proxy credentials and the Unix socket.</param>
    /// <param name="url">The URL set, parsed.</param>
    /// <param name="proxy">The proxy, or <see langword="null" /> to connect directly.</param>
    /// <param name="refused">The selector's failure for proxy text that cannot be used; <see langword="null" /> otherwise.</param>
    /// <returns><see langword="true" /> unless the perform must end with <paramref name="refused" />.</returns>
    private bool TrySelectProxy(
        EasyOptions options,
        EngineTransferSettings settings,
        CurlUrl url,
        out ProxyEndpoint? proxy,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(false)] out TransferResult? refused)
    {
        if (ToolInputs is { } toolInputs)
        {
            proxy = toolInputs.Proxy;
            refused = null;
            return true;
        }

        return TransferProxySelection.TrySelect(PerformShared.ProxySelector, options.Proxy, settings.OverUnixSocket, settings.ProxyCredentials, url, out proxy, out refused, Host.DiagnosticLog);
    }

    /// <summary>The credentials <see cref="TryLookUpUrlAndNetrcCredentials" /> chose for the running perform, which <see cref="OwnInputsOf" /> puts on the context.</summary>
    private System.Net.NetworkCredential? lookedUpCredentials;

    /// <summary>
    /// Chooses the perform's credentials with <see cref="TransferCredentialLookup" />, as libcurl's <c>override_login</c>
    /// does for every handle, proxied or not: with no <c>CURLOPT_USERPWD</c> user name, the URL's own user name and
    /// password (upstream test1964, BL-2684), and under <c>CURLOPT_NETRC</c> the <c>.netrc</c> file's entry
    /// (upstream test2309, BL-2696): the <c>CURLOPT_NETRC_FILE</c> file, else the one <c>NETRC</c> names, else
    /// <c>.netrc</c> in <c>HOME</c>, read through <see cref="EasyTransferHost.ReadFile" />. Nothing is looked up with
    /// <see cref="ToolInputs" /> set, whose runner chose its own.
    /// </summary>
    /// <param name="options">The handle's options.</param>
    /// <param name="url">The URL set, parsed, whose host picks the entry.</param>
    /// <param name="failure">The exit-26 failure of a required file missing or malformed, or the exit-3 one of URL credentials refused.</param>
    /// <returns><see langword="false" /> when the perform must end with <paramref name="failure" />.</returns>
    private bool TryLookUpUrlAndNetrcCredentials(
        EasyOptions options,
        CurlUrl url,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(false)] out TransferResult? failure)
    {
        lookedUpCredentials = null;
        hopCredentials = null;
        failure = null;
        if (ToolInputs is not null)
        {
            return true;
        }

        TransferCredentialLookup lookup = new(Host.ReadFile, shared.ReadEnvironmentVariable, Host.RunsOnWindows, Host.DiagnosticLog);
        hopCredentials = lookup.ForRedirectHops(options.Authentication);
        return lookup.TryLookUp(options.Authentication, url, out lookedUpCredentials, out failure);
    }

    /// <summary>
    /// The lookup <see cref="TryLookUpUrlAndNetrcCredentials" /> chose for each redirect hop's credentials, which
    /// <see cref="FollowerOf" /> gives the handle's own follower: under <c>CURLOPT_NETRC</c> the hop's own host's
    /// <c>.netrc</c> entry, or none, whatever <c>CURLOPT_UNRESTRICTED_AUTH</c> says, as libcurl looks the file up
    /// again for each hop (upstream test2506, BL-2701); <see langword="null" /> to carry the first hop's by the
    /// follower's origin rules.
    /// </summary>
    private HopCredentialSelector? hopCredentials;

    /// <summary>
    /// Whether the perform only connects (<see cref="PerformConnectOnlyAsync" />): under <c>CURLOPT_CONNECT_ONLY</c> 1
    /// for a URL whose connect phase sends nothing (not <see cref="LogsInBeforeConnectOnly" />), and under 2 for a URL
    /// that is not <c>ws</c> or <c>wss</c>, as libcurl, which upgrades only those (ADR-0616).
    /// </summary>
    /// <param name="connectOnly">The handle's <c>CURLOPT_CONNECT_ONLY</c>.</param>
    /// <param name="url">The URL set, parsed.</param>
    /// <returns><see langword="true" /> when the perform connects and sends nothing.</returns>
    private static bool IsRawConnectOnly(Api.Options.Network.ConnectOnly connectOnly, CurlUrl url) => connectOnly switch
    {
        Api.Options.Network.ConnectOnly.Connect => !LogsInBeforeConnectOnly(url),
        Api.Options.Network.ConnectOnly.WebSocket => !IsWebSocket(url),
        _ => false,
    };

    /// <summary>
    /// Whether a <c>CURLOPT_CONNECT_ONLY</c> 1 perform of <paramref name="url" /> runs the protocol's connect phase
    /// (<see cref="ConnectOnlyLoginDispatcherOf" />): <c>ftp</c>, <c>ftps</c>, <c>imap</c> and <c>imaps</c>, whose
    /// handlers have a connect-only entry point, as libcurl logs in before such a perform ends (upstream tests 597
    /// and 660, BL-2623).
    /// </summary>
    /// <param name="url">The URL set, parsed.</param>
    /// <returns><see langword="true" /> for those schemes.</returns>
    private static bool LogsInBeforeConnectOnly(CurlUrl url) => url.Scheme.ToLowerInvariant() is "ftp" or "ftps" or "imap" or "imaps";

    /// <summary>Whether <paramref name="url" /> is a <c>ws</c> or <c>wss</c> URL.</summary>
    /// <param name="url">The URL.</param>
    /// <returns><see langword="true" /> for either WebSocket scheme.</returns>
    private static bool IsWebSocket(CurlUrl url) => url.Scheme.ToLowerInvariant() is "ws" or "wss";

    /// <summary>
    /// <c>CURLOPT_CONNECT_ONLY</c> 1: connects to the URL's host and port through <see cref="CreateConnectOnlyConnector" />'s
    /// connector, with TLS for a TLS scheme, through the proxy <see cref="TrySelectProxy" /> chose (a tunnel through
    /// an HTTP or HTTPS proxy, the handshake of a SOCKS one, as libcurl tunnels for connect-only without
    /// <c>CURLOPT_HTTPPROXYTUNNEL</c>), sends nothing, and keeps the connection for <see cref="Connection" />; a
    /// failed connect ends the perform with the connector's code and text (ADR-0608).
    /// </summary>
    /// <param name="options">The handle's options.</param>
    /// <param name="url">The URL set, parsed.</param>
    /// <param name="proxy">The proxy to connect through, or <see langword="null" /> to connect directly.</param>
    /// <param name="cancellationToken">Stops the connect.</param>
    /// <returns>The perform's code.</returns>
    private async Task<CurlCode> PerformConnectOnlyAsync(EasyOptions options, CurlUrl url, ProxyEndpoint? proxy, CancellationToken cancellationToken)
    {
        IConnector connector = CreateConnectOnlyConnector(options, PerformShared);
        ConnectTarget target = new(url.IdnHost, url.Port, ConnectOnlyConnection.UsesTls(url.Scheme)) { Proxy = proxy };
        ConnectResult connect = await connector.ConnectAsync(target, cancellationToken).ConfigureAwait(false);
        if (connect.Connection is not { } opened)
        {
            // ConnectResult.Failed takes a non-null message, so a result with no connection always has one.
            return Finish(connect.ExitCode.ToCurlCode(), connect.ErrorMessage!, Info);
        }

        await ConnectOnly.OpenAsync(opened).ConfigureAwait(false);
        TransferInfo connected = WithConnectionEndPoints(Info, opened.RemoteEndPoint as System.Net.IPEndPoint, opened.LocalEndPoint as System.Net.IPEndPoint ?? connect.LocalEndPoint);
        return Finish(CurlCode.Ok, string.Empty, TransferInfoFromReport.WithScheme(connected, url.Scheme));
    }
}
