# Curl.Core.UnitLibrary

Phase 1.

Transfer engine: URL parsing, scheme dispatch, redirects, resume, retries, rate limiting, IPFS gateway rewriting.

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
none is; two handlers claiming one scheme make its constructor throw. It is not yet wired
into `Curl.Console`.

`ByteRangeParser` is the one place `-r`/`--range` text becomes the `ByteRange` a handler
receives on `ITransferContext.Range`, read as libcurl 8.21.0's `Curl_range` reads it; text
that names no range is `NotDeliveredFailure`, exit 33. Handlers never parse range text.

`RedirectFollower` wraps `ProtocolDispatcher` for `-L`/`--location`: it follows a
successful 3xx hop's `TransferReport.RedirectUrl` under a `RedirectPolicy`
(`--max-redirs`, `--post301/302/303`, `--location-trusted`, allowed redirect schemes),
rewriting POST to GET and dropping credentials to another host, port or scheme as curl
8.21.0 does, and returns the last hop's result with one merged report (redirect count,
effective URL, summed header/request/connection counts, timings from the first hop with
`RedirectDuration`). Without `-L` it returns the dispatcher's result unchanged. It is not
yet wired into `Curl.Console`.

`UrlSchemeGuesser` gives a URL typed without a scheme the one curl 8.21.0 guesses: the
scheme its host prefix implies (`ftp.`, `dict.`, `ldap.`, `imap.`, `smtp.`, `pop3.`, any
case), otherwise `http`. It only prepends `<scheme>://`; rejecting a malformed URL is left
to the URL parser. It is not yet wired into `Curl.Console`.

`ProxySelector` chooses the `ProxyEndpoint` curl 8.21.0 would use for a URL from `-x`,
`--noproxy` and the proxy environment variables (ADR-0024). It reads the environment only
through the `Func<string, string?>` it is constructed with, asking for curl's exact names;
production passes `Environment.GetEnvironmentVariable`, and tests pass a dictionary, never
the real environment. `NoProxyMatcher` is the `--noproxy`/`NO_PROXY` list, and
`ProxyUrlParser` turns proxy text into an endpoint or curl's exit 5 or 7 failure. It is not
yet wired into `Curl.Console` (BL-238).

`Multipart\MultipartFormBodyBuilder` turns `MultipartFormPart`s into the `multipart/form-data`
`StreamBody` curl 8.21.0 sends for `-F`, byte for byte (ADR-0027): headers chosen as
libcurl's `Curl_mime_prepare_headers` chooses them, files opened through `IFileSystem` while
building so `Content-Length` is known, then streamed; an unopenable file is exit 26 before
anything is sent. The text encoding and the boundary source are injected. It is not yet
wired into `Curl.Console`.

`Globbing\UrlGlob` is curl 8.21.0's URL globbing (ADR-0032): `TryParse` reads `{a,b}` sets
and `[1-10]`, `[01-10]`, `[a-z:2]` ranges as `tool_urlglob.c` does, failing with exit 3 and
curl's `<reason> in position N:` message, caret and all; `Unglobbed` is the URL under `-g`.
`Expand()` yields each URL lazily, rightmost glob fastest, and `UrlGlobMatch.SubstituteGlobValues`
replaces `#N` in an `-o` name. Windows name sanitizing is not done here (BL-283). It is not
yet wired into `Curl.Console` (BL-240).
