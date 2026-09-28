# Curl.Console

Phase 1. The entry point, published native ahead-of-time as a single file so it
drops onto PATH as `curl.exe`.

This is the only project without a `.UnitLibrary` suffix, because it is an
executable rather than a library.

It is also the only project that references every library: composing the services
is its job. `CurlComposition` is the composition root, written as plain constructor
calls - no container, no reflection, no assembly scanning, so native AOT sees every
type. `Curl.Core.UnitLibrary` dispatches through the `IProtocolHandler` instances it is
given and must never reference a protocol library directly.

`CurlComposition.CreateTransports` wraps the run's `TcpConnector` in one `PoolingConnector`,
which every TCP handler connects through, so a later URL to the same pool key reuses an
earlier URL's connection; `TransferDispatch` holds it as the run's `ConnectionPool`, and the
runner disposes the dispatch, closing the pool without writing anything, once the transfers
end, whatever their outcome (ADR-0050, BL-334). It also holds the `TcpConnector`'s
`LoadResolveEntries`, which the runner calls at the start of every URL's transfer, just before
the `-b` files load, so `-v` prints the `--resolve` entries' `Added ... to DNS cache` lines for
each URL; a `--retry` attempt and a followed redirect reload nothing, as in curl 8.21.0 (BL-486).

`CurlComposition.CreateProtocolHandlers` gives every handler an `EndPointRecordingConnector` and
an `EndPointRecordingDatagramConnector` sharing one `ConnectionEndPointRecorder`, and wraps
each handler in an `EndPointReportingProtocolHandler`, which puts the end points of the
transfer's first connection (FTP's control connection) on its report when the handler reported
neither, so `%{local_ip}`, `%{local_port}`, `%{remote_ip}` and `%{remote_port}` work for every
scheme without handler code; HTTP keeps its own (ADR-0119, BL-515).

`Program.Main` only opens the standard streams, builds the composition and hands the
arguments to `CurlCommandRunner`, which parses them, runs each URL and prints curl's
`curl: (N) <message>` lines, each transfer's message cut to curl's 255-byte error buffer by
`CurlErrorBuffer` (a bad glob's lines are not cut; ADR-0072, BL-380). The parse reads the default config file first, where
`DefaultConfigFileSearch.ForProcess` finds it (the composition passes it; a runner given none
reads no `.curlrc`, which keeps tests off the real home directory), unless the first argument
starts with `-q` or is `--disable`; `-K` files apply where they stand. Both are read through
the injected `IDataFileReader`. Under `-v` or a `--trace` option an accepted command line then
prints `Note: Read config file from '<path>'`, wrapped as a warning is and shown even with
`-s` (BL-243; the refused-command-line cases are BL-352). `-o` files open on the first write through
`DeferredOutputFileStream`, which is how curl's exit 23 message comes out right. The
parser's warning lines are written to standard error before anything else.
Every `Warning: ` line is wrapped by `WarningLineWrapper` as curl's `warnf` wraps it, at the
width `TerminalColumns` resolves (`COLUMNS` from 21 to 9999, else the standard-error
console, else 79).
On Windows each `-o` name is first rewritten by `Curl.Core`'s `WindowsOutputFileNameSanitizer`
(`"*<>?|` and control characters become `_`, as curl 8.21.0 does; not under `-g`, see below),
and that name is the one opened, sized for `-C -` and named in every message.

Before its transfers, each command-line URL is expanded as a glob by `Curl.Core`'s `UrlGlob`
(`{a,b}`, `[1-3]`, ...; under `-g` the URL is taken as written), and every URL it expands to is
one transfer, a `UrlTransfer`: all of them share the command-line URL's output entry, `-T` file
and `%{urlnum}`, while `%{xfer_id}` counts transfers across the run. Each `#N` in the `-o` name
takes glob N's value and, on Windows and not under `-g`, the result is sanitized
(`UrlGlobMatch.ResolveOutputFileName`); a `-D` file is truncated by the first transfer and
appended to by every later one, even of the same glob. A URL that is not a well-formed glob
prints curl's `curl: (3) bad range in position N:` lines (not under `-s`) and ends the run with
exit 3. An `ipfs://` or `ipns://` URL is then rewritten by `IpfsGatewayRewriter` from
`--ipfs-gateway`, `IPFS_GATEWAY` or the gateway file (read through the runner's data-file
reader; the environment comes from the runner's `readEnvironmentVariable`, the process's in
production and none in tests unless given), and `%{url}` prints the gateway URL; one that
cannot be rewritten prints `curl: <message>` and the try-help line even under `-s`, has
`%{xfer_id}` and `%{conn_id}` `-1`, and ends the run with exit 37 or 3. Before that, `-O` or
`--remote-name-all` on an IPFS URL with no `-o` name is refused as curl 8.21.0 refuses it,
gateway or not: `curl: Failed to extract a filename from the URL to use for storage` and
`curl: (1) Unsupported protocol` (neither under `-s` alone), `%{xfer_id}` and `%{conn_id}`
`-1`, exit 1, and the run ends (BL-372 Notes). A URL still without a
scheme gets the one `UrlSchemeGuesser` guesses (`http`, or `ftp` for `ftp.` and so on), which
`%{url_effective}` shows while `%{url}` keeps the URL as typed. Measured on curl 8.21.0
(BL-240 Notes). `%{url_effective}` prints the URL `UrlEffective` rebuilds: the scheme in
lower case, the authority as typed, and the path as curl sends it - `/` when empty, dot
segments removed unless `--path-as-is` - before the query and fragment; a URL curl rejects
prints as typed, as curl 8.21.0 does (BL-371 and BL-444 Notes).

Each URL's output comes from `CommandLineOptions.UrlOutputs`: an `-o` name, or for `-O` /
`--remote-name-all` the name `RemoteFileName` takes from the URL path (last non-empty
segment, still percent-encoded; none gives `curl_response` and curl's
`Warning: No remote filename` line unless `-s`). On Windows a remote name also goes through
`WindowsOutputFileNameSanitizer.SanitizeRemoteName` (`:` becomes `_`, DOS device names are
renamed). `--output-dir` is put in front of either with `/`, as typed. `--create-dirs` makes
each leading directory through `IOutputPaths` (`PhysicalOutputPaths` in production) before the
transfer; one that cannot be made prints `curl: Error creating directory <dir>` and stops the
run with exit 23. Under `-J` a remote-named file's header output goes through
`RemoteHeaderNameStream`, which opens the file under the first `Content-Disposition`
`filename=` (`ContentDispositionFileName`) of a 2xx or 3xx response before the lines go on; a
name already taken is refused with `File exists` and exit 23. Measured on curl 8.21.0
(BL-239 Notes).

`TransferContextFactory` builds each transfer's context from the parsed options; the
context carries the `-r` text as given (`RangeText`, which the HTTP handler sends verbatim)
and its parsed range (`ByteRangeParser`; text that names no range ends the transfer with exit
33 before it is dispatched, except on an `http`/`https` URL, BL-386), the `-C` offset and the
`--max-filesize` limit. `-C -` resumes from the size of the URL's `-o` file, and a transfer
that resumes past byte zero opens that file for appending before it starts, as curl does.
Every context also carries `Http`, which `HttpRequestOptionsMapping` fills from `-X`,
`--request-target` (sent verbatim as the request-line target), `-H`, `-A`, `-e`, the `-d` family and `--json`: `--json` appends `Content-Type: application/json` and
`Accept: application/json` after the `-H` headers unless a `-H` header already starts with
that name (case-insensitive), and a body is a `BytesBody` sent as
`application/x-www-form-urlencoded` unless `-G` moved it into the query. The runner appends
the `-G` / `--url-query` query with `QueryUrl` before the URL is parsed. `http` and
`https` are served by `HttpProtocolHandler`, registered in `CurlComposition` with a
`RankedHttpAuthenticator` (Basic and Bearer, and Digest with a random client nonce, all in the
platform's credential encoding), which answers the scheme `-u`, `--basic`, `--digest`,
`--anyauth` and `--oauth2-bearer` allow (`HttpRequestOptions.AuthSchemes` and `BearerToken`).
`-0` / `--http1.0` and `--http1.1` set `HttpRequestOptions.Version` (the last one wins, HTTP/1.1
when neither is given), and `--compressed`, `--tr-encoding`, `--raw` and `--ignore-content-length`
are copied as they are (BL-236); `CurlCommandRunnerTransferEncodingTests` pins each one's request
bytes and output as BL-177, BL-180 and BL-315 measured them.
With `-b` or `-c` the handler also gets the run's `CookieEngine`: one `CookieStore` shared by
every URL, the `-b` files loaded before the first transfer (session cookies dropped under
`-j`, a missing file ignored), the `-b name=value` strings sent after the stored cookies (left out when an `-H` value names
`Cookie`, BL-291), and
the `-c` jar written after every `http`/`https` transfer, after its `-w` output, whatever its
outcome, and after no other scheme's (`-c -` prints it to standard output each time, in the
mode standard output is in). With nothing but `-b` strings, received cookies are not stored,
as curl's cookie engine stays off. Measured on curl 8.21.0 (BL-237 Notes).
`-D -` sends the handler's header lines to standard output; any other `-D` name is opened
(unsanitized, truncated for the first transfer and appended for the rest) before the transfer,
and one that cannot be opened prints `curl: Failed to open <file>` and stops the run with
exit 23. `-i` and `-I` send the header lines to the body output too (standard output or the
`-o` file); with `-D` as well, `HeaderLineTeeStream` writes each line to the `-D` output and
then the body output before the next, so `-i -D -` prints every header line twice in a row,
as curl 8.21.0 does. `-I` also sets the context's `NoBody`, and `-f` / `--fail-with-body`
become `HttpRequestOptions.Fail`. Under `--fail-early` the first failed transfer stops the
run with its own exit code.

A `-d` / `--data*` / `--json` body to be posted (no `-G`) with `-I` (HEAD) or `--no-head` (GET) is
refused at transfer setup, not while parsing, as curl 8.21.0 refuses it in `tool_operate`: after
the parse is accepted and `-V` is handled, and before any dispatch is built, `RunAsync` writes
`CommandLineWarning.PostRequestedWithHead` or `PostRequestedWithGet` (nothing under `-s`, even
with `-S`), with no `curl: try` line, and exits 2, whatever order the options came in (BL-255).

The Nth `-T` / `--upload-file` value uploads to the Nth URL (ADR-0051). Its URL is resolved
by `UploadTransferUrl` before anything else of that transfer: one it cannot parse is exit 3
with no warning lines. The `-T` file is opened through the runner's `IFileSystem` after the
before-transfer warning lines and becomes the context's `Upload`; one that cannot be opened
prints `curl: cannot open '<file>'` and the try-help line even under `-s`, is exit 26, and
stops the run, a `-T` glob match as much as a lone file. When the run's previous transfer
failed, it reports that transfer's code instead, with the text `CurlEasyErrorText` gives it
(`curl: (7) Could not connect to server`), as curl 8.21.0 does (BL-440 Notes). `-T -` and `-T .` upload standard input. `%{url_effective}` prints the
resolved URL.

Each transfer's proxy is chosen by `TransferProxySelection`, after the URL, range and `-F`
body are checked: `Curl.Core`'s `ProxySelector` (held by `TransferDispatch`, reading the
process's proxy environment variables in production and none in tests unless given) picks it
from `-x` or a `--socks` option, `--noproxy` and the variables; `-U` replaces its credential;
it goes into `HttpRequestOptions.ForwardProxy` with `-p` as `ProxyTunnel`. Proxy text curl
cannot use ends the transfer with the selector's exit 5 or 7, and a SOCKS proxy, or an HTTPS
proxy for `https` or under `-p` or `-L`, ends an `http`/`https` transfer with exit 4 until the connector opens those tunnels
(ADR-0053, BL-328). Other schemes do not read the proxy yet (BL-330), and redirect hops keep
the first URL's proxy (BL-329). Measured on curl 8.21.0 (BL-238 Notes).
An `ftp` or `ftps` URL is claimed by `RoutingFtpProtocolHandler`, which hands an `ftp` one to
the HTTP handler when its proxy is `Http` or `Http10` and `-p` is not given, so it is forwarded
to the proxy as `GET ftp://host/path` with `Host: host:21` (ADR-0056, rule 3; BL-344); any
other transfer, `ftps` through an HTTP proxy included (curl 8.21.0 tunnels it with
`CONNECT host:990`, BL-458), goes to `FtpProtocolHandler` over the pooling connector
(ADR-0093, BL-434). `CurlComposition.CreateFtpProtocolHandler` builds it with a
`TcpConnectionListener` for `-P`, the run's TLS provider and DNS resolver, and a
`SystemNetworkInterfaceLookup` (ADR-0102, ADR-0108, ADR-0110), and `TransferContextFactory`
copies `-P`, `--disable-eprt`, `--ssl`/`--ssl-reqd` and `--ftp-ssl-control` into the context.

Every transfer goes through `Curl.Core`'s `RedirectFollower`. `-L` becomes
`HttpRequestOptions.FollowRedirects`, and `RedirectPolicyMapping` turns `--max-redirs`,
`--post301`/`--post302`/`--post303` and `--location-trusted` into its `RedirectPolicy`. Every
hop writes to the same body and header outputs, so `-L -i` prints every response's head and
only the last body, and one redirect past `--max-redirs` exits 47 with
`curl: (47) Maximum (N) redirects followed`, as measured on curl 8.21.0 (BL-234).

Under `-Y`/`--speed-limit` or `-y`/`--speed-time` each attempt gets a `Curl.Core`
`LowSpeedWatchdog` on the runner's clock: `TransferContextFactory` wraps the output and the
progress sink so it counts the bytes moved, and gives the context its token. An attempt it
cancels after `-y` seconds below `-Y` (30 seconds, or 1 byte per second, when only one is
given) ends with `curl: (28) Operation too slow. Less than N bytes/sec transferred the last T
seconds`, as measured on curl 8.21.0 (ADR-0106, BL-400).

Under `-R`/`--remote-time` a successful transfer to an `-o` file whose result carries
`SourceLastWriteTimeUtc` stamps the closed file with it through `IFileTimeSetter`
(`PhysicalFileSystem` in production), even when no body was written, as curl does. A
failed stamp is ignored for now; curl's warning lines for it are BL-139.

Under `-v`, `--trace` or `--trace-ascii` every transfer's context carries the run's
`ITransferEvents`, which `TransferEventOutput` opens once the first command-line URL has parsed
as a glob and closes after the last transfer (ADR-0046): `-v` is `Curl.Output`'s
`VerboseTransferEventWriter` on standard error, with no `[N bytes data]` lines when standard
output is a terminal; a trace is its `TraceTransferEventWriter`, stamped under `--trace-time`, into
the named file (opened once per run, truncated), standard output for `-`, standard error for `%`,
and standard error, with no warning, for a file that cannot be opened. On Windows each is text
mode, CR LF. Measured on curl 8.21.0 (BL-242 Notes). The lines are only as complete as what the
handler and connector report (BL-242 Notes name the follow-ups).

`--stderr <file>` replaces the runner's standard error where it stands among the parser's
warning lines, as curl opens the file while parsing: each `StandardErrorRedirect` in
`CommandLineParseResult.StandardErrorRedirects` says how many warning lines came before it, so
those go where standard error went until then and the rest, a refusal's lines, the config-file
note and every later line (`-v`, warnings, `curl: (N)`, the progress meter, `-w %{stderr}`) go to
the file, opened truncated even when nothing is written, or to standard output for
`--stderr -`. A later `--stderr` closes the earlier file and takes over. A file that cannot be
opened prints `Warning: Warning: Failed to open <file>` where standard error goes at that point,
unless `-s` came before the option, and standard error stays put (BL-410, BL-476 Notes).

After each successful transfer, after one `-f` failed with exit 22, and after one that failed
once its handler reported it past connect or open (BL-130), standard error gets curl's progress
meter: `** Resuming transfer from byte position N` when it resumed past byte zero (N is `-1`
for a `-T` upload under `-C -`, whatever the `-o` file holds, BL-416), the two
header lines (`ProgressMeterLines`), the status lines, and one newline. The status lines come
from `TransferProgressRecorder`, the transfer's `ITransferProgress` sink, which draws them on the
runner's `TimeProvider` as curl 8.21.0's `progress_calc` and `progress_meter` do, with the fields
`ProgressMeterFields` formats (`max6out`, `time2str`): the all-zero line when the transfer
starts, a line for a byte report a second or more after the last speed sample, and, when the
handler reported any bytes, three done lines after a success or one more update after a failure.
A handler that reports no bytes, as `file://`'s does not, leaves only the zero line - every byte
curl writes for a `file://` transfer. From the handler's first "transfer started" report the
recorder hands each drawn line to the runner, which writes it (after the header lines the first
time) to standard error synchronously, so a terminal sees it move; the end draws, the newline,
and the whole meter of a transfer never reported started are written after the transfer, so the
bytes are BL-131's (ADR-0099, BL-383). The `-v` and trace output goes through a `HoldableStream`
over standard error, which the recorder holds when the handler reports the transfer done (while
the meter is written live) and the runner releases after the meter's end, so the connection-end
`-v` line follows the meter as in curl 8.21.0 (ADR-0116, BL-411). It is not written under `-s`,
`--no-progress-meter` or `-#`, nor for a body on standard output when that is a terminal.

Under `-#` (and not `-s`, `--no-progress-meter` or a body on a terminal) the recorder passes
every report on to a `ProgressBarRecorder` instead, which draws curl 8.21.0's bar as
`tool_progress_cb` does: `\r`, `#` padded to the width less seven, and ` %5.1f%`, drawn when
the position moves, at most every 100 ms below 100%, with `fly`'s `-=O=-` animation while
the size is unknown. The width is `terminalColumns`, clamped to 20..400; the `-C` offset
counts towards the position and the total. A successful transfer whose handler reported no
bytes gets one last call with its byte count, which is how a `file://` transfer ends on a
full bar. The bar is written after the transfer. Its newline, written when the handler
reported the transfer started, comes after the failure lines and before the `-w` output, as
in curl. No `** Resuming` line is written under `-#` (ADR-0082, BL-132 Notes).

With `-w`, each transfer's template is rendered by `Curl.Output`'s `WriteOutTemplateRenderer`
after its failure lines, after a failure as after a success (a `-D` or resumed `-o` file that
cannot be opened included), with `TransferWriteOutVariables` as its values. Its `%time{format}`
follows the `WriteOutTimeDialect` the runner is given: `CurlComposition.WriteOutTimeDialectFor`
passes `WindowsCRuntime` on Windows and `Glibc` elsewhere (ADR-0078, BL-387). On Windows the
line feeds it writes to standard error, and to standard output while curl's standard output
would still be in text mode, go through `LineFeedToCrLfStream` as CR LF (ADR-0081).
`%output{file}` targets go through the runner's `IWriteOutFileOpener`: `CurlComposition`
passes `DiskWriteOutFileOpener`, which opens each file shared for writing (truncated, or
appended for `%output{>>file}`), in text mode on Windows, and refuses one it cannot open;
a runner given none uses `RefusingWriteOutFileOpener`, which opens none.

A URL with no `-o` writes through `StandardOutputFailureDeferringStream`, which
models curl's 4096-byte stdio buffer: a failed standard output is reported as
`curl: Failed writing body` (exit 23) while the body fits the buffer, and as the
handler's own `(23)` write failure once it would not.

`Program.Main` opens standard output with `StandardOutputOpener`, not
`System.Console.OpenStandardOutput()`, because the latter hides both failures that
stream models: it returns `Stream.Null` for a closed standard output and reports a write
to a pipe whose reader has gone as a success. A console is still opened the .NET way; a
redirected standard output becomes an unbuffered `FileStream` over the process's own
handle (`GetStdHandle` on Windows, descriptor 1 elsewhere), and a closed one a
`ClosedStandardOutputStream` whose writes throw.
