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

`Program.Main` only opens the standard streams, builds the composition and hands the
arguments to `CurlCommandRunner`, which parses them, runs each URL and prints curl's
`curl: (N) <message>` lines. `-o` files open on the first write through
`DeferredOutputFileStream`, which is how curl's exit 23 message comes out right. The
parser's warning lines are written to standard error before anything else.
Every `Warning: ` line is wrapped by `WarningLineWrapper` as curl's `warnf` wraps it, at the
width `TerminalColumns` resolves (`COLUMNS` from 21 to 9999, else the standard-error
console, else 79).
On Windows each `-o` name is first rewritten by `WindowsOutputFileNameSanitizer`
(`"*<>?|` and control characters become `_`, as curl 8.21.0 does), and that name is the
one opened, sized for `-C -` and named in every message.

`TransferContextFactory` builds each transfer's context from the parsed options; the
context carries the parsed `-r` range (`ByteRangeParser`; text that names
no range ends the transfer with exit 33 before it is dispatched), the `-C` offset and the
`--max-filesize` limit. `-C -` resumes from the size of the URL's `-o` file, and a transfer
that resumes past byte zero opens that file for appending before it starts, as curl does.
Every context also carries `Http`, which `HttpRequestOptionsMapping` fills from `-X`,
`-H`, `-A`, `-e`, the `-d` family and `--json`: `--json` appends `Content-Type: application/json` and
`Accept: application/json` after the `-H` headers unless a `-H` header already starts with
that name (case-insensitive), and a body is a `BytesBody` sent as
`application/x-www-form-urlencoded` unless `-G` moved it into the query. The runner appends
the `-G` / `--url-query` query with `QueryUrl` before the URL is parsed. `http` and
`https` are served by `HttpProtocolHandler`, registered in `CurlComposition` with a
`RankedHttpAuthenticator` (Basic and Bearer, and Digest with a random client nonce, all in the
platform's credential encoding), which answers the scheme `-u`, `--basic`, `--digest`,
`--anyauth` and `--oauth2-bearer` allow (`HttpRequestOptions.AuthSchemes` and `BearerToken`).
With `-b` or `-c` the handler also gets the run's `CookieEngine`: one `CookieStore` shared by
every URL, the `-b` files loaded before the first transfer (session cookies dropped under
`-j`, a missing file ignored), the `-b name=value` strings sent after the stored cookies, and
the `-c` jar written after every `http`/`https` transfer, after its `-w` output, whatever its
outcome, and after no other scheme's (`-c -` prints it to standard output each time, in the
mode standard output is in). With nothing but `-b` strings, received cookies are not stored,
as curl's cookie engine stays off. Measured on curl 8.21.0 (BL-237 Notes).
`-D -` sends the handler's header lines to standard output; any other `-D` name is opened
(unsanitized, truncated for the first URL and appended for the rest) before the transfer,
and one that cannot be opened prints `curl: Failed to open <file>` and stops the run with
exit 23. `-i` and `-I` send the header lines to the body output too (standard output or the
`-o` file); with `-D` as well, `HeaderLineTeeStream` writes each line to the `-D` output and
then the body output before the next, so `-i -D -` prints every header line twice in a row,
as curl 8.21.0 does. `-I` also sets the context's `NoBody`, and `-f` / `--fail-with-body`
become `HttpRequestOptions.Fail`. Under `--fail-early` the first failed transfer stops the
run with its own exit code.

The Nth `-T` / `--upload-file` value uploads to the Nth URL (ADR-0051). Its URL is resolved
by `UploadTransferUrl` before anything else of that transfer: one it cannot parse is exit 3
with no warning lines. The `-T` file is opened through the runner's `IFileSystem` after the
before-transfer warning lines and becomes the context's `Upload`; one that cannot be opened
prints `curl: cannot open '<file>'` and the try-help line even under `-s`, is exit 26, and
stops the run. `-T -` and `-T .` upload standard input. `%{url_effective}` prints the
resolved URL.

Every transfer goes through `Curl.Core`'s `RedirectFollower`. `-L` becomes
`HttpRequestOptions.FollowRedirects`, and `RedirectPolicyMapping` turns `--max-redirs`,
`--post301`/`--post302`/`--post303` and `--location-trusted` into its `RedirectPolicy`. Every
hop writes to the same body and header outputs, so `-L -i` prints every response's head and
only the last body, and one redirect past `--max-redirs` exits 47 with
`curl: (47) Maximum (N) redirects followed`, as measured on curl 8.21.0 (BL-234).

Under `-R`/`--remote-time` a successful transfer to an `-o` file whose result carries
`SourceLastWriteTimeUtc` stamps the closed file with it through `IFileTimeSetter`
(`PhysicalFileSystem` in production), even when no body was written, as curl does. A
failed stamp is ignored for now; curl's warning lines for it are BL-139.

After each successful transfer, and after one `-f` failed with exit 22, standard error gets the opening of curl's progress meter
(`ProgressMeterLines`): `** Resuming transfer from byte position N` when it resumed past
byte zero, the two header lines, and the all-zero status line - every byte curl 8.21.0
writes for a `file://` transfer. It is not written under `-s`, `--no-progress-meter` or
`-#`, nor for a body on standard output when that is a terminal. Live counters, the bar
form and the meter after any other failed transfer are not modelled yet (BL-130 to BL-132).

With `-w`, each transfer's template is rendered by `Curl.Output`'s `WriteOutTemplateRenderer`
after its failure lines, after a failure as after a success (a `-D` or resumed `-o` file that
cannot be opened included), with `TransferWriteOutVariables` as its values. On Windows the
line feeds it writes to standard error, and to standard output while curl's standard output
would still be in text mode, go through `LineFeedToCrLfStream` as CR LF (ADR-0040).
`%output{file}` targets go through the runner's `IWriteOutFileOpener`; the default,
`RefusingWriteOutFileOpener`, opens none until BL-280.

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
