# Curl.Engine.UnitLibrary

The managed transfer engine (ADR-0506): the code that runs a transfer, moving out of
`Curl.Console` so that `Curl.Console`, `Curl.Api` and `Libcurl` can all drive it. Namespace
`Curl.Engine`. The sections below record, task by task (BL-2285 onwards), what moved here and what
stayed in `Curl.Console` because it is the curl tool's.

It must never reference `Curl.Cli.UnitLibrary` (BL-2310 dropped the last reference): the engine
reads only the easy option groups (`Curl.Api`'s `Easy*Options` records and the engine's own
`EngineToolOptions`, `EngineTransferSettings` and `EngineTraceComponents`, the run's `--trace-config`
components with the ones `-vv` and up add), which `Curl.Console` builds from the command line.

Never reference `Curl.Console`, `Libcurl` or `Curl.Api`: the engine sits beneath all three.

Held to the four quality gates in the root `CLAUDE.md`. Its tests live in `Curl.Engine.UnitTests`.

## What has moved here so far (BL-2285)

`EngineComposition.CreateProtocolHandlers` gives every handler an
`EndPointRecordingConnector` and an `EndPointRecordingDatagramConnector` sharing one
`ConnectionEndPointRecorder`, and wraps each handler in an `EndPointReportingProtocolHandler`, which
puts the end points of the transfer's first connection (FTP's control connection) on its report when
the handler reported neither, so `%{local_ip}`, `%{local_port}`, `%{remote_ip}` and `%{remote_port}`
work for every scheme without handler code; HTTP keeps its own (ADR-0119, BL-515). Each handler sits
behind a `LazyProtocolHandler` that names its schemes and builds the handler on its first transfer
(BL-1715, AF-0063); `EndPointReportingProtocolHandler.Handler` gives the built handler.

An `ftp` or `ftps` URL is claimed by `RoutingFtpProtocolHandler`, which hands an `ftp` one to the HTTP
handler when its proxy is `Http` or `Http10` and `-p` is not given, so it is forwarded to the proxy
as `GET ftp://host/path` with `Host: host:21` (ADR-0056, rule 3; BL-344); any other transfer goes to
`FtpProtocolHandler`.

The trace-event decorators `ClientReaderResetTraceEvents` (the `[READ] client_reset, clear readers`
line before a finished transfer's `left intact` or `shutting down connection` line and after each
followed redirect's `Issue another request to this URL` line; ADR-0357's BL-1159 amendment),
`ClientWriterTraceEvents`, `ConnectReplyHeadWritingEvents` and `ResponseWaitTimerTraceEvents` (the
response wait's `[TIMER]` lines after `Request completely sent off` when the multi is not traced;
ADR-0401, BL-1258) wrap an `ITransferEvents`. `LateBoundDiagnosticLog` is an `IDiagnosticLog` bound
once the runner has opened the run's log.

Four types the plan listed here stay in `Curl.Console` because each uses a tool-side type that has
not moved yet: `ConnectionIdRecordingTransferEvents` and `TlsResultRecordingTransferEvents` (both use
`RunningTransferState`), `MultiStateTraceEvents` (uses `TransferTimers` from `Curl.Engine` and other tool-side types) and
`GatedDiagnosticLog` (uses `WriteGate`).

## Authentication and security-context sources (BL-2286)

`TransferCredentialLookup` then chooses the transfer's credentials, once per URL. When `-u` gives
no user name, the URL's percent-decoded user name and password are sent (`http://zz@host/` sends
`zz:`, `http://:x@host/` sends `:x`, `http://@host/` nothing), replacing a `-u :pw` whole (BL-791).
Under `-n`, `--netrc-file` or `--netrc-optional` the netrc file has its say too, and the runner keeps them as the running transfer's
`LookedUpCredentials`, which `TransferContextFactory` puts on every attempt's context in place of
`-u`'s. A `-u` with a user name wins and no file is read. The file is the `--netrc-file` one, else the one a
non-empty `NETRC` names (BL-2114), else `.netrc` in `HOME` (on Windows `_netrc` after it, and `USERPROFILE` when `HOME` is not set), read
through the runner's `IDataFileReader` and environment. The URL's percent-decoded user name picks
the entry (`Curl.Authentication`'s `NetrcFile`), whose password beats the URL's; an entry with no
login takes the URL's user name, and one with no password sends an empty one, never the URL's
(BL-1356); with no entry the URL's user and password are sent. Under `--netrc-optional` a URL
with a password is not looked up at all, and its own credentials are sent (upstream test381,
BL-1986). A NUL byte in the file ends its line (upstream test793). A required file that is missing or malformed fails each URL with
`curl: (26) .netrc error: no such file` or `syntax error` before anything is sent, `-v` writing
the same text as an info line first (BL-1447);
`--netrc-optional` ignores both. When the file is in use, `TransferCredentialLookup.ForRedirectHops`
gives `RedirectFollower` the same lookup for each redirect hop's URL, so every hop sends its own
host's entry or none, `--location-trusted` or not (BL-790). Measured on curl 8.21.0 (BL-505 Notes).
URL credentials that percent-decode to a byte below 0x20 (only `%00` for `http`, `https`, `ws` and
`wss`) fail with `curl: (3) error extracting credentials from URL` before connecting, a redirect
hop's too, and a matching netrc entry holding one with `curl: (26) control code detected in .netrc
credentials` except over those four schemes; `-v` writes each as an info line first (BL-1411).

The other types moved with it, in `Curl.Engine`: `AwsSigV4HttpAuthenticator` signs every request
with `--aws-sigv4` in place of the ranked schemes (`HttpRequestOptions.AwsSigV4`, ADR-0243) and
`HttpSignatureHttpAuthenticator` signs with the `--httpsig-*` options' `Signature-Input` and
`Signature` lines in place of all of them (`HttpRequestOptions.HttpSignature`, BL-2182, ADR-0485);
`HttpSignatureSetup` runs curl 8.22.0's setup checks for those options at each URL, refusing with
exit 2, 26 or 43 and its `errorf` line. `HandBuiltKerberosSources` reads `krb5.conf` and the
credential cache from disk (`KerberosDiskFileReader`, `KerberosDiskFileWriter`) only when a
hand-built context first asks (BL-527, ADR-0176); `HandBuiltNtlmSecurityContextFactory` answers NTLM with the
hand-built factory on every platform, other schemes with the router (BL-1858); `LateBoundSecurityContextFactory` is an `ISecurityContextFactory` bound
once the connectors exist (ADR-0270); `NegotiateOptionsMapping` and `Socks5AuthenticationMapping`
map the `--negotiate` and `--socks5-*` options to their settings (ADR-0276, BL-615); `NoCryptoEngines`
is what `--engine` does with no crypto engines (ADR-0151).

`NoCryptoEnginesTests` and `Socks5AuthenticationMappingTests` stay in `Curl.Console.UnitTests`
because each drives `CurlCommandRunner` or `CurlComposition`, which are tool-side.

The per-transfer stores moved too (BL-2287), in `Curl.Engine`: `CookieEngine` is the cookies of
one run, as curl's tool sets them up from `-b`, `-c` and `-j`; `HstsTransferPolicy`'s test clock
is `FixedWallClock` and `ClockOverridingHstsStore` (a `CURL_TIME` of whole seconds, ADR-0493);
`SshKnownHostsFileSearch` finds the known-hosts file a scp or sftp transfer checks (ADR-0122);
`CRuntimeErrorNumbers` words a C runtime errno as `strerror` does; and `CurlErrorBuffer` cuts a
message to curl's 255-byte error buffer (ADR-0072; `Curl.Networking` has its own internal copy).

`TlsSessionFileLines`, with its tests, stays in `Curl.Console` because it uses tool-side types
(`ToolNotes`). `HstsCacheFile` stayed too until BL-2291 gave it its own `0666` create mode in
place of `DeferredOutputFileStream.CreateMode` and moved it here. `CookieEngineTests` uses its own read-only file system fake,
and `SshKnownHostsFileSearchTests` links `InMemoryDataFileReader` from `Curl.Console.UnitTests`.

## Option mappings and transfer plumbing (BL-2288)

Moved here, in `Curl.Engine`, then reading `CommandLineOptions` (BL-2303 to BL-2358 retargeted them
to the seam's option groups): the mappings from command-line options to protocol option records
(`HttpRequestOptionsMapping`, `HttpVersionMapping`, `MailRequestOptionsMapping`,
`MultipartFormPartMapping`, `RedirectPolicyMapping`, `SshOptionsMapping`, `TlsClientOptionsMapping`),
`CurlTransports` (the run's shared connectors and resolvers), `TransferProxySelection`,
`TransferTimers`, `TlsBuildInformation` and `UnusableDohUrlResolver`. `RetryPolicyMapping` stays in
`Curl.Console`: `--retry` is the curl tool's.

`CurlTransportsTests`, `HttpVersionMappingTests` and `TransferProxySelectionPreProxyTests` stay in
`Curl.Console.UnitTests`. `TlsClientOptionsMappingTests` links `OpenSslBuildParser` from
`Curl.Cli.UnitTests`.

## The engine's composition (BL-2289)

`EngineComposition` builds the engine's services with plain constructor calls - no container, no
reflection, so native AOT sees every type: the protocol handlers (`CreateProtocolHandlers`), the
transports (`CreateTransports`, `CreateTcpConnector`, `CreatePoolingConnector`,
`CreateUdpDatagramConnector`, `CreateDatagramConnectorQuicDialer`), the DNS resolvers
(`CreateDnsResolver`, `CreateDohResolver`, `DnsServerVariableResolverOf`), the TLS providers
(`CreateTlsProvider`), the security-context factories and authenticators
(`CreateSecurityContextFactory`, `CreateDialingSecurityContextFactory`, `CreateHttpAuthenticator`,
`CreateSaslAuthenticator`), the `--trace-config` predicates (`Traces*`) and the option readers
(`AddressFamilyOf`, `ConnectTimeoutOf`, `LocalBindingOf`, `PreProxyOf`, `UnixSocketOf` and the
like). `Curl.Console`'s `CurlComposition` keeps `CreateRunner` and calls these. curl's easy error
text stays in `Curl.Console` (`CurlEasyErrorText`, which `Gap/` tooling reads by path), so the DoH
resolver's text comes in through the `easyErrorText` parameter of `CreateTransports`,
`CreateDnsResolver` and `CreateDohResolver`; without one it is empty. `OptionGroupConnectionSettings`
and `ProcessUserId` moved here with it.

What the factories do, moved from `Curl.Console/CLAUDE.md` (BL-2322):

`CreateTransports` wraps an option group's `TcpConnector` in one `PoolingConnector`,
which every TCP handler connects through, so a later URL to the same pool key reuses an
earlier URL's connection; `Curl.Engine`'s `TransferDispatch` holds it as its `ConnectionPool`. In production the
connector shares the run's `ConnectionCache`, which the runner closes without writing anything once
the run's transfers end, whatever their outcome (ADR-0050, BL-334, ADR-0285); a connector made
without one owns its pool, which disposing the dispatch closes. It also holds the `TcpConnector`'s
`LoadResolveEntries`, which the runner calls at the start of every URL's transfer, just before
the `-b` files load, so `-v` prints the `--resolve` entries' `Added ... to DNS cache` lines for
each URL; a `--retry` attempt and a followed redirect reload nothing, as in curl 8.21.0 (BL-486).
The origin's and the HTTPS proxy's TLS providers come from `CreateTlsProvider`,
which builds `HandBuiltTlsProvider` when `TlsClientRouting.Choose` routes the options to the
hand-built client (a `--tls-max` of 1.0 or 1.1 today) and `SslStreamTlsProvider` otherwise
(ADR-0140, ADR-0162, BL-708).

`CreateProtocolHandlers` builds the handlers behind the engine's end-point recorders and `LazyProtocolHandler` (ADR-0119, BL-515, BL-1715, AF-0063).

Their tests are `EngineComposition*Tests` in `Curl.Engine.UnitTests` (with their own copies of the
`LoopbackDnsResolver` and `ScriptedTcpDialer` fakes, and a reference to `Curl.Protocol.Ssh.UnitTests`
for its in-memory SSH server) and `EngineCompositionDnsServersIntegrationTests` in
`Curl.Engine.IntegrationTests`. Tests that run a transfer through `CurlComposition.CreateRunner`,
`CreateTransferDispatch` or `CreateDispatcher` stay in `Curl.Console.UnitTests` as
`CurlComposition*Tests`.

## The transfer context and dispatch (BL-2290)

`TransferContextFactory` builds the `TransferContext` of each URL of an accepted command line,
carrying every option a handler reads: the clock, the run's diagnostic log and HSTS store (both set
by the runner once it has opened them), the `-Y`/`-y` and `-m` watchdogs' wrapped output, progress
sink and cancellation token, the `-D`/`-i`/`-I` header output (`HeaderLineTeeStream` writes each line
to the `-D` output and then the body output) and the `CONNECT` reply heads. Standard input comes in
through its constructor, for a `telnet` transfer's upload. `TransferDispatch` holds one option
group's dispatcher, cookies, proxy selector, connection pool and `--resolve` loader, which the
runner disposes when the group ends; `CurlCommandRunner` receives a
`Func<CommandLineOptions, TransferDispatch>` that returns it.

`AltSvcTransferCache` moved with the factory (the factory stores it on `HttpRequestOptions.AltSvcStore`
and asks it for the route), so the earlier note that it stays in `Curl.Console` no longer holds; it
carries its own copy of the `0666` create mode `DeferredOutputFileStream.CreateMode` holds, so it no
longer reaches a tool-side type.

Their tests moved too: `TransferContextFactoryTests`, `TransferContextFactoryMailTests`,
`AltSvcTransferCacheTests` and `HeaderLineTeeStreamTests` are in `Curl.Engine.UnitTests`, linking
`ManualTimeProvider` and `SteppingTimeProvider` from `Curl.Console.UnitTests`.

## The transfer performer (BL-2291)

`TransferPerformer` (`TransferPerformer.cs` and `TransferPerformer.Setup.cs`, built in `Curl.Console`
by BL-2282 to BL-2284) is here: one attempt under its watchdogs, the run's `%{xfer_id}` and
`%{conn_id}` numbering, and the per-transfer setup and cleanup libcurl does. What it keeps about a
running transfer is `EngineTransferState` (the attempt's watchdogs, the retry and connection IDs, the
looked-up credentials, the SSH options and the alt-svc cache); `Curl.Console`'s `RunningTransferState`
derives from it and adds the tool's part. curl's easy error text comes in through the constructor's
`easyErrorText` (`CurlEasyErrorText.Of` in production, which stays in `Curl.Console`), so
`KnownHostsFileMissingFailure` is one per performer and `ResolveSshOptionsAsync` and
`SetupFailureAsync` are instance methods. `HstsCacheFile` and `UrlEffective` (`%{url_effective}`,
which the HSTS switch line prints) moved with it. `TransferPerformerTests` and `UrlEffectiveTests`
are in `Curl.Engine.UnitTests`; the setup tests use their own `ReadRecordingFileSystem` fake, as
`Curl.Console.UnitTests`' `InMemoryFileSystem` uses tool-side types.

## The network option group (BL-2323)

`Curl.Engine.UnitLibrary` references `Curl.Api.UnitLibrary` for its option records. `EngineComposition`'s transport, TCP connector, pooling connector and DNS resolver factories take `EasyNetworkOptions` beside `CommandLineOptions` (then still read for TLS, proxy, trace, HTTP version and `-m`; BL-2324, BL-2325 and BL-2350 moved TLS, proxy and the HTTP version to their records), and `CreateUdpDatagramConnector`, `LocalBindingOf`, `UnixSocketOf`, `AddressFamilyOf`, `ConnectTimeoutOf`, `DnsServerVariableResolverOf`, `TcpSocketOptionsOf`, `TransferTimers.Of` and `OptionGroupConnectionSettings.Of` read their network settings from it alone (ADR-0526). `Curl.Console` builds the record with `EasyOptionsFromCommandLine.NetworkOf`, which `Curl.Engine.UnitTests` and `Curl.Engine.IntegrationTests` link. `TransferContextFactory.Create` takes it too and reads `--connect-timeout` (the record's zero, not set, becomes a `null` `TransferContext.ConnectTimeout`) and the `--connect-to` mappings `ConnectToTunnelsThroughProxy` checks from it (BL-2343); `-m` still comes from `EasyTransferOptions`.

## The TLS option group (BL-2324)

`TlsClientOptionsMapping` (`Of`, `ProxyOf`, `DohOf`), `TlsBuildInformation`, `CreateTransports`, `CreateDnsResolver`, `CreateDohConnector`, `CreatePoolingConnector` and `OptionGroupConnectionSettings.Of` read TLS settings from `EasyTlsOptions`, which `Curl.Console` builds with `EasyOptionsFromCommandLine.TlsOf`; `--no-alpn` and `--ssl-sessions` still come from `CommandLineOptions` (ADR-0530).

## The form body (BL-2344)

`MultipartFormPartMapping.FromMimeBody` builds the builder's parts from `EasyHttpOptions.MimePost`, which `Curl.Console` builds with `EasyOptionsFromCommandLine.HttpOf`: an `EasyMimeFile` or `EasyMimeStandardInput` part with a file name is an upload, one without is file content, `EasyMimeBytes` is UTF-8 text, and `SubParts` is a multipart. `NameEscapingOf` and `RequestContentTypeOf` still read `CommandLineOptions` (ADR-0531).

## The HTTP request, version and redirect mappings (BL-2345)

`HttpRequestOptionsMapping.Of`, `RedirectPolicyMapping.Of` and `TransferContextFactory.Create` take `EasyHttpOptions` for the headers (with `--json`'s), `-A`, `-e`, `--request-target`, the `-d` body (`PostFields`, `null` under `-G`), the encodings (`Compressed` is a set `AcceptEncoding`, `Raw` both decodings off), `-L`/`--follow`, `--max-redirs`, `--post30x`, the HTTP version, `--http0.9`, `--ignore-content-length` and `--expect100-timeout`; proxy headers, `--unix-socket`, `--happy-eyeballs-timeout-ms` and the `--proto` options still come from `CommandLineOptions`, and authentication from `EasyAuthenticationOptions` (BL-2352). `HttpVersionMapping` maps `Curl.Api`'s `HttpVersion` (ADR-0532). `CreateTransports`, `CreateTcpConnector`, `CreatePoolingConnector` and `OptionGroupConnectionSettings.Of` take `EasyHttpOptions` beside the other groups, and the ALPN list, `HttpsConnectFirstAttemptVersionOf` and the HTTPS-CONNECT second attempt read its `HttpVersion` (BL-2350); `--no-alpn` and `--ssl-sessions` still come from `CommandLineOptions`.

## The cookie, alt-svc and HSTS settings (BL-2346)

`CookieEngine.FromHttpOptions`, `AltSvcTransferCache.OpenAsync`, `HstsCacheFile.ReadAsync` and `WriteAsync`, and `TransferPerformer`'s `SwitchToHttpsForHstsAsync`, `OpenAltSvcCacheAsync` and `WriteCookieJarAltSvcAndHstsFilesAsync` take `EasyHttpOptions` for `-b` (`Cookie`, the strings joined as curl's `cookie_setopts` joins them, and `CookieFiles`), `-c`, `-j`, `-H` (an `-H` naming `Cookie` drops the strings), `--alt-svc`, the HTTP version the alt-svc lookup keys on, and `--hsts`; the `-G` / `--url-query` query, `--path-as-is` and `--connect-to` still come from `CommandLineOptions` (ADR-0533).

## The behaviour and transfer settings (BL-2347)

`TransferContextFactory.Create` takes `EasyBehaviourOptions` (`-i`) and `EasyTransferOptions` beside `EasyHttpOptions` for the `-r` text, `-I`, `-R` (`FileTime`), `-z` (`TimeValue` in whole seconds, so a date past 9999 keeps its seconds), `-B`, `--crlf`, `--max-filesize` and `-m` (zero is none, as libcurl's), and a `-T` upload under `-C -` (`Upload` with `ResumeFrom` -1, which the runner sets per transfer); `HttpRequestOptionsMapping.Of` reads `-X` and `-f` from them (`--fail-with-body` still from `CommandLineOptions`), `MailRequestOptionsMapping.FromCommandLine` reads `-X`, and `TransferPerformer.StartLowSpeedWatchdog`, `StartMaxTimeWatchdog` and `TransferTimers.Of` read `-Y`/`-y` and `-m` (ADR-0534).

## The protocol settings (BL-2348)

`TransferContextFactory.Create` takes `EasyProtocolOptions` for FTP's `-P`, `-Q` (`Quote`, `PreQuote` behind `+` and `PostQuote` behind `-`, one list the handlers sort by prefix), `-a`, `--disable-epsv`/`--disable-eprt`, `--ftp-pret`, `--ftp-create-dirs`, `--ftp-alternative-to-user`, `--ftp-skip-pasv-ip`, `--ftp-ssl-ccc`, `--ftp-account` and `--ftp-method`, and for `--tftp-blksize` (512 is `null`, not given), `--tftp-no-options` and `-t`; `MailRequestOptionsMapping.FromCommandLine` reads the sender, recipients, `AUTH` and allow-fails from `EasySmtpOptions`; `TransferPerformer.ResolveSshOptionsAsync` and `SshOptionsMapping.Of` read the SSH keys, fingerprints, compression and `--knownhosts` from `EasySshOptions` (`--pass`, `-k`, `-s` and `-S` still from `CommandLineOptions`). `RoutingFtpProtocolHandler`, `CreateFtpProtocolHandler` and `SshKnownHostsFileSearch` read no option (ADR-0535).

## The proxy settings (BL-2325)

`TransferProxySelection.TrySelect`, `EngineComposition.PreProxyOf`, `HaproxyProtocolOf`, `CreateProxyTunnelOptions`, `Socks5AuthenticationMapping.Of` (was `FromCommandLine`), `TransferPerformer.TrySelectProxyAndCredentials` and `TransferContextFactory.Create` (`-p`, `--suppress-connect-headers`) read the proxy, pre-proxy, proxy type, no-proxy list, proxy headers, SOCKS5 and HAProxy settings from `EasyProxyOptions`, which `Curl.Console` builds with `EasyOptionsFromCommandLine.ProxyOf`; `CreateTransports`, `CreateTcpConnector`, `CreatePoolingConnector` and `OptionGroupConnectionSettings.Of` take it beside the other groups. `ProxyType` is the kind of proxy text with no scheme, and `Https2` stays HTTP and asks the tunnel for HTTP/2. `-U` still comes from `CommandLineOptions`, `--proxy-service-name` and `--delegation` from `EasyAuthenticationOptions` (BL-2353), and `--connect-to`'s tunnelling from `EasyNetworkOptions` (BL-2343) (ADR-0536). Their record-driven tests are `EasyProxyOptionsReadingTests`.

## The credential lookup (BL-2338)

`TransferCredentialLookup` (`TryLookUp`, `TryCheckUrlCredentials`, `ForRedirectHops`) and `TransferPerformer.TrySelectProxyAndCredentials` read `-u`, `-n`, `--netrc-file` and `--netrc-optional` from `EasyAuthenticationOptions`, which `Curl.Console` builds with `EasyOptionsFromCommandLine.AuthenticationOf`: `Netrc` and `NetrcFile` choose the file, and `-u` wins when `UserName`, else `UserPassword` up to its first colon, is not empty (ADR-0516). `-U` and `--unix-socket` still come from `CommandLineOptions`. `NegotiateOptionsMapping.Of` (was `FromCommandLine`), `CreateTransports`, `CreateTcpConnector` (SOCKS5 GSS-API) and `CreateProxyTunnelOptions` read the service names, `--delegation` and the proxy's auth methods from it too, `HttpRequestOptionsMapping.ProxyAuthSchemesOf` turning `ProxyAuthentication` back into the tunnel's schemes (`Any` is `Any`, each bit its scheme, Basic when none is left); `CreatePoolingConnector` reads no authentication setting and takes no record (BL-2353). `HttpSignatureSetup.Of`, `HttpRequestOptionsMapping.Of` (the methods, bearer token, AWS SigV4 and HTTP signature), `MailRequestOptionsMapping.FromCommandLine` (login options, SASL, bearer, service name), `RedirectPolicyMapping.Of` (`--location-trusted` as `UnrestrictedAuthentication`, `--disallow-username-in-url`) and `TransferContextFactory.Create` (`-u`'s `UserPassword`, split at its first colon) read it too (BL-2352). `HttpRequestOptionsMapping.AuthSchemesOf` turns its `CURLAUTH_*` bits into the handler's schemes as `CommandLineOptions.AuthSchemes` gives them: `Any` is every scheme the engine knows, Bearer only with `XOAuth2Bearer` set, the `AwsSigV4` bit ignored, and Basic when none is left. Their record-driven tests are `EasyAuthenticationOptionsReadingTests` and `EasyAuthenticationOptionsMappingTests`.

## The transport factories' tool settings (BL-2356)

`CreateTransports`, `CreateTcpConnector`, `CreatePoolingConnector`, `WaitsForMultiplexing` and `OptionGroupConnectionSettings.Of` take an `EngineToolOptions` (`-Z`, `--parallel-immediate`, `--ssl-sessions` and the `-m` limit the trace timer lines report), which `Curl.Console` builds with `EasyOptionsFromCommandLine.ToolOptionsOf`; ALPN is `EasyHttpOptions.SslEnableAlpn`, `CreateProxyTunnelOptions` takes `EasyHttpOptions` for `-A`, and `CreateDnsResolver`, `CreateDohConnector` and `CreateDatagramConnectorQuicDialer` take no command line. `--interface` is parsed by `EngineInterfaceBinding` (ADR-0538).

## The transfer settings (BL-2357)

`TransferPerformer`'s setup steps (`TryParseTransferUrl`, `RefuseMalformedSetopt`, `TrySelectProxyAndCredentials`, `ResolveSshOptionsAsync`, `LoadCookieFilesAsync`, `SwitchToHttpsForHstsAsync`, `OpenAltSvcCacheAsync`, `SetupFailureAsync`, `WriteCookieJarAltSvcAndHstsFilesAsync`), `TransferContextFactory.Create`, `RedirectPolicyMapping.Of`, `HttpRequestOptionsMapping.Of` and `MailRequestOptionsMapping.FromCommandLine` take an `EngineTransferSettings` for the curl tool's own settings (`-s`, `-S`, `--fail`, `-k`, `--pass`, `--engine`, `--ech`, `-d`, `-U`, `--proto`, `--proto-redir`, the `--ssl*` level, `-l`, `--upload-flags`, `--proxy-header`, `-p`, `--unix-socket`, `--happy-eyeballs-timeout-ms` and the `-G` / `--url-query` query), which `Curl.Console` builds with `EasyOptionsFromCommandLine.TransferSettingsOf`; `--path-as-is`, `--interface` and `--connect-to` come from `EasyNetworkOptions` (ADR-0539).

## The last Curl.Cli types in the mappings and lookups (BL-2358)

`MultipartFormPartMapping.NameEscapingOf` takes whether `--form-escape` was given and `RequestContentTypeOf` the `-H` values; `HttpRequestOptionsMapping.HappyEyeballsTimeoutOf` takes the `--happy-eyeballs-timeout-ms` value; `EngineTransferSettings.UploadFlags` is the names of the `--upload-flags` flags set, in curl's order, which `Curl.Console` works out (`EasyOptionsFromCommandLine.TransferSettingsOf`) so `MailRequestOptionsMapping` no longer names `ImapUploadFlags`. `TransferCredentialLookup` and `SshKnownHostsFileSearch.Find` read files through a `Func<string, byte[]?>` (`null` when unreadable) that `CurlCommandRunner.ReadDataFile` supplies from its `IDataFileReader`, so neither names `Curl.Cli`.

## The managed engine and its easy handle (BL-2316)

`ManagedCurlEngine` is the `ICurlEngine` behind `Curl.Api` and `Libcurl` (ADR-0506, ADR-0541). Its
public constructor runs on the system clock and the real network; the internal one takes a
`TimeProvider` and a `Func<EasyOptions, ProtocolDispatcher>`, which tests use to put a fake
connector under the handlers. Its connection, TLS session and DNS caches are fields of the engine
object, never static, so two engines in one process share nothing; `DisposeAsync` closes its pooled
connections. `CreateDispatcher` builds a transfer's dispatcher as `Curl.Console`'s composition
builds an option group's: `EngineComposition.CreateTransports` over the handle's easy groups and the
engine's caches, then `CreateProtocolHandlers` over its pooling connector. `CreateMulti`,
`CreateShare`, `CreateUrl`, `CreateMime` and `CreateForm` throw `NotSupportedException`: they are
phase 3 work.

`ManagedEasyTransfer` is its `IEasyTransfer`. `PerformAsync` reads `EasyNetworkOptions.Url` (none is
`CurlCode.UrlMalformat` with `No URL set`, one that does not parse the same code with libcurl's
malformed-URL text), builds the context with `TransferContextFactory.Create` from the easy groups and
`TransferSettingsOf`'s `EngineTransferSettings` (what the easy groups say for verify-peer, the key
password, the crypto engine, the proxy tunnel and the Unix socket, libcurl's defaults for the rest),
and performs the attempt with `TransferPerformer.FollowWatchingSpeedAsync` through a
`RedirectFollower`. Body bytes go to `EasyCallbackOptions.Write` and header lines to
`EasyCallbackOptions.Header` through `CallbackWriteStream`, each discarded when unset; a callback that
takes fewer bytes than it was given fails the transfer with `CurlCode.WriteError`. The result's exit
code becomes the `CurlCode`, its message `ErrorText`, and `Info` is what `TransferInfoFromReport.Of`
fills from the result's `TransferReport` (BL-2317): every `CURLINFO_*` the report holds, those a `-w`
variable reads filled by `Curl.Output`'s `WriteOutTransferInfo.FromReport`, which `-w` reads them through too (ADR-0553, BL-2319) (times in whole
microseconds from the handler's start, at least one; speeds over the total time), plus the
`CURLAUTH_*` bits a 401's `WWW-Authenticate` or a 407's `Proxy-Authenticate` lines offer, the last
`Content-Length`, the effective URL's scheme and `CURLPROTO_*` bit, and `CURLINFO_FILETIME` only
under `EasyTransferOptions.FileTime`. BL-2361 added `CURLINFO_RETRY_AFTER` (the last response's
`Retry-After`, read by `Curl.Core`'s `RetryAfterHeader`), `CURLINFO_CONTENT_LENGTH_UPLOAD`
(`ManagedEasyTransfer.ContentLengthUploadOf`: `CURLOPT_INFILESIZE` under `CURLOPT_UPLOAD`, else a
post's `CURLOPT_POSTFIELDSIZE` or body length, else -1) and `CURLINFO_COOKIELIST`
(`CookieEngine.ListLines`, the shared store's cookies as Netscape lines while a `-b` file or `-c`
turns the cookie engine on). `CURLINFO_CONN_ID` came with BL-2362 (below), and `CURLINFO_SSL_VERIFYRESULT`
and `CURLINFO_PROXY_SSL_VERIFYRESULT` with BL-2366: `TlsVerifyResultRecordingTransferEvents`, the engine's
copy of the runner's `TlsResultRecordingTransferEvents`, keeps the verify codes the transfer reports and
puts them on its `TransferReport` (`SslVerifyResult`, `ProxySslVerifyResult`). BL-2367 added
`CURLINFO_PROXYAUTH_USED` (the report's `ProxyAuthSchemePicked`, the scheme the HTTP handler answered a
forward proxy's 407 with) and the RTSP handler's `CURLINFO_RTSP_SESSION_ID`, `RTSP_CLIENT_CSEQ`,
`RTSP_SERVER_CSEQ` and `RTSP_CSEQ_RECV`. BL-2374 added `CURLINFO_OS_ERRNO`: `TcpConnector` puts a failed dial's last `SocketError` on
`ConnectResult.LastSocketError`, `EndPointRecordingConnector` hands it to `ConnectionEndPointRecorder`,
which keeps it as the platform's number (`SocketException.NativeErrorCode`: the Winsock code on
Windows, `errno` elsewhere) and puts it on `TransferReport.OsErrno`. BL-2375 added
`CURLINFO_PROXY_ERROR` the same way: `Curl.Networking`'s SOCKS handshakes put the `CURLPX_*` code upstream's
`lib/socks.c` gives each failure on `ConnectResult.ProxyError` (`CurlProxyCode`), and the recorder puts the
last one on `TransferReport.ProxyError`. BL-2368 added `CURLINFO_CERTINFO` under `EasyTlsOptions.GatherCertificateInfo` (`CURLOPT_CERTINFO`):
one list per `TransferReport.PeerCertificates` entry, each `label:value` line of `Curl.Output`'s
`PeerCertificateText` one string and its PEM block the last, `Cert:` and the block
(`TransferInfoFromReport.CertInfoRecordsOf`). The rest keep their fresh-handle defaults until BL-2369. `Duplicate` copies the options and none of the results, `Reset` restores
`new EasyOptions()`, `Pause` records the directions and returns `Ok`, and `UpkeepAsync` returns
`Ok`. `Headers`, `WebSocket` and `Connection` throw `NotSupportedException` (phase 3). Their tests are
`ManagedCurlEngineTests`, `ManagedEasyTransferTests` and `CallbackWriteStreamTests`.

## The tool's settings and the engine-wide state (BL-2359)

`ManagedEasyTransfer.TransferSettingsOf` maps the easy options libcurl has a `CURLOPT_*` for onto `EngineTransferSettings`: `EasyNetworkOptions.Protocols` and `RedirectProtocols` (`ProtocolSetOf`: comma-separated, trimmed, lower cased) as `--proto` and `--proto-redir`, `EasyTransferOptions.DirListOnly` as `-l` and `EasyProxyOptions.ProxyHeaders` as `--proxy-header`. The internal `ToolSettings` replaces them whole when set: it is for `Curl.Console` to set from `EasyOptionsFromCommandLine.TransferSettingsOf` once its runner performs through the engine (BL-2318), for the settings that are the tool's alone (`-s`, `-S`, `--fail-with-body`, the `-G` query and the rest); `Duplicate` copies it and `Reset` clears it. Under `EasyTransferOptions.Upload` the handler reads `EasyCallbackOptions.Read` through `CallbackReadStream` (0 ends the upload; below 0 or past the buffer is an `IOException` with libcurl's `read function returned funny value`), and with no read callback the upload is empty. Everything an engine's handles share is one `ManagedEngineSharedState`: the connection, TLS session and DNS caches, a `CookieStore` every handle's HTTP handler keeps cookies in when `CookieEngine.FromHttpOptions` turns the engine on, an `HstsTransferPolicy` on every context, and a `TransferPerformer` that gives each perform the next `TransferInfo.TransferId` and holds curl's easy error text, which `ManagedCurlEngine`'s internal constructor takes (empty from the public one). The internal constructor's dispatcher factory gets the options, the shared state and the cookie store (ADR-0542).

## The cookie, alt-svc and HSTS files and `%{conn_id}` (BL-2362)

`ManagedEasyTransfer.PerformAsync` does the runner's setup and cleanup around its attempt with `TransferPerformer`'s own steps, in the runner's order: `LoadCookieFilesAsync` (the `-b` files into the shared `CookieStore`), `OpenAltSvcCacheAsync` (the transfer's own `AltSvcTransferCache`, kept on its `EngineTransferState` and put on the context) and `SwitchToHttpsForHstsAsync` (the `--hsts` file read, an `http` URL the cache knows switched to `https`) before connecting, and `WriteCookieJarAltSvcAndHstsFilesAsync` after it, whatever the outcome. What those steps reach outside the engine comes from the internal `ManagedEasyTransfer.Host`, an `EasyTransferHost`: the `IFileSystem`, standard input for `-b -`, the `-c -` print target, the diagnostic log every context carries, the `--hsts` line endings and `CURL_ALTSVC_HTTP`. The default, `EasyTransferHost.Library`, is `PhysicalFileSystem` with nothing to read for `-`, `Stream.Null` for `-c -` and no log; `Curl.Console` sets its own once its runner performs through the engine (BL-2318). `Duplicate` copies it and `Reset` keeps it. Every transfer's events go through `EngineConnectionIdTransferEvents`, the engine's copy of the runner's `ConnectionIdRecordingTransferEvents`, so `TransferInfo.ConnectionId` is the engine performer's next connection number for a new connection and the opener's for a reused one (`ManagedEngineSharedState.ConnectionIdsByPoolNumber`), and -1 (`ManagedEasyTransfer.NoConnectionId`) after an unsupported scheme or malformed URL (ADR-0544). Their tests are `ManagedEasyTransferTests` (`ManagedEasyTransferTests.Files.cs`) and `EngineConnectionIdTransferEventsTests`.

## The debug and progress callbacks (BL-2360)

Under `EasyBehaviourOptions.Verbose` with `EasyCallbackOptions.Debug` set, `ManagedEasyTransfer` gives the context a `DebugCallbackTransferEvents`: an info line goes to the callback as `CurlInfoType.Text` with its line feed, a connection opened or reused and a TLS handshake, trust or message as the `Text` lines `-v` prints (`Curl.Output`'s `TransferEventInfoText`, the running platform's build), head bytes as `HeaderOut` and `HeaderIn`, body bytes as `DataOut` and `DataIn`, and TLS record bytes as `SslDataOut` and `SslDataIn` in the OpenSSL build only. Without `EasyBehaviourOptions.NoProgress`, `CallbackTransferProgress` calls `EasyCallbackOptions.TransferInfo`, else `Progress`, on every progress report with the download total and count and the upload total and count (an unknown total as 0); a return other than 0 or `ProgressCallbackResult.Continue` cancels the attempt and the perform returns `CurlCode.AbortedByCallback` with `Callback aborted`. `DebugCallback` keeps libcurl's signature (ADR-0543); for the `--trace-ids` prefix the callback reads the handle's `Info`, which holds the running transfer's `TransferId` from its start and its `ConnectionId` (-1 before) as soon as `EngineConnectionIdTransferEvents` takes one, before the event that opened or reused the connection reaches the callback (BL-2364, ADR-0547).

## The trace components (BL-2363)

The internal `ManagedEasyTransfer.TraceComponents` (`EngineTraceComponents.None` by default; `Duplicate` copies it, `Reset` keeps it) is libcurl's `curl_global_trace`, for `Curl.Console` to set (BL-2318). `WithTraceComponentEvents` wraps the debug events in the runner's decorators in the runner's order: `ResponseWaitTimerTraceEvents` under `timer` (the `CURLOPT_TIMEOUT` and `CURLOPT_CONNECTTIMEOUT` timers, written as with the multi not traced), `ClientReaderResetTraceEvents` under `read` and `ClientWriterTraceEvents` outermost under `write`. `MultiStateTraceEvents` and `AsyncResolveTeardownTraceEvents` stay in `Curl.Console` (ADR-0545).

## The curl tool's prepared attempt (BL-2318)

`ManagedEasyTransfer` implements the internal `IToolAttemptTransfer`: when `ToolAttempt` is set (a `ToolAttempt` record: the running transfer's `EngineTransferState`, the `RedirectFollower`, the attempt's `TransferContext` and the `RedirectPolicy`), `PerformAsync` clears it and performs that attempt with `TransferPerformer.FollowWatchingSpeedAsync` in place of one built from the options (`PerformFromOptionsAsync`), keeps the whole result as `ToolAttemptResult`, and fills `ErrorText` and `Info` from it (`TransferInfoFromReport.Of` on `Options`' URL, the state's `%{conn_id}` or -1). `Curl.Console`'s runner performs each serial attempt this way (ADR-0552); BL-2394 moves the attempt's wiring into the handle and retires the hook. Its tests are `ManagedEasyTransferTests.ToolAttempt.cs`.

## The runner's chosen pieces and the last result (BL-2395)

`ManagedEasyTransfer.LastResult` is the whole `TransferResult` of the last perform, from the options or a `ToolAttempt` (`null` before one, after one that failed before connecting and after a progress-callback abort), for the runner's `--retry`, failure lines and `-w`. The internal `ToolInputs` (`EngineToolTransferInputs`) carries what the runner chose before connecting - proxy, looked-up credentials, SSH options, `If-None-Match` lines, `-r` range, `-C` offset, `-F` body, `-T` source and alt-svc cache - which a perform from the options passes to `TransferContextFactory.Create` as the runner does; while it is set the handle reads and writes no cookie, alt-svc or HSTS file, leaving them to the runner. `Duplicate` does not copy it and `Reset` clears it (ADR-0554). Its tests are `ManagedEasyTransferTests.ToolInputs.cs`.

## The runner's body and header outputs (BL-2396)

The internal `ManagedEasyTransfer.ToolOutputs` (`EngineToolTransferOutputs`: the body stream as the runner chains it through `--limit-rate`, `-N`, `--use-ascii` and its standard-output or file stream, the `-D` stream, the `-J` header watcher and the `--styled-output` styles) goes to `TransferContextFactory.Create` as the runner passes them, in place of `EasyCallbackOptions.Write` and `Header`; without it the handle builds the same record from those callbacks (`OwnOutputsOf`). `Duplicate` does not copy it and `Reset` clears it. A write or header callback that returns anything but the chunk's length fails with `OutputWriteFailedException` and `Failure writing output to destination, passed N returned M`, the runner's exit 23 text (ADR-0558). Its tests are `ManagedEasyTransferTests.ToolOutputs.cs`, which run each case through `ToolOutputs` and through a `ToolAttempt` and compare the writes.

## The runner's -v and --trace events (BL-2397)

The internal `ManagedEasyTransfer.ToolEvents` (`EngineToolTransferEvents`: the runner's whole events chain - its `-v` and `--trace` writer, `TraceIdsTransferEvents`, `MultiStateTraceEvents`, its `--trace-config` decorators and `ConnectionIdRecordingTransferEvents` - and the running transfer it keeps) is where a perform from the options reports every event while it is set, in place of `EasyCallbackOptions.Debug` and the `TraceComponents` decorators; the perform runs on its `State` and builds no `EngineConnectionIdTransferEvents`, so `%{conn_id}` is numbered once, by the runner's recorder, and `Info.ConnectionId` is what it kept (`StateAndEventsOf`). `TlsVerifyResultRecordingTransferEvents` still wraps it. The debug callback keeps libcurl's signature and cannot carry the structured events, so this is an internal hook (ADR-0561). `Duplicate` does not copy it and `Reset` clears it. Its tests are `ManagedEasyTransferTests.ToolEvents.cs`, which run a transfer through `ToolEvents` and through a `ToolAttempt` and compare the events.

## The runner's progress meter and watchdogs (BL-2398)

The internal `ManagedEasyTransfer.ToolProgress` (`EngineToolTransferProgress`: the runner's `TransferProgressRecorder` and its `-Z` `--fail-early` abort token) is where a perform from the options reports progress while it is set, in place of `EasyCallbackOptions.TransferInfo` and `Progress`; the abort token is linked into the transfer's, and the handle starts fresh `-Y`/`-y` and `-m` watchdogs from `EasyTransferOptions` on the running transfer (`ToolEvents`' state, else its own) through the engine's `TransferPerformer`, writing to `Host.DiagnosticLog`, on every perform, so a `--retry` attempt gets a fresh `-m` and a stalled one ends with the runner's exit 28 text (`ProgressStartingWatchdogsOf`). Without it no watchdog starts, as before. The xferinfo callback's four counts cannot carry the recorder's start, retry and done reports, so this is an internal hook (ADR-0567). `Duplicate` does not copy it and `Reset` clears it. Its tests are `ManagedEasyTransferTests.ToolProgress.cs`.

## The write, read and header callbacks' pause and abort (BL-2415)

Each `ManagedEasyTransfer` owns an `EasyPauseGate` (the handle's `CurlPause` bits; `Pause` sets them and `PausedDirections` reads them). A write or header callback that returns `WriteCallbackResult.Pause` pauses receiving, and `CallbackWriteStream.WriteAsync` waits under the transfer's token until `Pause` clears that bit, then hands the same chunk over again; a read callback's `ReadCallbackResult.Pause` pauses sending and `CallbackReadStream.ReadAsync` asks again the same way. A `file` URL gets no gate (libcurl's `PROTOPT_NONETWORK`), and the synchronous `Write` and `Read` cannot wait, so a pause there fails with libcurl's `Write callback asked for PAUSE when not supported` (`CURLE_WRITE_ERROR`) or `Read callback asked for PAUSE when not supported`. `ReadCallbackResult.Abort` marks the stream `Aborted`, and the perform answers `CurlCode.AbortedByCallback` with `operation aborted by callback`; `WriteCallbackResult.Error` is a short write (ADR-0573). Their tests are `EasyPauseGateTests`, `CallbackWriteStreamTests`, `CallbackReadStreamTests` and `ManagedEasyTransferTests.Callbacks.cs`.
