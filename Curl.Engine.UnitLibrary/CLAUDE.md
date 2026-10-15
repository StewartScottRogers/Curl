# Curl.Engine.UnitLibrary

The managed transfer engine (ADR-0506): the code that runs a transfer, moving out of
`Curl.Console` so that `Curl.Console`, `Curl.Api` and `Libcurl` can all drive it. Namespace
`Curl.Engine`. Transfer code is moving in task by task (BL-2285 onwards).

It references `Curl.Cli.UnitLibrary` only temporarily; BL-2310 drops that reference.

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

Moved here, in `Curl.Engine`, still reading `CommandLineOptions` (BL-2303 to BL-2309 retarget them
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

`Curl.Engine.UnitLibrary` references `Curl.Api.UnitLibrary` for its option records. `EngineComposition`'s transport, TCP connector, pooling connector and DNS resolver factories take `EasyNetworkOptions` beside `CommandLineOptions` (still read for TLS, proxy, trace, HTTP version and `-m`), and `CreateUdpDatagramConnector`, `LocalBindingOf`, `UnixSocketOf`, `AddressFamilyOf`, `ConnectTimeoutOf`, `DnsServerVariableResolverOf`, `TcpSocketOptionsOf`, `TransferTimers.Of` and `OptionGroupConnectionSettings.Of` read their network settings from it alone (ADR-0526). `Curl.Console` builds the record with `EasyOptionsFromCommandLine.NetworkOf`, which `Curl.Engine.UnitTests` and `Curl.Engine.IntegrationTests` link. `TransferContextFactory` still reads the connect timeout and `--connect-to` from `CommandLineOptions` until BL-2343.

## The TLS option group (BL-2324)

`TlsClientOptionsMapping` (`Of`, `ProxyOf`, `DohOf`), `TlsBuildInformation`, `CreateTransports`, `CreateDnsResolver`, `CreateDohConnector`, `CreatePoolingConnector` and `OptionGroupConnectionSettings.Of` read TLS settings from `EasyTlsOptions`, which `Curl.Console` builds with `EasyOptionsFromCommandLine.TlsOf`; `--no-alpn` and `--ssl-sessions` still come from `CommandLineOptions` (ADR-0530).

## The form body (BL-2344)

`MultipartFormPartMapping.FromMimeBody` builds the builder's parts from `EasyHttpOptions.MimePost`, which `Curl.Console` builds with `EasyOptionsFromCommandLine.HttpOf`: an `EasyMimeFile` or `EasyMimeStandardInput` part with a file name is an upload, one without is file content, `EasyMimeBytes` is UTF-8 text, and `SubParts` is a multipart. `NameEscapingOf` and `RequestContentTypeOf` still read `CommandLineOptions` (ADR-0531).

## The HTTP request, version and redirect mappings (BL-2345)

`HttpRequestOptionsMapping.Of`, `RedirectPolicyMapping.Of` and `TransferContextFactory.Create` take `EasyHttpOptions` for the headers (with `--json`'s), `-A`, `-e`, `--request-target`, the `-d` body (`PostFields`, `null` under `-G`), the encodings (`Compressed` is a set `AcceptEncoding`, `Raw` both decodings off), `-L`/`--follow`, `--max-redirs`, `--post30x`, the HTTP version, `--http0.9`, `--ignore-content-length` and `--expect100-timeout`; authentication, proxy, `--unix-socket`, `--happy-eyeballs-timeout-ms`, `--location-trusted` and the `--proto` options still come from `CommandLineOptions`. `HttpVersionMapping` maps `Curl.Api`'s `HttpVersion`; its `FromRequested` bridges `CommandLineOptions.HttpVersion` for `EngineComposition` and `OptionGroupConnectionSettings` until BL-2350 (ADR-0532).

## The cookie, alt-svc and HSTS settings (BL-2346)

`CookieEngine.FromHttpOptions`, `AltSvcTransferCache.OpenAsync`, `HstsCacheFile.ReadAsync` and `WriteAsync`, and `TransferPerformer`'s `SwitchToHttpsForHstsAsync`, `OpenAltSvcCacheAsync` and `WriteCookieJarAltSvcAndHstsFilesAsync` take `EasyHttpOptions` for `-b` (`Cookie`, the strings joined as curl's `cookie_setopts` joins them, and `CookieFiles`), `-c`, `-j`, `-H` (an `-H` naming `Cookie` drops the strings), `--alt-svc`, the HTTP version the alt-svc lookup keys on, and `--hsts`; the `-G` / `--url-query` query, `--path-as-is` and `--connect-to` still come from `CommandLineOptions` (ADR-0533).

## The behaviour and transfer settings (BL-2347)

`TransferContextFactory.Create` takes `EasyBehaviourOptions` (`-i`) and `EasyTransferOptions` beside `EasyHttpOptions` for the `-r` text, `-I`, `-R` (`FileTime`), `-z` (`TimeValue` in whole seconds, so a date past 9999 keeps its seconds), `-B`, `--crlf`, `--max-filesize` and `-m` (zero is none, as libcurl's), and a `-T` upload under `-C -` (`Upload` with `ResumeFrom` -1, which the runner sets per transfer); `HttpRequestOptionsMapping.Of` reads `-X` and `-f` from them (`--fail-with-body` still from `CommandLineOptions`), `MailRequestOptionsMapping.FromCommandLine` reads `-X`, and `TransferPerformer.StartLowSpeedWatchdog`, `StartMaxTimeWatchdog` and `TransferTimers.Of` read `-Y`/`-y` and `-m` (ADR-0534).

## The protocol settings (BL-2348)

`TransferContextFactory.Create` takes `EasyProtocolOptions` for FTP's `-P`, `-Q` (`Quote`, `PreQuote` behind `+` and `PostQuote` behind `-`, one list the handlers sort by prefix), `-a`, `--disable-epsv`/`--disable-eprt`, `--ftp-pret`, `--ftp-create-dirs`, `--ftp-alternative-to-user`, `--ftp-skip-pasv-ip`, `--ftp-ssl-ccc`, `--ftp-account` and `--ftp-method`, and for `--tftp-blksize` (512 is `null`, not given), `--tftp-no-options` and `-t`; `MailRequestOptionsMapping.FromCommandLine` reads the sender, recipients, `AUTH` and allow-fails from `EasySmtpOptions`; `TransferPerformer.ResolveSshOptionsAsync` and `SshOptionsMapping.Of` read the SSH keys, fingerprints, compression and `--knownhosts` from `EasySshOptions` (`--pass`, `-k`, `-s` and `-S` still from `CommandLineOptions`). `RoutingFtpProtocolHandler`, `CreateFtpProtocolHandler` and `SshKnownHostsFileSearch` read no option (ADR-0535).

## The proxy settings (BL-2325)

`TransferProxySelection.TrySelect`, `EngineComposition.PreProxyOf`, `HaproxyProtocolOf`, `CreateProxyTunnelOptions`, `Socks5AuthenticationMapping.Of` (was `FromCommandLine`), `TransferPerformer.TrySelectProxyAndCredentials` and `TransferContextFactory.Create` (`-p`, `--suppress-connect-headers`) read the proxy, pre-proxy, proxy type, no-proxy list, proxy headers, SOCKS5 and HAProxy settings from `EasyProxyOptions`, which `Curl.Console` builds with `EasyOptionsFromCommandLine.ProxyOf`; `CreateTransports`, `CreateTcpConnector`, `CreatePoolingConnector` and `OptionGroupConnectionSettings.Of` take it beside the other groups. `ProxyType` is the kind of proxy text with no scheme, and `Https2` stays HTTP and asks the tunnel for HTTP/2. `-U`, `--proxy-service-name` and `--delegation` still come from `CommandLineOptions`, `--connect-to`'s tunnelling until BL-2343 (ADR-0536). Their record-driven tests are `EasyProxyOptionsReadingTests`.
