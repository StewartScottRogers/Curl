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

`AltSvcTransferCache`, `HstsCacheFile` and `TlsSessionFileLines`, with their tests, stay in
`Curl.Console` because they use tool-side types (`DeferredOutputFileStream`,
`HttpVersionMapping`, `ToolNotes`). `CookieEngineTests` uses its own read-only file system fake,
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
