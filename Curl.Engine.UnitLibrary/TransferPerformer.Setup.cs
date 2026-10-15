using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Text;
using Curl.Api.Options.Authentication;
using Curl.Api.Options.Http;
using Curl.Api.Options.Network;
using Curl.Api.Options.Protocols;
using Curl.Api.Options.Proxy;
using Curl.Core;
using Curl.Core.Hsts;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

/// <summary>
/// The per-transfer setup libcurl does before it connects, split out of <c>CurlCommandRunner</c>
/// (ADR-0506, BL-2283): the URL parse and its refusals, the setopt refusals, the proxy and credential
/// choice, the SSH options, the <c>-b</c> files, the HSTS upgrade, the alt-svc cache and the setup
/// failures, and the <c>-c</c> jar, alt-svc and HSTS write-back libcurl does at cleanup. What a step
/// writes to standard error is written by the delegate the runner passes.
/// </summary>
internal sealed partial class TransferPerformer
{
    /// <summary>
    /// What curl 8.21.0 prints before its URL parser's reason when it rejects a transfer
    /// URL, as in <c>URL rejected: Bad file:// URL</c>.
    /// </summary>
    internal const string UrlRejectedPrefix = "URL rejected: ";

    /// <summary>
    /// The longest percent-decoded host, in bytes, curl 8.21.0 connects to; a longer one
    /// fails with <see cref="TooLongHostnameMessage" /> before any connection.
    /// </summary>
    internal const int MaximumHostLength = 65535;

    /// <summary>curl 8.21.0's message for a host longer than <see cref="MaximumHostLength" />.</summary>
    internal const string TooLongHostnameMessage = "Too long hostname (maximum is 65535)";

    /// <summary>The line curl 8.21.0 prints before exit 2 when an SSH transfer has no known-hosts file.</summary>
    internal const string KnownHostsFileMissingLine = "curl: Could not find a known_hosts file";

    /// <summary>The warning curl 8.21.0 prints instead when a host key fingerprint was given.</summary>
    internal const string KnownHostsFileMissingWarning = "Warning: Could not find a known_hosts file";

    /// <summary>
    /// The result of an <c>scp</c> or <c>sftp</c> transfer with no <c>-k</c>, no <c>--knownhosts</c>, no
    /// host key fingerprint and no known-hosts file to be found: exit 2 with curl's text for it, before
    /// any connection, measured on curl 8.21.0 (BL-576 Notes). It is compared by reference, so that
    /// the runner stops before the remaining URLs, as curl does.
    /// </summary>
    internal TransferResult KnownHostsFileMissingFailure { get; } =
        TransferResult.Failure(CurlExitCode.FailedInit, easyErrorText(CurlExitCode.FailedInit));

    /// <summary>
    /// The result of a transfer whose <c>--interface</c> value libcurl refuses when curl sets it
    /// (<c>CURLOPT_INTERFACE</c>, option 10062, 0x274e): exit 43 with curl 8.21.0's message, before
    /// any connection (measured 2026-09-29, BL-600 Notes). It is compared by reference, so that
    /// the runner stops before the remaining URLs, as curl does.
    /// </summary>
    internal static readonly TransferResult InterfaceSetoptFailure =
        TransferResult.Failure(CurlExitCode.BadFunctionArgument, InterfaceSetoptMessage);

    /// <summary>
    /// The result of a transfer whose <c>--ech</c> mode libcurl refuses when curl sets it
    /// (<c>CURLOPT_ECH</c>, option 10325, 0x2855): exit 43 with curl 8.21.0's message, before any
    /// connection (measured with curl's ECH build 2026-10-02, BL-1107; ADR-0378). It is compared by
    /// reference, so that the runner stops before the remaining URLs, as curl does.
    /// </summary>
    internal static readonly TransferResult EchSetoptFailure =
        TransferResult.Failure(CurlExitCode.BadFunctionArgument, EchSetoptMessage);

    private const string InterfaceSetoptMessage = "setopt 0x274e got bad argument";

    private const string EchSetoptMessage = "setopt 0x2855 got bad argument";

    /// <summary>
    /// Parses the transfer's URL; under <c>--disallow-username-in-url</c> a URL with user information,
    /// even an empty user, is rejected with <see cref="CurlUrlRejection.UserNotAllowed" /> before its
    /// host and port are checked, so it fails with exit 67 even when its host or port is bad (curl
    /// 8.21.0, measured, BL-626 and BL-910 Notes).
    /// </summary>
    /// <param name="text">The URL, with any <c>-G</c> / <c>--url-query</c> query.</param>
    /// <param name="disallowUsernameInUrl"><c>--disallow-username-in-url</c>.</param>
    /// <param name="pathAsIs"><c>--path-as-is</c>.</param>
    /// <param name="url">The parsed URL.</param>
    /// <param name="rejection">Why the URL was rejected.</param>
    /// <returns><see langword="true" /> when the URL parsed.</returns>
    internal static bool TryParseTransferUrl(
        string text,
        bool disallowUsernameInUrl,
        bool pathAsIs,
        [NotNullWhen(true)] out CurlUrl? url,
        out CurlUrlRejection rejection) =>
        disallowUsernameInUrl
            ? CurlUrl.TryParseDisallowingUser(text, pathAsIs, out url, out rejection)
            : CurlUrl.TryParse(text, pathAsIs, out url, out rejection);

    /// <summary>
    /// Reports a transfer URL curl's parser rejects as the <c>-v</c> info line
    /// <c>URL rejected: &lt;reason&gt;</c>, as curl 8.21.0's <c>failf</c> in <c>lib/url.c</c> does
    /// (measured 2026-10-03, BL-1332), and fails with exit 67 for
    /// <see cref="CurlUrlRejection.UserNotAllowed" /> and exit 3 for every other rejection, with the same text.
    /// </summary>
    /// <param name="rejection">Why the URL was rejected.</param>
    /// <param name="events">The transfer's events before connecting.</param>
    /// <returns>The failure carrying the same text.</returns>
    internal static TransferResult ReportUrlRejected(CurlUrlRejection rejection, ITransferEvents events)
    {
        string message = UrlRejectedPrefix + rejection.ToCurlMessage();
        events.ReportInfo(message);
        return TransferResult.Failure(
            rejection == CurlUrlRejection.UserNotAllowed ? CurlExitCode.LoginDenied : CurlExitCode.UrlMalformat,
            message);
    }

    /// <summary>
    /// Why the parsed URL is refused before any connection, or <see langword="null" /> when it is not:
    /// a host longer than <see cref="MaximumHostLength" /> fails with exit 3 and
    /// <see cref="TooLongHostnameMessage" />.
    /// </summary>
    /// <param name="url">The parsed URL.</param>
    /// <returns>The refusal, or <see langword="null" />.</returns>
    internal static TransferResult? ParsedUrlRefusal(CurlUrl url) =>
        Encoding.UTF8.GetByteCount(url.Host) > MaximumHostLength
            ? TransferResult.Failure(CurlExitCode.UrlMalformat, TooLongHostnameMessage)
            : null;

    /// <summary>
    /// Refuses an <c>--interface</c> and then an <c>--ech</c> value curl's setopt refuses, before the transfer connects.
    /// </summary>
    /// <param name="network">The option group's network options, for <c>--interface</c>.</param>
    /// <param name="settings">The tool settings, for <c>--ech</c>.</param>
    /// <param name="events">The transfer's events before connecting.</param>
    /// <returns>The first refusal, or <see langword="null" /> when both are accepted.</returns>
    internal static TransferResult? RefuseMalformedSetopt(EasyNetworkOptions network, EngineTransferSettings settings, ITransferEvents events) =>
        RefuseMalformedInterface(network, events) ?? RefuseMalformedEchMode(settings, events);

    /// <summary>
    /// Chooses the transfer's proxy with <see cref="TransferProxySelection" />, then its credentials
    /// with <see cref="TransferCredentialLookup" />, which it keeps as the running transfer's
    /// <see cref="EngineTransferState.LookedUpCredentials" /> for every attempt's context. A scheme no
    /// handler serves fails first, with exit 1.
    /// </summary>
    /// <param name="dispatch">The run's dispatch: its dispatcher says whether the URL's scheme is served, its proxy selector chooses the proxy.</param>
    /// <param name="settings">The tool settings, for <c>-U</c> and <c>--unix-socket</c>.</param>
    /// <param name="proxyOptions">The option group's proxy options, which <see cref="TransferProxySelection" /> chooses from.</param>
    /// <param name="authentication">The option group's authentication options: <c>-u</c> and the netrc options <see cref="TransferCredentialLookup" /> reads.</param>
    /// <param name="url">The transfer's URL.</param>
    /// <param name="state">The running transfer, which keeps the credentials.</param>
    /// <param name="events">
    /// The transfer's events before connecting, which get curl's <c>-v</c> info line for every failure:
    /// credentials refused for a control character (BL-1411) and a required netrc file missing or malformed (BL-1447).
    /// </param>
    /// <param name="diagnosticLog">Where the proxy choice is logged.</param>
    /// <param name="credentialLookup">Chooses the credentials from the URL and the netrc file.</param>
    /// <param name="proxy">The proxy chosen, or <see langword="null" /> to connect directly.</param>
    /// <param name="failure">The proxy's, the credentials' or the netrc file's failure, when one refuses the transfer.</param>
    /// <returns><see langword="false" /> when the transfer ends before it starts.</returns>
    internal static bool TrySelectProxyAndCredentials(
        TransferDispatch dispatch,
        EngineTransferSettings settings,
        EasyProxyOptions proxyOptions,
        EasyAuthenticationOptions authentication,
        CurlUrl url,
        EngineTransferState state,
        ITransferEvents events,
        IDiagnosticLog diagnosticLog,
        TransferCredentialLookup credentialLookup,
        out ProxyEndpoint? proxy,
        [NotNullWhen(false)] out TransferResult? failure)
    {
        proxy = null;
        if (!dispatch.Dispatcher.Serves(url.Scheme))
        {
            failure = TransferResult.Failure(CurlExitCode.UnsupportedProtocol, $"Protocol \"{url.Scheme.ToLowerInvariant()}\" not supported");
            events.ReportInfo(failure.ErrorMessage!);
            return false;
        }

        if (!TransferProxySelection.TrySelect(dispatch.ProxySelector, proxyOptions, settings.OverUnixSocket, settings.ProxyCredentials, url, out proxy, out failure, diagnosticLog))
        {
            return false;
        }

        bool looked = credentialLookup.TryLookUp(authentication, url, out NetworkCredential? lookedUpCredentials, out failure, events.ReportInfo);
        state.LookedUpCredentials = lookedUpCredentials;
        if (failure is not null)
        {
            events.ReportInfo(failure.ErrorMessage!);
        }

        return looked;
    }

    /// <summary>
    /// Sets <see cref="EngineTransferState.Ssh" /> for an <c>scp</c> or <c>sftp</c> transfer, resolving its
    /// known-hosts file as curl 8.21.0's tool does (ADR-0122): none under <c>-k</c>, whatever
    /// <c>--knownhosts</c> says; else the <c>--knownhosts</c> file; else <paramref name="findKnownHostsFile" />'s.
    /// When none is found, a <c>--hostpubmd5</c> or <c>--hostpubsha256</c> fingerprint lets the transfer
    /// go on after <see cref="KnownHostsFileMissingWarning" /> (hidden by <c>-s</c>); without one,
    /// <see cref="KnownHostsFileMissingLine" /> (hidden by <c>-s</c> unless <c>-S</c>) and
    /// <see cref="KnownHostsFileMissingFailure" /> end the run before any connection (measured 2026-09-29,
    /// BL-576 Notes). Any other scheme is left without SSH options.
    /// </summary>
    /// <param name="settings">The tool settings, for <c>-k</c>, <c>-s</c>, <c>-S</c> and <c>--pass</c>.</param>
    /// <param name="ssh">The option group's SSH options: the keys, the host key fingerprints, compression and <c>--knownhosts</c>.</param>
    /// <param name="url">The URL to transfer.</param>
    /// <param name="state">The running transfer, which keeps the SSH options.</param>
    /// <param name="findKnownHostsFile">Finds the default known-hosts file, or gives <see langword="null" />.</param>
    /// <param name="recordKnownHostsFile">Records the file chosen, and whether it is missing, for <c>--libcurl</c>.</param>
    /// <param name="writeErrorLine">Writes a line to standard error.</param>
    /// <returns><see cref="KnownHostsFileMissingFailure" />, or <see langword="null" /> to go on.</returns>
    internal async Task<TransferResult?> ResolveSshOptionsAsync(
        EngineTransferSettings settings,
        EasySshOptions ssh,
        CurlUrl url,
        EngineTransferState state,
        Func<string?> findKnownHostsFile,
        Action<string?, bool> recordKnownHostsFile,
        Func<string, Task> writeErrorLine)
    {
        if (!SshOptionsMapping.IsSshScheme(url.Scheme))
        {
            return null;
        }

        string? knownHosts = settings.Insecure ? null : ssh.KnownHostsFile ?? findKnownHostsFile();
        bool missing = knownHosts is null && !settings.Insecure;
        recordKnownHostsFile(knownHosts, missing);
        if (missing && await ReportKnownHostsFileMissingAsync(settings, ssh, writeErrorLine).ConfigureAwait(false) is { } failure)
        {
            return failure;
        }

        state.Ssh = SshOptionsMapping.Of(ssh, settings.Passphrase, knownHosts);
        return null;
    }

    /// <summary>
    /// Loads the run's <c>-b</c> files before its first <c>http</c> or <c>https</c> transfer and
    /// not before any other: curl 8.21.0 reads <c>-b -</c> from standard input only then, so a
    /// <c>telnet</c> transfer or <c>file</c> upload before it gets standard input and the
    /// <c>-b -</c> after it finds it empty, and a <c>-T -</c> upload in the same transfer finds it
    /// empty (measured 2026-09-27, BL-316 Notes).
    /// </summary>
    /// <param name="cookies">The option group's cookies, or <see langword="null" /> without <c>-b</c> or <c>-c</c>.</param>
    /// <param name="settings">The tool settings, for the <c>-G</c> / <c>--url-query</c> query.</param>
    /// <param name="transferUrl">The URL about to be transferred.</param>
    /// <param name="fileSystem">Opens the files.</param>
    /// <param name="standardInput">What <c>-b -</c> reads.</param>
    /// <param name="events">Where the files' <c>-v</c> lines go.</param>
    /// <returns>A task that completes when the files are loaded.</returns>
    internal async Task LoadCookieFilesAsync(
        CookieEngine? cookies,
        EngineTransferSettings settings,
        string transferUrl,
        IFileSystem fileSystem,
        Stream standardInput,
        ITransferEvents events)
    {
        if (cookies is not null && IsHttpUrl(settings.AppendUrlQuery(transferUrl)))
        {
            await cookies.LoadCookieFilesAsync(fileSystem, standardInput, timeProvider.GetUtcNow(), events).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Reads the group's <c>--hsts</c> file into the run's HSTS cache, then switches an <c>http</c> URL whose
    /// host the cache knows to <c>https</c>, printing curl 8.21.0's <c>-v</c> line with the URL
    /// <c>%{url_effective}</c> then shows, as curl does just before it connects (BL-621 Notes).
    /// </summary>
    /// <param name="hsts">The run's HSTS cache.</param>
    /// <param name="settings">The tool settings, for the <c>-G</c> / <c>--url-query</c> query.</param>
    /// <param name="network">The option group's network options, for <c>--path-as-is</c>.</param>
    /// <param name="http">The option group's HTTP options, whose <see cref="EasyHttpOptions.Hsts" /> is the <c>--hsts</c> file.</param>
    /// <param name="transferUrl">The URL about to be transferred.</param>
    /// <param name="fileSystem">Opens the <c>--hsts</c> file.</param>
    /// <param name="events">Where the <c>-v</c> line goes.</param>
    /// <returns>The URL to transfer: switched, or as it was.</returns>
    internal static async Task<string> SwitchToHttpsForHstsAsync(
        HstsTransferPolicy hsts,
        EngineTransferSettings settings,
        EasyNetworkOptions network,
        EasyHttpOptions http,
        string transferUrl,
        IFileSystem fileSystem,
        ITransferEvents events)
    {
        await HstsCacheFile.ReadAsync(http, hsts, fileSystem).ConfigureAwait(false);

        if (!CurlUrl.TryParse(settings.AppendUrlQuery(transferUrl), network.PathAsIs, out CurlUrl? url)
            || !hsts.TrySwitchToHttps(transferUrl, url, out string? httpsUrl))
        {
            return transferUrl;
        }

        events.ReportInfo(HstsTransferPolicy.SwitchedMessagePrefix + UrlEffective.Normalize(settings.AppendUrlQuery(httpsUrl), network.PathAsIs));
        return httpsUrl;
    }

    /// <summary>
    /// Gives an <c>http</c> or <c>https</c> transfer of a group with <c>--alt-svc</c> its own alt-svc cache,
    /// its file read now, as curl 8.21.0 gives each transfer's handle one; any other transfer gets none, and
    /// its file is neither read nor written (measured 2026-09-29, BL-623 Notes).
    /// </summary>
    /// <param name="state">The running transfer, which keeps the cache.</param>
    /// <param name="settings">The tool settings, for the <c>-G</c> / <c>--url-query</c> query.</param>
    /// <param name="network">The option group's network options, for <c>--connect-to</c>.</param>
    /// <param name="http">The option group's HTTP options: the <see cref="EasyHttpOptions.AltSvc" /> file and the HTTP version.</param>
    /// <param name="transferUrl">The URL transferred.</param>
    /// <param name="fileSystem">Opens the file.</param>
    /// <param name="diagnosticLog">Where the cache logs.</param>
    /// <param name="usesOverHttp">Whether the cache also serves plain <c>http</c> origins (<c>CURL_ALTSVC_HTTP</c>).</param>
    /// <returns>A task that completes when the file is read.</returns>
    internal async Task OpenAltSvcCacheAsync(
        EngineTransferState state,
        EngineTransferSettings settings,
        EasyNetworkOptions network,
        EasyHttpOptions http,
        string transferUrl,
        IFileSystem fileSystem,
        IDiagnosticLog diagnosticLog,
        bool usesOverHttp)
    {
        if (http.AltSvc is not null && IsHttpUrl(settings.AppendUrlQuery(transferUrl)))
        {
            state.AltSvc = await AltSvcTransferCache
                .OpenAsync(http, network.ConnectTo, fileSystem, timeProvider, diagnosticLog, usesOverHttp)
                .ConfigureAwait(false);
        }
    }

    /// <summary>
    /// The failure a transfer ends with while curl 8.21.0 sets its options, before any upload file
    /// is opened or the URL parsed: when the group's joined <c>-b name=value</c> strings reach
    /// <see cref="CookieEngine.LongestCookieLine" /> bytes, <see cref="CookieEngine.CookieLineTooLongWarning" />
    /// unless <c>-s</c> and exit 100 with <c>A value or data field grew larger than allowed</c>, as curl's
    /// <c>cookie_setopts</c> does (measured 2026-10-03, BL-1391); else <see cref="NoCryptoEngines.LoadFailure" />'s.
    /// </summary>
    /// <param name="cookies">The option group's cookies, or <see langword="null" />.</param>
    /// <param name="settings">The tool settings, for <c>--engine</c> and <c>-s</c>.</param>
    /// <param name="runsOnWindows">Whether the run is the Windows build's.</param>
    /// <param name="writeWarning">Writes a warning's text to standard error, wrapped as every warning is.</param>
    /// <returns>The failure, or <see langword="null" /> when the transfer goes ahead.</returns>
    internal async Task<TransferResult?> SetupFailureAsync(
        CookieEngine? cookies,
        EngineTransferSettings settings,
        bool runsOnWindows,
        Func<string, Task> writeWarning)
    {
        if (cookies is not { CookieLineTooLong: true })
        {
            return NoCryptoEngines.LoadFailure(settings.Engine, runsOnWindows);
        }

        if (!settings.Silent)
        {
            await writeWarning(CookieEngine.CookieLineTooLongWarning).ConfigureAwait(false);
        }

        return TransferResult.Failure(CurlExitCode.TooLarge, easyErrorText(CurlExitCode.TooLarge));
    }

    /// <summary>
    /// Writes the run's <c>-c</c> jar after the transfer of <paramref name="transferUrl" />, then the
    /// transfer's <c>--alt-svc</c> file, when it has one (<see cref="EngineTransferState.AltSvc" />), then
    /// the group's <c>--hsts</c> file, when it has one (<see cref="HstsCacheFile.WriteAsync" />), as libcurl
    /// writes them at cleanup. The jar is written when there is one and the URL is <c>http</c> or
    /// <c>https</c>, whatever the transfer's outcome: curl 8.21.0 writes it after every HTTP transfer,
    /// after its <c>-w</c> output, and not after a <c>file</c> or <c>dict</c> one (measured 2026-09-26,
    /// BL-237 Notes); the jar file is replaced each time, and <c>-c -</c> prints it to
    /// <paramref name="jarPrintTarget" />.
    /// </summary>
    /// <param name="cookies">The option group's cookies, or <see langword="null" />.</param>
    /// <param name="settings">The tool settings, for the <c>-G</c> / <c>--url-query</c> query.</param>
    /// <param name="http">The option group's HTTP options, whose <see cref="EasyHttpOptions.Hsts" /> is the <c>--hsts</c> file.</param>
    /// <param name="transferUrl">The URL transferred, as typed or as a <c>-T</c> upload resolved it.</param>
    /// <param name="jarPrintTarget">Gives the stream the runner chose for <c>-c -</c> to print the jar to; the performer never picks it.</param>
    /// <param name="state">The transfer's state, with its alt-svc cache.</param>
    /// <param name="hsts">The run's HSTS cache.</param>
    /// <param name="fileSystem">Writes the files.</param>
    /// <param name="runsOnWindows">Whether the <c>--hsts</c> file's lines end in CR LF.</param>
    /// <returns>A task that completes when all are written.</returns>
    internal async Task WriteCookieJarAltSvcAndHstsFilesAsync(
        CookieEngine? cookies,
        EngineTransferSettings settings,
        EasyHttpOptions http,
        string transferUrl,
        Func<Stream> jarPrintTarget,
        EngineTransferState state,
        HstsTransferPolicy hsts,
        IFileSystem fileSystem,
        bool runsOnWindows)
    {
        if (cookies is not null && IsHttpUrl(settings.AppendUrlQuery(transferUrl)))
        {
            await cookies.WriteCookieJarAsync(fileSystem, jarPrintTarget(), timeProvider.GetUtcNow()).ConfigureAwait(false);
        }

        if (state.AltSvc is { } altSvc)
        {
            await altSvc.WriteAsync(fileSystem).ConfigureAwait(false);
        }

        await HstsCacheFile.WriteAsync(http, hsts, fileSystem, runsOnWindows).ConfigureAwait(false);
    }

    /// <summary>
    /// Reports that an SSH transfer checked by known hosts has no known-hosts file, for
    /// <see cref="ResolveSshOptionsAsync" />: <see cref="KnownHostsFileMissingWarning" />, unless <c>-s</c>,
    /// when a host key fingerprint was given, and otherwise <see cref="KnownHostsFileMissingLine" />, unless
    /// <c>-s</c> without <c>-S</c>, and <see cref="KnownHostsFileMissingFailure" />.
    /// </summary>
    /// <param name="settings">The tool settings, for <c>-s</c> and <c>-S</c>.</param>
    /// <param name="ssh">The option group's SSH options, for the host key fingerprints.</param>
    /// <param name="writeErrorLine">Writes a line to standard error.</param>
    /// <returns><see cref="KnownHostsFileMissingFailure" />, or <see langword="null" /> when the transfer goes on.</returns>
    private async Task<TransferResult?> ReportKnownHostsFileMissingAsync(EngineTransferSettings settings, EasySshOptions ssh, Func<string, Task> writeErrorLine)
    {
        if (ssh.HostPublicKeyMd5 is not null || ssh.HostPublicKeySha256 is not null)
        {
            await WriteUnlessHiddenAsync(settings.Silent, KnownHostsFileMissingWarning, writeErrorLine).ConfigureAwait(false);
            return null;
        }

        await WriteUnlessHiddenAsync(settings.Silent && !settings.ShowError, KnownHostsFileMissingLine, writeErrorLine).ConfigureAwait(false);
        return KnownHostsFileMissingFailure;
    }

    /// <summary>Writes <paramref name="line" /> unless <paramref name="hidden" />.</summary>
    /// <param name="hidden">Whether the line is hidden.</param>
    /// <param name="line">The line.</param>
    /// <param name="writeErrorLine">Writes a line to standard error.</param>
    /// <returns>A task that completes when the line is written, or at once when hidden.</returns>
    private static Task WriteUnlessHiddenAsync(bool hidden, string line, Func<string, Task> writeErrorLine) =>
        hidden ? Task.CompletedTask : writeErrorLine(line);

    /// <summary>
    /// Refuses a <c>--interface</c> value libcurl refuses when curl sets it
    /// (<see cref="EngineInterfaceBinding.IsMalformed" />): curl 8.21.0 reports <c>setopt 0x274e got bad
    /// argument</c> as a <c>-v</c> line, fails the transfer with exit 43 and that message, still writes
    /// its <c>-w</c> output, and transfers nothing after it (measured 2026-09-29, BL-600 Notes).
    /// </summary>
    /// <param name="network">The option group's network options, for <c>--interface</c>.</param>
    /// <param name="events">Where the <c>-v</c> line goes.</param>
    /// <returns><see cref="InterfaceSetoptFailure" />, or <see langword="null" /> when the value was accepted.</returns>
    private static TransferResult? RefuseMalformedInterface(EasyNetworkOptions network, ITransferEvents events)
    {
        if (network.Interface is not { } interfaceValue || !EngineInterfaceBinding.Parse(interfaceValue).IsMalformed)
        {
            return null;
        }

        events.ReportInfo(InterfaceSetoptMessage);
        return InterfaceSetoptFailure;
    }

    /// <summary>
    /// Refuses an <c>--ech</c> mode libcurl refuses when curl sets it
    /// (<see cref="EngineTransferSettings.EchModeIsMalformed" />): curl 8.21.0's ECH build reports
    /// <c>setopt 0x2855 got bad argument</c> and fails the transfer with exit 43 (BL-1107), as it
    /// fails a malformed <c>--interface</c>, so the <c>-v</c> line and the <c>-w</c> output follow that
    /// measurement (ADR-0378).
    /// </summary>
    /// <param name="settings">The tool settings, for <c>--ech</c>.</param>
    /// <param name="events">Where the <c>-v</c> line goes.</param>
    /// <returns><see cref="EchSetoptFailure" />, or <see langword="null" /> when the mode was accepted.</returns>
    private static TransferResult? RefuseMalformedEchMode(EngineTransferSettings settings, ITransferEvents events)
    {
        if (!settings.EchModeIsMalformed)
        {
            return null;
        }

        events.ReportInfo(EchSetoptMessage);
        return EchSetoptFailure;
    }

    /// <summary>
    /// Tells whether <paramref name="requestUrl" /> is an absolute <c>http</c> or <c>https</c> URL.
    /// </summary>
    /// <param name="requestUrl">The URL, with any <c>-G</c> / <c>--url-query</c> query.</param>
    /// <returns><see langword="true" /> for an HTTP URL.</returns>
    private static bool IsHttpUrl(string requestUrl) =>
        CurlUrl.TryParse(requestUrl, pathAsIs: false, out CurlUrl? url)
        && url.Scheme is "http" or "https";
}
