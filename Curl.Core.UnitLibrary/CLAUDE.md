# Curl.Core.UnitLibrary

Phase 1.

Transfer engine: URL parsing, scheme dispatch, redirects, resume, retries, rate limiting, low-speed aborts (`LowSpeedWatchdog`, ADR-0106), IPFS gateway rewriting.

Curl's own diagnostic log (`--log-level`, ADR-0222, BL-921): `TransferRetrier` writes component
`retry` through `RetryDiagnosticLog` and `RedirectFollower` writes `redirect` through
`RedirectDiagnosticLog` (target URLs without user information), both to the context's
`ITransferContext.DiagnosticLog`, and every followed hop carries that log on. `MaxTimeWatchdog`
and `LowSpeedWatchdog` (`runner`) and `Hsts\HstsTransferPolicy` (`hsts`, host names only) take
the log through their constructor; `Curl.Console`'s runner passes the run's log. `ProxySelector`
takes it per `TrySelect` call (`proxy`: the proxy chosen as `info`, by scheme, host and port only;
a no-proxy match naming its entry, or no proxy text, as `verbose`), and `AltSvc\AltSvcCache`
(`altsvc`) and `Hsts\HstsCache` (`hsts`: entries stored and expired) through their constructors
(ADR-0302, BL-1072).

Never construct a `Socket`, `SslStream` or `HttpClient` here. Take `IConnection`
so the tests in the matching `.UnitTests` project can drive this code from a
recorded byte stream with no network.

`FileSystem\PhysicalFileSystem` is the real-disk `IFileSystem` behind `file://`
(ADR-0002). Every failed open comes back as a `FileAccessStatus`; only cancellation
throws. Its disk tests in `Curl.Core.UnitTests` use temporary files and the null device
and run in the fast suite, so the coverage gate measures every line of it.
It is also the `IFileTimeSetter` `Curl.Console` stamps an `-o` file's last-write time
through under `-R`; a time it cannot set comes back as `false`, never as an exception.

`ProtocolDispatcher` hands a transfer to the `IProtocolHandler` registered for its URL's
scheme (case-insensitive) and returns exit 1, `Protocol "<scheme>" not supported`, when
none is, or `Protocol "<scheme>" is disabled` when `--proto` excludes it; either refusal is
also reported to the transfer's events, as curl 8.21.0's `-v` writes it (BL-805), as is the
`RedirectFollower`'s `(in redirect)` refusal. Two handlers claiming one scheme make its
constructor throw. It is not yet wired
into `Curl.Console`.

`ByteRangeParser` is the one place `-r`/`--range` text becomes the `ByteRange` a handler
receives on `ITransferContext.Range`, read as libcurl 8.21.0's `Curl_range` reads it; text
that names no range is `NotDeliveredFailure`, exit 33, except on an `http`/`https` URL, whose
handler sends `ITransferContext.RangeText` verbatim as curl does (ADR-0044, BL-386). Handlers
never parse range text.

`RedirectFollower` wraps `ProtocolDispatcher` for `-L`/`--location`: it follows a
successful 3xx hop's `TransferReport.RedirectUrl` under a `RedirectPolicy`
(`--max-redirs`, `--post301/302/303`, `--location-trusted`, allowed redirect schemes),
rewriting POST to GET and dropping credentials to another host, port or scheme as curl
8.21.0 does - except that each hop sends its own URL's user information unless `-u`
credentials go to it (ADR-0240, BL-814) - and returns the last hop's result with one merged report (redirect count,
effective URL, summed header/request/connection counts, timings from the first hop with
`RedirectDuration`). Without `-L` it returns the dispatcher's result unchanged. Given a
`HopProxySelector`, it chooses each hop's proxy again from that hop's own URL, as curl
8.21.0 does (BL-329); without one, every hop keeps the first URL's proxy. Given a
`HopAltSvcSelector`, it looks each hop's `--alt-svc` route and HTTP version up again from the
hop's URL (ADR-0226); without one, only a hop to the first URL's origin keeps its route. Under
`HttpRequestOptions.AutoReferer` (`-e "...;auto"`) each hop is sent the previous URL, without
user information or fragment, as its `Referer`, and the merged report's `Referer` is the last
one sent, which `%{referer}` prints (ADR-0101, BL-361). Each target that parses with a scheme
curl knows is reported as `Issue another request to this URL: '<target>'` before the HSTS
switch and the `--proto-redir` check, never after a `--max-redirs` refusal (BL-907). Right
after it comes curl's `http_switch_to_get` line when a redirect switches the method - a POST
on a 301 or 302 without `--post301`/`--post302`, every 303 but a POST under `--post303`:
`Switch to GET because of <code> response` under `--follow` for anything but a plain GET, or
`Stick to <method> instead of GET` under `-L` with `-X` (BL-1352).

`TransferRetrier` runs a transfer again under `--retry` (`RetryPolicy`: `--retry`,
`--retry-delay`) after curl 8.21.0's transient failures: exit 28, 6, 5 or 12
(`: timeout`), under `--retry-connrefused` an exit 7 whose `TransferResult.IsConnectionRefused`
is set (`: connection refused`; any other exit 7 only under `--retry-all-errors`), or an http(s) status 408, 429, 500, 502, 503, 504, 522 or 524 on a
success or a `-f` exit 22 (`: HTTP error`). It waits a `Retry-After`
(`RetryAfterHeader`, capped at six hours) when one asks for a wait, else the fixed
delay, else curl's backoff (1 s doubling to 10 min, advanced only when used), with
`Task.Delay` on the context's `TimeProvider`, and hands each retried attempt and its
`TransferRetryWarning` line (`Warning: Problem : HTTP error. Retrying in 1 second. 3
retries left.`) to the caller, which prints and wraps it and readies the output. It is
not yet wired into `Curl.Console` (BL-241).

`UrlSchemeGuesser` gives a URL typed without a scheme the one curl 8.21.0 guesses: the
scheme its host prefix implies (`ftp.`, `dict.`, `ldap.`, `imap.`, `smtp.`, `pop3.`, any
case), otherwise `http`. It only prepends `<scheme>://`; rejecting a malformed URL is left
to the URL parser. It is not yet wired into `Curl.Console`.

`ProxySelector` chooses the `ProxyEndpoint` curl 8.21.0 would use for a URL from `-x`,
`--noproxy` and the proxy environment variables (ADR-0024). It reads the environment only
through the `Func<string, string?>` it is constructed with, asking for curl's exact names;
production passes `Environment.GetEnvironmentVariable`, and tests pass a dictionary, never
the real environment. `NoProxyMatcher` is the `--noproxy`/`NO_PROXY` list, and
`ProxyUrlParser` turns proxy text into an endpoint or curl's exit 5 or 7 failure. Text with
no scheme is the kind of the option that gave it (`-x` is HTTP, `--socks5` is SOCKS5, the
environment is always HTTP) and `socks://` is SOCKS4 (BL-269). It is not yet wired into
`Curl.Console` (BL-238).

`Multipart\MultipartFormBodyBuilder` turns `MultipartFormPart`s into the `multipart/form-data`
`StreamBody` curl 8.21.0 sends for `-F`, byte for byte (ADR-0027): headers chosen as
libcurl's `Curl_mime_prepare_headers` chooses them, files opened through `IFileSystem` while
building so `Content-Length` is known, then streamed; an unopenable file is exit 26 before
anything is sent. The text encoding and the boundary source are injected. A part's
`Encoder` (`;encoder=`) goes through `MultipartPartEncoder`: every file part streams, and
`base64`, `quoted-printable` and `7bit` files are encoded as they are sent by
`EncodedReadStream`, never held whole; a seekable `7bit` file is read through once while
building so a byte above 127 still fails before sending, and an unseekable one fails the
read that reaches such a byte with a `RequestBodyReadFailedException`, which the sender turns
into exit 26. An unknown name is exit 43 (ADR-0041, ADR-0076, ADR-0093). An `@-` or `<-` part reads the standard-input `Stream` the
builder is given whole, never closing it, so the body keeps its `Content-Length`; without
one it opens the path `-` as before (BL-275). A file that cannot seek declares the length
`UnseekableFileLength` gives it (ADR-0097); one that can declares its length, except a device
under `/dev/` off Windows, which `SeekableFileLength` sends chunked as libcurl does
(ADR-0104). Names and file names are escaped as `MultipartNameEscaping` says: `%22`, `%0D`, `%0A` by default, `\\` and `\"` under `--form-escape` (BL-625). It is not yet wired into `Curl.Console`.

`Globbing\UrlGlob` is curl 8.21.0's URL globbing (ADR-0032): `TryParse` reads `{a,b}` sets
and `[1-10]`, `[01-10]`, `[a-z:2]` ranges as `tool_urlglob.c` does, failing with exit 3 and
curl's `<reason> in position N:` message, caret and all; `Unglobbed` is the URL under `-g`.
A glob may be named, `{<name>a,b}` or `[<name>1-3]` (at most 64 characters; a name used
twice is exit 3, `Duplicate glob name`; BL-1436).
`Expand()` yields each URL lazily, rightmost glob fastest, and `UrlGlobMatch.SubstituteGlobValues`
replaces `#N` and `#<name>` in an `-o` name; `UrlGlobMatch.TryResolveOutputFileName` also
fails a `#<name>` naming no glob as curl does (exit 43, `no glob exists with this name`). `UrlGlobMatch.ResolveOutputFileName` is the name curl
writes to: as written under `-g`, otherwise substituted and, when the caller passes
`OperatingSystem.IsWindows()`, sanitized by `WindowsOutputFileNameSanitizer` as curl's
Windows build does (control characters and `| < > " ? *` become `_`; BL-283). It is not
yet wired into `Curl.Console` (BL-240).

`IpfsGatewayRewriter` turns an `ipfs://<cid>/<path>` or `ipns://<name>/<path>` URL into the
gateway URL curl 8.21.0 fetches: gateway from `--ipfs-gateway`, else `IPFS_GATEWAY`, else
the first line of `$IPFS_PATH/gateway` or `$HOME/.ipfs/gateway` (never `USERPROFILE`). It
reads the environment and the file only through the two `Func<string, string?>` it is
constructed with. No gateway is `IpfsGatewayFailure.GatewayDetectionFailed` (exit 37,
`IPFS automatic gateway detection failed`); an unusable gateway or path is
`MalformedTargetUrl` (exit 3, `malformed target URL`); both are tool messages, printed as
`curl: <message>` with no `(<code>)`. It is not yet wired into `Curl.Console` (BL-240), and
`--ipfs-gateway` is not yet parsed (BL-353).

`AltSvc\AltSvcCache` is the `--alt-svc` cache (ADR-0175, BL-622): `ReadFile` takes curl
8.21.0's alt-svc file text (lines read strictly by `AltSvcFileLineParser`, expired entries
skipped), `ApplyHeader` learns from one `Alt-Svc` value as `AltSvcHeaderParser` reads it
(`clear`; `ma` and `persist` per alternative, 24 hours by default; the first known
alternative replaces the origin's entries), `Find` gives the first unexpired entry for an
origin and allowed versions, removing the expired ones it passes, `FindForOrigin` runs `Find`
under several origin versions in curl's order and says whether the entry names the origin
itself (`AltSvcMatch`, ADR-0226), and `FormatFile` writes
curl's file byte for byte with the line ending the caller passes (`Environment.NewLine`:
curl's Windows build writes CR LF). Time comes from the injected `TimeProvider`; it touches
no file. `Curl.Console`'s `AltSvcTransferCache` wraps it for each transfer (BL-623, BL-733).

`Hsts\HstsCache` is the `--hsts` cache (ADR-0179, BL-620): `ReadFile` takes curl 8.21.0's HSTS
file text (`[.]host "date"` lines read by `HstsFileLineParser`, dates by `CurlDateParser` or
`unlimited`, expired lines skipped, a repeated host merged), `ApplyHeader` learns from one
`Strict-Transport-Security` value as `HstsHeaderParser` reads it (nothing from an IP address;
`max-age=0` removes the host's own entry; an entry held is updated in place), `Find` gives the
host's entry or its longest `includeSubDomains` parent, removing expired entries it passes,
and `FormatFile` writes curl's file byte for byte with the caller's line ending, or `null`
when an expiry is past the platform's `gmtime` limit and curl would leave the file as it was.
Time comes from the injected `TimeProvider`; it touches no file. It is not yet wired into
`Curl.Console` (BL-621).
