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

`MultipartFormPartMapping.FromMimeBody` builds the builder's parts from `EasyHttpOptions.MimePost`, which `Curl.Console` builds with `EasyOptionsFromCommandLine.HttpOf`: an `EasyMimeFile` or `EasyMimeStandardInput` part with a file name is an upload, one without is file content, `EasyMimeBytes` is UTF-8 text when it is valid UTF-8 and raw bytes otherwise, `EasyMimeCallback` is a reader part (BL-2470), and `SubParts` is a multipart. `NameEscapingOf` and `RequestContentTypeOf` still read `CommandLineOptions` (ADR-0531).

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
engine's caches, then `CreateProtocolHandlers` over its pooling connector. `CreateMulti`
gives a `ManagedMultiTransfer` (BL-2424, below), `CreateUrl` a `ManagedCurlUrlHandle` (BL-2436 and BL-2437, below), `CreateShare` a `ManagedShareHandle` (BL-2434, below), `CreateMime` a `ManagedMimeBuilder` (BL-2439, below)
and `CreateForm` a `ManagedFormBuilder` (BL-2440, below).

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
`Ok`. `WebSocket` came with BL-2443, `Headers` with BL-2446 and `Connection` with BL-2423 (below). Their tests are
`ManagedCurlEngineTests`, `ManagedEasyTransferTests` and `CallbackWriteStreamTests`.

## The tool's settings and the engine-wide state (BL-2359)

`ManagedEasyTransfer.TransferSettingsOf` maps the easy options libcurl has a `CURLOPT_*` for onto `EngineTransferSettings`: `EasyNetworkOptions.Protocols` and `RedirectProtocols` (`ProtocolSetOf`: comma-separated, trimmed, lower cased) as `--proto` and `--proto-redir`, `EasyTransferOptions.DirListOnly` as `-l` and `EasyProxyOptions.ProxyHeaders` as `--proxy-header`. The internal `ToolSettings` replaces them whole when set: it is for `Curl.Console` to set from `EasyOptionsFromCommandLine.TransferSettingsOf` once its runner performs through the engine (BL-2318), for the settings that are the tool's alone (`-s`, `-S`, `--fail-with-body`, the `-G` query and the rest); `Duplicate` copies it and `Reset` clears it. Under `EasyTransferOptions.Upload` the handler reads `EasyCallbackOptions.Read` through `CallbackReadStream` (0 ends the upload; below 0 or past the buffer is an `IOException` with libcurl's `read function returned funny value`), and with no read callback the upload is empty. Everything an engine's handles share is one `ManagedEngineSharedState`: the connection, TLS session and DNS caches, a `CookieStore` every handle's HTTP handler keeps cookies in when `CookieEngine.FromHttpOptions` turns the engine on, an `HstsTransferPolicy` on every context, and a `TransferPerformer` that gives each perform the next `TransferInfo.TransferId` and holds curl's easy error text, which `ManagedCurlEngine`'s internal constructor takes (empty from the public one). The internal constructor's dispatcher factory gets the options, the shared state and the cookie store (ADR-0542).

## The cookie, alt-svc and HSTS files and `%{conn_id}` (BL-2362)

`ManagedEasyTransfer.PerformAsync` does the runner's setup and cleanup around its attempt with `TransferPerformer`'s own steps, in the runner's order: `LoadCookieFilesAsync` (the `-b` files into the shared `CookieStore`), `OpenAltSvcCacheAsync` (the transfer's own `AltSvcTransferCache`, kept on its `EngineTransferState` and put on the context) and `SwitchToHttpsForHstsAsync` (the `--hsts` file read, an `http` URL the cache knows switched to `https`) before connecting, and `WriteCookieJarAltSvcAndHstsFilesAsync` after it, whatever the outcome. What those steps reach outside the engine comes from the internal `ManagedEasyTransfer.Host`, an `EasyTransferHost`: the `IFileSystem`, standard input for `-b -`, the `-c -` print target, the diagnostic log every context carries, the `--hsts` line endings and `CURL_ALTSVC_HTTP`. The default, `EasyTransferHost.Library`, is `PhysicalFileSystem` with nothing to read for `-`, `Stream.Null` for `-c -` and no log; `Curl.Console` sets its own once its runner performs through the engine (BL-2318). `Duplicate` copies it and `Reset` keeps it. Every transfer's events go through `EngineConnectionIdTransferEvents`, the engine's copy of the runner's `ConnectionIdRecordingTransferEvents`, so `TransferInfo.ConnectionId` is the engine performer's next connection number for a new connection and the opener's for a reused one (`ManagedEngineSharedState.ConnectionIdsByPoolNumber`), and -1 (`ManagedEasyTransfer.NoConnectionId`) after an unsupported scheme or malformed URL (ADR-0544). Their tests are `ManagedEasyTransferTests` (`ManagedEasyTransferTests.Files.cs`) and `EngineConnectionIdTransferEventsTests`.

## The debug and progress callbacks (BL-2360)

Under `EasyBehaviourOptions.Verbose` with `EasyCallbackOptions.Debug` set, `ManagedEasyTransfer` gives the context a `DebugCallbackTransferEvents`: an info line goes to the callback as `CurlInfoType.Text` with its line feed, a connection opened or reused and a TLS handshake, trust or message as the `Text` lines `-v` prints (`Curl.Output`'s `TransferEventInfoText`, the running platform's build), head bytes as `HeaderOut` and `HeaderIn`, body bytes as `DataOut` and `DataIn`, and TLS record bytes as `SslDataOut` and `SslDataIn` in the OpenSSL build only. Without `EasyBehaviourOptions.NoProgress`, `CallbackTransferProgress` calls `EasyCallbackOptions.TransferInfo`, else `Progress`, on every progress report with the download total and count and the upload total and count (an unknown total as 0); a return other than 0 or `ProgressCallbackResult.Continue` cancels the attempt and the perform returns `CurlCode.AbortedByCallback` with `Callback aborted`. `DebugCallback` keeps libcurl's signature (ADR-0543); for the `--trace-ids` prefix the callback reads the handle's `Info`, which holds the running transfer's `TransferId` from its start and its `ConnectionId` (-1 before) as soon as `EngineConnectionIdTransferEvents` takes one, before the event that opened or reused the connection reaches the callback (BL-2364, ADR-0547).

## The trace components (BL-2363)

The internal `ManagedEasyTransfer.TraceComponents` (`EngineTraceComponents.None` by default; `Duplicate` copies it, `Reset` keeps it) is libcurl's `curl_global_trace`, for `Curl.Console` to set (BL-2318). `WithTraceComponentEvents` wraps the debug events in the runner's decorators in the runner's order: `ResponseWaitTimerTraceEvents` under `timer` (the `CURLOPT_TIMEOUT` and `CURLOPT_CONNECTTIMEOUT` timers, written as with the multi not traced), `ClientReaderResetTraceEvents` under `read` and `ClientWriterTraceEvents` outermost under `write`. `MultiStateTraceEvents` and `AsyncResolveTeardownTraceEvents` stay in `Curl.Console` (ADR-0545).

## The curl tool's serial attempts (BL-2318, BL-2394)

`ManagedEasyTransfer` implements the internal `IToolEasyTransfer` (ADR-0622), which replaced BL-2318's `IToolAttemptTransfer` and its prepared `ToolAttempt`: `Curl.Console`'s runner sets `Options`, `ToolSettings` and `Host` once per transfer and, before each attempt's `PerformAsync`, `ToolInputs` (with `Url`, the URL it checked, which the handle transfers in place of a parse of `EasyNetworkOptions.Url`), `ToolOutputs`, `ToolEvents`, `ToolProgress` and `ToolDispatch`, then reads `LastResult`. With `ToolEvents` set the context carries the runner's events unwrapped (no `TlsVerifyResultRecordingTransferEvents`), and with `ToolProgress` set and an uncancellable perform token it carries the runner's abort token as it is, so the handler sees what the runner's own context gave.

## The runner's chosen pieces and the last result (BL-2395)

`ManagedEasyTransfer.LastResult` is the whole `TransferResult` of the last perform, from the options (`null` before one, after one that failed before connecting and after a progress-callback abort), for the runner's `--retry`, failure lines and `-w`. The internal `ToolInputs` (`EngineToolTransferInputs`) carries what the runner chose before connecting - proxy, looked-up credentials, SSH options, `If-None-Match` lines, `-r` range, `-C` offset, `-F` body, `-T` source and alt-svc cache - which a perform from the options passes to `TransferContextFactory.Create` as the runner does; while it is set the handle reads and writes no cookie, alt-svc or HSTS file, leaving them to the runner. `Duplicate` does not copy it and `Reset` clears it (ADR-0554). Its tests are `ManagedEasyTransferTests.ToolInputs.cs`.

## The runner's body and header outputs (BL-2396)

The internal `ManagedEasyTransfer.ToolOutputs` (`EngineToolTransferOutputs`: the body stream as the runner chains it through `--limit-rate`, `-N`, `--use-ascii` and its standard-output or file stream, the `-D` stream, the `-J` header watcher and the `--styled-output` styles) goes to `TransferContextFactory.Create` as the runner passes them, in place of `EasyCallbackOptions.Write` and `Header`; without it the handle builds the same record from those callbacks (`OwnOutputsOf`). `Duplicate` does not copy it and `Reset` clears it. A write or header callback that returns anything but the chunk's length fails with `OutputWriteFailedException` and `Failure writing output to destination, passed N returned M`, the runner's exit 23 text (ADR-0558). Its tests are `ManagedEasyTransferTests.ToolOutputs.cs`, which run each case through `ToolOutputs` and directly on the runner's own context and compare the writes.

## The runner's -v and --trace events (BL-2397)

The internal `ManagedEasyTransfer.ToolEvents` (`EngineToolTransferEvents`: the runner's whole events chain - its `-v` and `--trace` writer, `TraceIdsTransferEvents`, `MultiStateTraceEvents`, its `--trace-config` decorators and `ConnectionIdRecordingTransferEvents` - and the running transfer it keeps) is where a perform from the options reports every event while it is set, in place of `EasyCallbackOptions.Debug` and the `TraceComponents` decorators; the perform runs on its `State` and builds no `EngineConnectionIdTransferEvents`, so `%{conn_id}` is numbered once, by the runner's recorder, and `Info.ConnectionId` is what it kept (`StateAndEventsOf`). `TlsVerifyResultRecordingTransferEvents` no longer wraps it (BL-2394, ADR-0622). Under it `Info` answers the runner's own `-w` values (BL-2495, ADR-0654): `xfer_id` is the state's `RetryTransferId`, else its `TransferId`; `conn_id` is -1 after an unsupported scheme or malformed URL, else the retried attempt's when the attempt reused it (`KeepRetriedConnectionIdWhenReused`), else the state's, else `EngineToolTransferEvents.TakeConnectionId`'s, kept on the state; the effective URL falls back to the state's `RequestUrl`, the scheme is that URL's (`TransferInfoFromReport.RequestSchemeOf`), the `Referer` is the report's else `CURLOPT_REFERER`, and the verify results and early data are the state's (`EngineTransferState.SslVerifyResult`, `ProxySslVerifyResult`, `TlsEarlyDataSent`, which the runner's recorder sets). The debug callback keeps libcurl's signature and cannot carry the structured events, so this is an internal hook (ADR-0561). `Duplicate` does not copy it and `Reset` clears it. Its tests are `ManagedEasyTransferTests.ToolEvents.cs`, which run a transfer through `ToolEvents` and directly on the runner's own context and compare the events.

## The runner's progress meter and watchdogs (BL-2398)

The internal `ManagedEasyTransfer.ToolProgress` (`EngineToolTransferProgress`: the runner's `TransferProgressRecorder` and its `-Z` `--fail-early` abort token) is where a perform from the options reports progress while it is set, in place of `EasyCallbackOptions.TransferInfo` and `Progress`; the abort token is linked into the transfer's, and the handle starts fresh `-Y`/`-y` and `-m` watchdogs from `EasyTransferOptions` on the running transfer (`ToolEvents`' state, else its own) through the engine's `TransferPerformer`, writing to `Host.DiagnosticLog`, on every perform, so a `--retry` attempt gets a fresh `-m` and a stalled one ends with the runner's exit 28 text (`ProgressStartingWatchdogsOf`). Without it no watchdog starts, as before. The xferinfo callback's four counts cannot carry the recorder's start, retry and done reports, so this is an internal hook (ADR-0567). `Duplicate` does not copy it and `Reset` clears it. Its tests are `ManagedEasyTransferTests.ToolProgress.cs`.

## The write, read and header callbacks' pause and abort (BL-2415)

Each `ManagedEasyTransfer` owns an `EasyPauseGate` (the handle's `CurlPause` bits; `Pause` sets them and `PausedDirections` reads them). A write or header callback that returns `WriteCallbackResult.Pause` pauses receiving, and `CallbackWriteStream.WriteAsync` waits under the transfer's token until `Pause` clears that bit, then hands the same chunk over again; a read callback's `ReadCallbackResult.Pause` pauses sending and `CallbackReadStream.ReadAsync` asks again the same way. A `file` URL gets no gate (libcurl's `PROTOPT_NONETWORK`), and the synchronous `Write` and `Read` cannot wait, so a pause there fails with libcurl's `Write callback asked for PAUSE when not supported` (`CURLE_WRITE_ERROR`) or `Read callback asked for PAUSE when not supported`. `ReadCallbackResult.Abort` marks the stream `Aborted`, and the perform answers `CurlCode.AbortedByCallback` with `operation aborted by callback`; `WriteCallbackResult.Error` is a short write (ADR-0573). `EasyPauseGate` counts its `Set` calls (`Unpauses`); each stream reads the count before calling the callback and passes it to `Pause`, which ignores the pause when a `Set` ran since, so an unpause made while the callback was returning its pause is not lost (BL-2518). Their tests are `EasyPauseGateTests`, `CallbackWriteStreamTests`, `CallbackReadStreamTests` and `ManagedEasyTransferTests.Callbacks.cs`.

## The multi handle's event loop (BL-2424)

`ManagedCurlEngine.CreateMulti` gives a `ManagedMultiTransfer`, the engine's `IMultiTransfer`: each added transfer runs as its own `IEasyTransfer.PerformAsync`, started by the next `PerformAsync` of the multi handle, so the transfers run at once over the engine's shared caches. `PerformAsync` never waits: it starts what is new, queues one `CURLMSG_DONE` `CurlMultiMessage` with the transfer's `CurlCode` for each perform that has completed, in add order, and answers the transfers not yet done; `TryReadInfo` takes the messages oldest first. `ManagedEngineSharedState.MultiOwners` keeps each added transfer's multi handle by reference, so a transfer is in one of the engine's multi handles at a time (`AddedAlready`); `Remove` answers `Ok` for a transfer in none and `BadEasyHandle` for one in another, cancels a running perform and drops its unread message. `DisposeAsync` cancels and awaits every perform, and later calls answer `BadHandle`. Get_handles, waitfds, get_offt, the notifications and the network change came with BL-2427 (below), and the socket-action API with BL-2426 (ADR-0594). Its tests are `ManagedMultiTransferTests`, which drive fake transfers that finish when the test says.

## The multi handle's timeout, wait, poll and wakeup (BL-2425)

`ManagedMultiTransfer` takes the engine's `TimeProvider` and an `IWaitSocketPoller`. `GetTimeout` is 0 while a perform has work (a transfer added and not started, or one finished and not yet reported), else the milliseconds, rounded up, until the first running transfer's `EasyTransferOptions.Timeout` expires, measured from the multi perform that started it, else -1. `WaitAsync` ends when a running perform completes (one ready socket), an extra socket has an event (each one's `ReturnedEvents` set), or at the timeout capped by `GetTimeout`; with nothing to wait on it returns at once. `PollAsync` waits the whole timeout then, and `Wakeup`, sticky like libcurl's wakeup socket, ends it or the next one. A wait with extra sockets asks the poller again every `SocketPollInterval` (10 ms); the production `SocketWaitPoller` calls `Socket.Poll` with no wait on a `Socket` that does not own the descriptor. A negative timeout is `BadFunctionArgument` and a disposed handle `BadHandle` (ADR-0597). Its tests are `ManagedMultiTransferTests.Timers.cs`, on `TimerManualTimeProvider`, whose timers fire only when the test advances it, and `SocketWaitPollerTests`, on a loopback socket.

## The multi handle's socket action and callbacks (BL-2426)

`SocketActionAsync` and `PerformAsync` share one loop (`MoveTransfersOnAsync`): start what is new, queue each finished transfer's `CURLMSG_DONE`, then tell `MultiOptions.Socket` of each transfer that started (`CurlPoll.In` on a socket the handle numbers from `FirstTransferSocket`, `0x40000000`, one per running transfer, since the engine's connections are no descriptors an application could wait on) or finished (`CurlPoll.Remove` with the value `Assign` gave), and `MultiOptions.Timer` of the next timeout when it changed or after an action on `CurlMultiSockets.Timeout`. While a transfer runs the timer is at most `RunningTransferCheckInterval` (10 ms), sooner when `GetTimeout` is; it is what brings the application back to see a transfer finish. `Add` tells the timer 0. The socket and events an action names are not read. A callback's -1 answers `AbortedByCallback`, and from the next action every unfinished transfer ends with `CurlCode.AbortedByCallback`, one not started never starting. `Assign` answers `BadSocket` for a socket the callback was not told of or was told to remove, and `Remove` tells the callback `CurlPoll.Remove` for a transfer with a socket (ADR-0599). Its tests are `ManagedMultiTransferTests.SocketAction.cs`, which drive transfers purely through socket actions and pin the callback sequence.

## The URL handle's get (BL-2436)

`ManagedCurlEngine.CreateUrl` gives a `ManagedCurlUrlHandle`, the engine's `ICurlUrlHandle`, holding a `UrlHandleParts` (one nullable field per part, as upstream's `struct Curl_URL`). `Set` parses a whole URL with `CurlUrl` (`UrlHandleParts.Of`: the written port recovered from the text without leading zeros, the scheme guessed when the text does not start with it) under `CURLU_DEFAULT_SCHEME`, `GUESS_SCHEME` (else `BadScheme`), `PATH_AS_IS` and `DISALLOW_USER`, mapping each `CurlUrlRejection` to its `CurlUrlCode`. `Get` reads every part as curl 8.22.0's `curl_url_get`, with the `CURLUE_NO_*` code for a part not set, and every get flag; `UrlPartText` holds the decode, encode, escape and punycode transforms (ADR-0601). Its tests are `ManagedCurlUrlHandleTests`.

## The URL handle's set, dup and clear (BL-2437)

`Set` with a null value clears the part (`UrlPartSetter.Cleared`; the whole URL clears them all), and with a value sets any part as curl 8.22.0's `curl_url_set` (`UrlPartSetter.Set`, answering a `UrlSetResult`): the scheme, port and host checked with upstream's `CurlUrlCode`s, `CURLU_URLENCODE` escaping, `CURLU_APPENDQUERY` joining with one `&`, `CURLU_NON_SUPPORT_SCHEME` and `CURLU_NO_AUTHORITY`, and the handle unchanged on failure. A whole URL that is not absolute (`UrlRelativeReference.IsAbsolute`) is resolved against the URL held (`UrlRelativeReference.Resolve`, upstream's `redirect_url`); a scheme libcurl does not support (`UrlHandleParts.IsSupportedScheme`) is `UnsupportedScheme` without `CURLU_NON_SUPPORT_SCHEME`; under `CURLU_ALLOW_SPACE` spaces are parsed as `%20` and put back (`UrlHandleParts.WithSpaces`). `Duplicate` is a new handle over the same immutable parts (ADR-0602). Its tests are `ManagedCurlUrlHandleTests.Set.cs` and `UrlRelativeReferenceTests`.

## The header API (BL-2446)

`ManagedEasyTransfer.Headers` is a `TransferHeaderStore`, the engine's `ITransferHeaders`. A perform from the options of an `http`, `https`, `ws` or `wss` URL writes its header lines through `HeaderRecordingStream`, which records each complete line in the store and passes the write on to the header callback when one is set; other schemes, and the curl tool's `ToolOutputs`, record nothing. The store numbers requests from 0, each status line after a final head starting the next (so each redirect hop is its own request), and tags lines `Informational` under a 1xx status, `Connect` under a CONNECT reply (`ConnectReplyHeadWritingEvents` marks the output through `IConnectReplyHeadMarking`, which `HeaderLineTeeStream` passes to its `-D` side), `Trailer` after a final head and `Header` otherwise, with an `HTTP/2` or `HTTP/3` status line's `:status` as `Pseudo`. `TryGet` and `Next` are curl 8.22.0's `curl_easy_header` and `curl_easy_nextheader`, `Next` finding its place from the `CurlHeader` it answered before by reference. Every perform and `Reset` forget the headers (ADR-0603). Their tests are `TransferHeaderStoreTests`, `HeaderRecordingStreamTests` and `ManagedEasyTransferTests.Headers.cs`.

## The multi handle's handles, wait sockets, counts, notifications and network change (BL-2427)

`GetHandles` lists the transfers added and not removed, in add order. `GetWaitSockets` gives one `CurlWaitEvents.PollIn` socket per transfer whose perform has started and whose message is not queued, numbered as the socket callback is told of it (each `MultiEntry` gets its number once, from whichever asks first); an array too short is filled as far as it goes with the full count and `OutOfMemory`. `GetInfo` counts `CURLMINFO_*` as curl 8.22.0: current (added, not removed), running (message not queued and not pending), done (message queued), added (every add ever), and pending (not started, held back by a connection limit; BL-2428 below); `None` and unknown numbers are `UnknownOption`. `EnableNotification` and `DisableNotification` keep one bit per number 0 to 31 (`UnknownOption` outside); each perform or socket action queues `InfoRead` when it queues a message with none before it and `EasyDone` for each transfer it finishes, and sends them to `MultiOptions.Notify` after moving every transfer on, before the socket and timer callbacks. Setting `Options` with `NetworkChanged` is an action: `ClearConnections` closes the engine's idle connections (`ConnectionCache.CloseIdleAsync`, which `DisposeAsync` awaits) and `ClearDns` empties its `DnsCache` (`Clear`), through the constructor's two delegates; the value is kept with `NetworkChanged` set back to `None` (ADR-0604). Its tests are `ManagedMultiTransferTests.Counts.cs`.

## The multi handle's connection limits, multiplexing and server push (BL-2428)

Before a perform starts a transfer, `ManagedMultiTransfer.CanStart` counts the connections the running transfers hold: one each, except that under `CurlPipelining.Multiplex` transfers to one origin (scheme, host and port from `System.Uri`, else the URL text) whose URL is `https` and not held to HTTP/1, or that ask for HTTP/2 with prior knowledge, share connections `MultiOptions.MaxConcurrentStreams` at a time (below 1 or past `int.MaxValue` is `DefaultConcurrentStreams`, 100). One that cannot join a multiplexed connection with a stream free starts only within `MaxHostConnections` and `MaxTotalConnections` (0 is no limit); otherwise it is pending: not performed, no socket, no timeout, counted by `TransfersPending` and not `TransfersRunning`. Each perform moves the started transfers on first, so a freed slot goes to a pending one in add order in the same call. `OfferPush` is the engine's `push_promise`: it denies for a disposed handle, a parent not in it, no `MultiOptions.Push`, or a pushed transfer that is null or already added, else the callback decides: `Ok` adds the pushed transfer, `ErrorOut` ends the parent with `CurlCode.Http2` on the next perform, anything else denies. `MaxConnects` is applied each time a transfer's message is queued: the handle calls `ConnectionCache.CloseIdleBeyondAsync` (the constructor's `closeIdleConnectionsBeyond`) with it, or `DefaultConnectsPerTransfer` (4) for each transfer in the handle when it is 0 or less, which closes the oldest idle connections until no more than that many, idle and in use, remain; `DisposeAsync` awaits it (BL-2468, tests in `ManagedMultiTransferTests.MaxConnects.cs`). Nothing calls `OfferPush` until BL-2469 wires HTTP/2's `PUSH_PROMISE` to it (ADR-0605). Its tests are `ManagedMultiTransferTests.Limits.cs`.

## The share handle (BL-2434)

`ManagedCurlEngine.CreateShare` gives a `ManagedShareHandle`, the engine's `IShareHandle`, which owns its own `CookieStore`, `DnsCache`, `TlsSessionCache`, `ConnectionCache` and `HstsTransferPolicy`. A perform from the options whose `EasyBehaviourOptions.Share` names one is attached to it for the perform (`Attach` / `Detach`) and runs over a `ManagedEngineSharedState` holding the share's cache in place of the engine's for each kind shared (`ManagedEasyTransfer.PerformShared`), so handles of different engines send each other's cookies and reuse each other's connections; the numbering and the performer stay the engine's. `Share` and `Unshare` accept cookies, DNS, TLS sessions, connections, PSL and HSTS, else `BadOption`; every option answers `InUse` while a transfer is attached and `Invalid` after a successful `Cleanup`, which refuses with `InUse` while one is attached and starts closing the shared connections. The lock callbacks run as synchronous pairs, always `Single`: `Share` around attach, detach and cleanup, each shared kind as the transfer takes its cache, and `Cookie` around each call the HTTP handler makes of the shared cookies (`CookieStoreFor`) (ADR-0606). An HSTS entry one attached transfer learns switches another's `http` URL to `https` while HSTS is shared. The public suffix list is `Curl.Cookies`' one embedded snapshot, which every transfer of every engine checks against, so it is shared whatever the handle says; while PSL is shared, `Attach` takes it under the `Psl` lock right after the `Share` lock (BL-2435, ADR-0617). Its tests are `ManagedShareHandleTests`.

## The MIME API (BL-2439)

`ManagedCurlEngine.CreateMime` gives a `ManagedMimeBuilder`, the engine's `IMimeBuilder` (`curl_mime`), whose `AddPart` appends a `ManagedMimePartBuilder` (`curl_mimepart`) and whose `Build` gives the `EasyMimeBody`, parts in add order. The part's setters follow curl 8.22.0's `lib/mime.c`: `SetData` copies the bytes, `SetFileData` sets the file and its base name as the file name and answers `ReadError` (keeping both) when the path does not exist, `SetDataCallback` keeps the read and seek callbacks (a null read callback clears the data), `SetEncoder` accepts `binary`, `8bit`, `7bit`, `base64` and `quoted-printable` in any case and answers `BadFunctionArgument` otherwise, `SetHeaders` copies the lines, and every data setter replaces the one before. `SetSubParts` nests another `ManagedMimeBuilder`, built when the part is; it answers `BadFunctionArgument` for another engine's builder, a body another part holds, or the body the part is in or one enclosing it, and replacing or clearing it frees the body for another part. A perform from the options with `EasyHttpOptions.MimePost` and no `ToolInputs` builds the body through `MimePostForm` as the runner builds `-F` (`MultipartFormPartMapping`, `MultipartFormBodyBuilder` in UTF-8 over `Host.FileSystem`, `CURLMIMEOPT_FORMESCAPE` and mail URLs escaping with a backslash) before any setup, posting it as the form body, or uploading it as a MIME mail message to an `smtp`, `smtps`, `imap` or `imaps` URL; a build failure (a file that cannot be opened is `ReadError` with `MultipartFormBodyBuilder.OpenFailedMessage`) ends the perform before anything is sent. The internal `CreateFormBoundary` (curl's random boundary by default; `Duplicate` copies it) lets tests fix the boundary. `MultipartFormPartMapping` maps `curl_mime_data` bytes that are valid UTF-8 to text, which the UTF-8 builder sends as the same bytes and the tool's `-F` text in its platform encoding, and any other bytes to a `MultipartFormPartKind.Bytes` part sent verbatim; a `curl_mime_data_cb` part is a `MultipartFormPartKind.Reader` part over a `MimeCallbackReadStream`, read while the body is sent, its size giving the body a `Content-Length` and -1 sending it chunked (with `Expect: 100-continue`, as libcurl). The perform collects those streams as the body is built: a `CURL_READFUNC_ABORT` from any of them ends it with `CurlCode.AbortedByCallback` and `operation aborted by callback`, and one with a seek callback makes the body seekable, a resend rewinding it through `(0, SeekOrigin.Begin)` once it was read (BL-2470, ADR-0627). Its tests are `ManagedMimeBuilderTests`, `MimePostFormTests` and `ManagedEasyTransferTests.Mime.cs`, which compare the whole request with the one the tool sends for the same `-F` parts.

## The form API (BL-2440)

`ManagedCurlEngine.CreateForm` gives a `ManagedFormBuilder`, the engine's `ICurlFormBuilder` (`curl_formadd`, `curl_formget`; dropping it is `curl_formfree`). `Add` reads a call's `CURLFORM_*` options with `FormAddCall` as curl 8.22.0's `FormAdd` does: one `FormFieldInfo` node per field, a further `CURLFORM_FILE` or `CURLFORM_CONTENTTYPE` after a file adding a node for one more file, `OptionTwice`, `Null` (a missing value or one of the wrong .NET type), `IllegalArray` and `UnknownOption` as upstream gives them, and `End` ending the call or the array. `FormPost.Complete` then answers `Incomplete` for a field with no name or value or a file with a content length, `Null` for a NUL inside `CURLFORM_NAMELENGTH`, and types a file or buffer with none by its name's ending, else the node before's type, else `application/octet-stream`; a failed call adds nothing. `Build` gives the parts `Curl_getformdata` makes (`FormPost.ToMimePart`): several files are a part named by the field whose sub-parts are the files, `-` is standard input, `CURLFORM_FILECONTENT` sends no file name, and a `CURLFORM_STREAM` part's data is a `FormStreamData` holding its `CURLFORM_CONTENTSLENGTH` and stream object. A perform from the options with no `MimePost` sends `EasyHttpOptions.HttpPost` (`MimePostForm.BodyOf`; never to a mail URL) exactly as it sends `MimePost`, each `FormStreamData` read through `EasyCallbackOptions.Read` as an `EasyMimeCallback` of that length, 0 being -1 (unknown, sent chunked), and sent empty with no read callback; the stream object is not passed, as a managed read callback has no user pointer (BL-2482, ADR-0651). `Serialize` builds the body with `MultipartFormBodyBuilder` over the constructor's `IFileSystem` and boundary (`PhysicalFileSystem` and curl's random one from `CreateForm`) and hands the callback the `Content-Type: multipart/form-data; boundary=...` line, an empty line and the body in pieces of at most `PieceSize` (8192) bytes, blocking on the build as `curl_formget` is synchronous, a stream part empty; a short take or an unreadable file answers 26 (ADR-0618). Its tests are `ManagedFormBuilderTests`, `MimePostFormTests` and `ManagedEasyTransferTests.HttpPost.cs`.

## Connect-only, curl_easy_recv and curl_easy_send (BL-2423)

A perform from the options with `EasyNetworkOptions.ConnectOnly` set to `Connect` (`CURLOPT_CONNECT_ONLY` 1) parses the URL, then `PerformConnectOnlyAsync` connects to its host and port (the scheme's default when none is written), with TLS for the schemes `ConnectOnlyConnection.UsesTls` names, through the connector the internal `ManagedEasyTransfer.CreateConnectOnlyConnector` builds (`ManagedCurlEngine.CreateConnectOnlyConnector`: the transports' `TcpConnector`, not the pool; tests put a fake there, `Duplicate` copies it), sends nothing and keeps the connection in the handle's `ConnectOnlyConnection`, which `Connection` answers. A failed connect answers the connector's code and text. Every later perform and `DisposeAsync` close it; `Reset` keeps it. `ReceiveAsync` and `SendAsync` never wait: a receive starts one read and answers `Again` while it runs, then hands its bytes over, keeping what does not fit; a send hands the bytes to one write-and-flush, answers them all sent, and answers `Again` while that write runs; an `IOException` is `RecvError` or `SendError`; with no connection, or options no longer connect-only, both answer `UnsupportedProtocol`. The proxy `TransferProxySelection.TrySelect` chooses from `EasyProxyOptions` (no environment variable read; a no-proxy host none) goes on the `ConnectTarget`'s `Proxy`, so the connector tunnels through an HTTP or HTTPS proxy or a SOCKS one first, and unusable proxy text ends the perform with the selector's code and text (BL-2472); `ConnectOnly.WebSocket` came with BL-2443 (below) (ADR-0608). Their tests are `ConnectOnlyConnectionTests` (over `ControlledConnection`, whose reads and writes complete when the test says) and `ManagedEasyTransferTests.ConnectOnly.cs`.

## The seek callback (BL-2417)

Under `EasyTransferOptions.Upload` with `EasyCallbackOptions.Seek` set, the upload is a `SeekingCallbackReadStream`, the `CallbackReadStream` that implements `Curl.Protocol.Abstractions`' `IUploadRewinder`: once the read callback has given bytes (`BytesReadSinceRewind`), a followed redirect or an authentication retry that resends the body calls the seek callback with `(0, SeekOrigin.Begin)`, and any result but `SeekResult.Ok` fails the perform with `CurlCode.SendFailRewind` and `seek callback returned error <n>`, as curl 8.22.0's `cr_in_rewind` does. Without a seek callback the upload is a plain `CallbackReadStream`, as before (ADR-0609). Their tests are `SeekingCallbackReadStreamTests` and `ManagedEasyTransferTests.Callbacks.cs`.

A perform from the options with no `ToolInputs` that uploads through the read callback with a positive `EasyTransferOptions.ResumeFrom` first skips that many bytes with `CallbackUploadResume`, as curl 8.22.0's `cr_in_resume_from`: the seek callback with `(offset, SeekOrigin.Begin)`; on `SeekResult.CantSeek`, or with no seek callback, the bytes read through the read callback in 4 KiB pieces and discarded (not counted in `BytesReadSinceRewind`), a read of 0 or past what it asked for ending the perform with `CurlCode.ReadError` and `Could only read N bytes from the input`; any other seek result `CurlCode.ReadError` and `Could not seek stream`; and with `InFileSize` known and the offset leaving none of it, `CurlCode.PartialFile` and `File already completely uploaded`. The failure ends the perform before anything is set up or sent; the handlers still get the offset for `Content-Range` or `APPE` (BL-2473, ADR-0641). Its tests are `CallbackUploadResumeTests` and `ManagedEasyTransferTests.Callbacks.cs`.

## The socket callbacks (BL-2418)

When a handle sets `EasyCallbackOptions.OpenSocket`, `SocketOption` or `CloseSocket`, `ManagedCurlEngine.PoolOf` puts a `SocketCallbackConnector` beneath the pool (`PoolingConnector.Over`, for the `TcpConnector` and FTP's data connector alike) and `CreateConnectOnlyConnector` wraps its `TcpConnector`; with none set nothing changes. Once the inner connector has connected, the open-socket callback is asked for a socket with `CurlSocketType.IpConnection` and the peer's address (`SocketCallbackConnector.AddressOf`), `CurlSocket.Bad` closing the connection and failing with exit 7; without it the socket is the engine's next `CurlSocketHandles` number (`ManagedEngineSharedState.SocketHandles`). The socket-option callback comes next, `SocketOptionResult.Error` closing the connection, calling close-socket and failing with exit 42, both as `Failed to connect to <host>:<port> after <n> ms: <text>`. The connection is a `SocketCallbackConnection`, which forwards every member and calls close-socket once (`CurlSocketCloser`) when it is disposed. QUIC passes through (ADR-0610). Their tests are `SocketCallbackConnectorTests` and `ManagedEasyTransferTests.Sockets.cs`.

## The prerequest and resolver-start callbacks (BL-2419)

When a handle sets `EasyCallbackOptions.Prerequest`, `ManagedCurlEngine.CreateDispatcher` puts a `PrerequestConnector` over the pool the TCP handlers connect through (not FTP's data connector) and `CreateConnectOnlyConnector` over its connector: after each connect that gives a connection, new or reused (once per request, a followed redirect again), it calls the callback with the connection's peer and local IP address and port (an empty address and 0 for an end point that is not an IP one), and `PrerequestResult.Abort` closes the connection and fails with exit 42 and `operation aborted by pre-request callback`. A QUIC session from `ConnectMultiplexedSessionAsync` is asked too. When it sets `ResolverStart`, `CreateTransports` passes `EngineComposition.CreateTransports` a `wrapLookUps` that puts a `ResolverStartDnsResolver` under the TCP and UDP connectors, so the callback runs (with 0 for the resolver state) before each look-up the DNS cache and `--resolve` entries do not answer; a non-zero answer resolves nothing, failing with exit 6 `Could not resolve host: <host>` and caching nothing (ADR-0611). Their tests are `PrerequestConnectorTests`, `ResolverStartDnsResolverTests` and `ManagedEasyTransferTests.Phases.cs`.

## The trailer callback (BL-2475)

A read-callback upload carries `EasyCallbackOptions.Trailer` as `CallbackReadStream.Trailer`, and `CallbackReadStream` implements `Curl.Protocol.Abstractions`' `IUploadTrailerSource` over it: with no trailer callback `SuppliesTrailers` is false and the HTTP handler sends the plain `0\r\n\r\n`; with one, the handler calls it once the read callback has ended a chunked HTTP/1.1 upload and sends the `Name: value` lines it added in the closing chunk, skipping a line whose first colon is not followed by a space, and `TrailerResult.Abort` fails the perform with `CurlCode.AbortedByCallback` and `operation aborted by trailing headers callback`, as curl 8.22.0's `add_last_chunk` in `lib/http_chunks.c` (ADR-0646). Its tests are `CallbackReadStreamTests` and `ManagedEasyTransferTests.Callbacks.cs`.

## The runner's follower and context factory (BL-2479)

The internal `ManagedEasyTransfer.ToolDispatch` (`EngineToolTransferDispatch`: the runner's `RedirectFollower` over its option group's `TransferDispatch`, and its own `TransferContextFactory` with standard input for a `telnet` upload, the platform's credential encoding, the run's diagnostic log and HSTS store) makes a perform from the options run through that follower and build its context with that factory, in place of the dispatcher `createDispatcher` builds and a factory with no standard input (`FollowerOf`, `CreateContext`). `Duplicate` does not copy it and `Reset` clears it. `LastContext` is the context the last perform from the options built (`null` before one and after one that failed before building it), for the runner's `TransferRetrier` and `%{conn_id}` reuse, which read its `Url` (ADR-0614). Its tests are `ManagedEasyTransferTests.ToolDispatch.cs`.

## The WebSocket API (BL-2443)

A perform from the options with `EasyNetworkOptions.ConnectOnly` set to `WebSocket` (`CURLOPT_CONNECT_ONLY` 2) of a `ws` or `wss` URL runs the normal perform path over a dispatcher of one `WebSocketUpgradeOnlyHandler` (`FollowerOf`, `WebSocketUpgradeDispatcherOf`): a `WsProtocolHandler` over `CreateConnectOnlyConnector`'s connector, authenticating with `ManagedCurlEngine.CreateWebSocketAuthenticator`, whose `ConnectAndUpgradeOnlyAsync` hands the open connection and the bytes after the `101` head to the handle's `ManagedEasyWebSocket`, which `WebSocket` answers; a refused upgrade answers the transfer's code and keeps nothing. For any other scheme `WebSocket` only connects, as `Connect` (`IsRawConnectOnly`). `ReceiveAsync` never waits: one read at a time, `Again` while it runs or while only part of a frame has come, then the first run `WsFrameMetadataDecoder` gave, as much as the buffer holds, with `WebSocketFrameFlags.Of`'s bits (a continuation reports its message's type, `Continuation` while FIN is clear), the offset and the bytes left; `GotNothing` when the server closes, `RecvError` for a failed read or a protocol violation, from then on. Each completed ping is answered with a pong unless `EasyWebSocketOptions.Options` has `NoAutoPong` or `RawMode`. Under `RawMode` (`CURLWS_RAW_MODE`) `SendAsync` writes the bytes unframed and refuses any bit or fragment size with `BadFunctionArgument`, as `lib/ws.c` does, while `ReceiveAsync` still decodes frames, as `curl_ws_recv` does (BL-2444); a perform that is not connect-only gives `WsProtocolHandler.RawMode` the same bit (`ManagedCurlEngine.IsWebSocketRawMode`, `EngineComposition.CreateProtocolHandlers`' `webSocketRawMode`), so the write callback gets the received stream unframed (BL-2494). `SendAsync` writes one masked frame, the opcode from `WebSocketFrameFlags.TryFrameOf` (`BadFunctionArgument` for bits libcurl refuses), `SendError` when the write fails. `CurrentFrame` is the frame last handed over, `null` before any. With options no longer connect-only both answer `UnsupportedProtocol`, and with no upgraded connection `BadFunctionArgument`. `CURLWS_OFFSET` and `StartFrameAsync` answer `BadFunctionArgument` until BL-2481. The internal `WebSocketRandomSource` (`SystemWebSocketRandomSource` by default; `Duplicate` copies it) gives the masks and the key. Every perform and `DisposeAsync` close the connection (ADR-0616). Their tests are `ManagedEasyWebSocketTests` and `WebSocketFrameFlagsTests` (over `QueuedConnection`, whose reads take queued chunks) and `ManagedEasyTransferTests.WebSocket.cs`.

## The chunk, fnmatch and interleave callbacks (BL-2420)

`ManagedCurlEngine.CreateDispatcher` passes `EngineComposition.CreateProtocolHandlers` the FTP handler's wildcard hooks and the RTSP handler's interleave sink, built from the transfer's options. `CallbackWildcardHooks.Of` is `null` without `EasyBehaviourOptions.WildcardMatch` (a wildcard URL is then fetched as a plain path), else hooks over `EasyCallbackOptions.FileNameMatch`, `ChunkBegin` and `ChunkEnd`, each one not set answering as `FtpDownloadEveryMatch.Instance`; the chunk-begin callback gets each match as a `CurlFileInfo` (`FileInfoOf`: the fields `ftplistparser.c` fills, `Time`, `UserId` and `GroupId` `null`) with the count of matches left, and any answer but `Skip` or `Fail` downloads it. `CallbackInterleaveSink.Of` is `null` without `EasyCallbackOptions.Interleave`, else a sink handing each whole `$` frame to it, a count short of the frame failing with 23 (ADR-0619). Their tests are `CallbackWildcardHooksTests`, `CallbackInterleaveSinkTests` and `ManagedEasyTransferTests.Wildcard.cs`, over `PerConnectionScriptedConnector`, which gives each connection its own script.

## The HSTS read and write callbacks (BL-2421)

While `EasyHttpOptions.HstsControl` has `Enable` or `Hsts` is set (`CallbackHstsCache.IsEnabled`), a perform from the options asks `EasyCallbackOptions.HstsRead` for entries after its URL parses and before anything else (`CallbackHstsCache.Load`, curl 8.22.0's `hsts_pull`), on the cache the perform uses (the engine's, or the share handle's when attached): each `HstsResult.Ok` entry is added, `Done` stops, `Fail` ends the perform with `CurlCode.AbortedByCallback` and an `Ok` with no entry or an empty name with `BadFunctionArgument`, both with no error text. `DisposeAsync` hands `HstsWrite` every entry of the cache the last perform used (the engine's before any), with its index and the total, stopping at the first answer that is not `Ok` (`CallbackHstsCache.Save`, `hsts_push`). Both go through `HstsTransferPolicy`'s file text, so an entry the file reader would skip is skipped from the read callback too (ADR-0620). Their tests are `CallbackHstsCacheTests` and `ManagedEasyTransferTests.Hsts.cs`.

## The SSH key callbacks (BL-2422)

A perform from the options of an `scp` or `sftp` URL with no `ToolInputs` gives the handler `SshOptionsMapping.OfHandle`'s `SshOptions`: the `EasySshOptions` keys, fingerprints and compression, `CURLOPT_KEYPASSWD`, the known-hosts file only when `CURLOPT_SSH_KNOWNHOSTS` names one (libcurl looks for no default), and the handle's host-key callbacks through `CallbackSshHostKeys`: `EasyCallbackOptions.SshHostKey` as `HostKeyAcceptance` (true only for `SshKeyMatch.Ok`) and `SshKey` as `KnownHostDecision`, the match and the answer passed by number, the key type name mapped to `SshKeyType` as `convert_ssh2_keytype` does and the file's key given as its base64 text. `Curl.Protocol.Ssh`'s `SshHostKeyChecker` asks them where curl 8.22.0's `libssh2.c` does; the known-hosts file is not written for `FINE_ADD_TO_FILE` or `FINE_REPLACE` until BL-2489 (ADR-0623). Their tests are `CallbackSshHostKeysTests` and `ManagedEasyTransferTests.SshKeys.cs`, over `Curl.Protocol.Ssh.UnitTests`' `InMemorySshServer`.

## The version information (BL-2465)

`ManagedCurlEngine.VersionInfo` is `ManagedCurlVersion.For(OperatingSystem.IsWindows(), OperatingSystem.IsMacOS())`: the build `curl -V` (`Curl.Cli`'s `CurlVersionText`) describes on the platform - the host triple and TLS backend of `-V`'s first line (`x86_64-w64-mingw32` and `Schannel` on Windows, `aarch64-apple-darwin25.0.0` and `SecureTransport` on macOS, `x86_64-pc-linux-gnu` and `OpenSSL` elsewhere), `-V`'s protocols without the tool's own `ipfs` and `ipns` (no `smb` or `smbs`, ADR-0453), and its features (the Windows line on Windows). The values are pinned here because this library never references `Curl.Cli.UnitLibrary`; `ManagedCurlVersionTests` checks every platform's against `CurlVersionText.Lines`, so a change to `-V` that misses this file fails there. Libcurl's `curl_version` and `curl_version_info` read it (ADR-0509).
