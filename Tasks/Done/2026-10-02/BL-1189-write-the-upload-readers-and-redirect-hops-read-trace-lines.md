---
id: BL-1189
title: Write the upload readers' and redirect hops' [READ] trace lines
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-1159]
touches: [Curl.Core.UnitLibrary, Curl.Core.UnitTests, Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-10-02
completed: 2026-10-02
---
# BL-1189 — Write the upload readers' and redirect hops' [READ] trace lines

## Goal

Curl writes curl 8.21.0's `[READ]` lines for an upload body's readers and for each followed redirect hop under `--trace-config read`, `all`, `-vvv` and `-vvvv`.

## Context

- Split from BL-1159, which writes the two `[READ] client_reset, clear readers` lines of a plain transfer (`ClientReaderResetTraceEvents`, `CurlCommandRunner.TraceClientReaderReset`). Measured 2026-10-02 for `-d ab`: after `using HTTP/1.x`, `[READ] add buf reader, len=2 -> 0`, `[READ] cr_buf_read(len=65388) -> 0, nread=2, eos=1`, `[READ] client_read(len=65388) -> 0, nread=2, eos=1`, before `upload completely sent off`. `-L` hops were not measured (`Record-CurlExchange.ps1` loops answering a self-redirect; serve a distinct second response).

## Acceptance criteria

- [x] Measured first with `Record-CurlExchange.ps1` for `-d`, `-T` and `-L` with one redirect; stderr in Notes.
- [x] Tests pin each case's `[READ]` lines, and that none appears without `read` or `all`.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage for each library changed.

## Notes

- Measured 2026-10-02, curl 8.21.0 (mingw, Schannel), `Record-CurlExchange.ps1` (`200`, `Content-Length: 2`, `hi`) on 127.0.0.1:47811; fixtures in `%TEMP%\bl1189\<case>`. Only `*` and `}` lines shown.
  - `-d ab`: `[READ] client_reset, clear readers`, `Trying`, `Established`, `using HTTP/1.x`, `[READ] add buf reader, len=2 -> 0`, `[READ] cr_buf_read(len=65388) -> 0, nread=2, eos=1`, `[READ] client_read(len=65388) -> 0, nread=2, eos=1`, then the `>` head, `} [2 bytes data]`, `upload completely sent off: 2 bytes`, ..., `[READ] client_reset, clear readers`, `Connection #0 ... left intact`. 65388 = 65536 less the 148-byte head.
  - `-T up.txt` (3 bytes): `[READ] add fread reader, len=3 -> 0`, `[READ] cr_in_read(len=3, total=3, read=3) -> 0, nread=3, eos=1`, `[READ] client_read(len=65432) -> 0, nread=3, eos=1` (104-byte head), then as above.
  - `-d @big` (100000 bytes): `add buf reader, len=100000 -> 0`, `cr_buf_read(len=65383) -> 0, nread=65383, eos=0`, `client_read(len=65383) ...eos=0`, `} [65383 bytes data]`, `cr_buf_read(len=65536) -> 0, nread=34617, eos=1`, `client_read(len=65536) ...eos=1`, `} [34617 bytes data]`. `-T big`: `add fread reader, len=100000 -> 0`, `cr_in_read(len=65426, total=100000, read=65426) -> 0, nread=65426, eos=0`, `client_read(len=65426)`, `}`, `cr_in_read(len=34574, total=100000, read=100000) -> 0, nread=34574, eos=1`, `client_read(len=65536) ...eos=1`.
  - `-L` to `/a`, `302 Location: /b` with `Connection: close`: `Request completely sent off`, `[READ] client_reset, clear readers`, `shutting down connection #0`, `Issue another request to this URL: 'http://127.0.0.1:47811/b'`, `[READ] client_reset, clear readers`, `Hostname 127.0.0.1 was found in DNS cache`, ... `[READ] client_reset, clear readers`, `Connection #1 ... left intact`. Kept alive: the same `[READ]` pair around `left intact` / `Issue another request`, then `Connection 0 seems to be dead`.
  - `-d ab -L` on 302 or 307: rewind lines (`client reader needs rewind before next request`, plain `Need to rewind upload for next request`, `client_reset, will rewind reader`, `client start, rewind readers`) - filed as BL-1213. `-T -` (chunked, 100-continue) lines - BL-1214. `-d ''`: no reader lines, `Request completely sent off` - BL-1216.
- Decided by Claude under Stewart's delegation (ADR-0383): the body lines come from `HttpRequestBodyWriter` (only it knows the read sizes), behind `HttpProtocolHandler.TracesClientReaders`, set from `CurlComposition.TracesRead` through `CurlTransports.TracesRead`; only unchunked HTTP/1.x `-d` and known-length `-T` bodies without a `100 Continue` wait, the shapes measured. A traced `-d` body is sent in the upload buffer's pieces so its `}` lines match curl's. The hop's reset line comes from `ClientReaderResetTraceEvents`, after `Issue another request`. No change to `Curl.Core` was needed.
- Verified with Curl's own build through `Record-CurlExchange.ps1 -Curl`: `-d ab`, `-T` 3 bytes, `-T` 100000 bytes and `-L` match real curl's stderr byte for byte (ports aside). `-d @big` traces right but the transfer itself fails exit 55/56 against the recorder, as it did before this change - filed as BL-1215.
- Tests: `HttpProtocolHandlerTests.ExecuteAsync_PostWithDataTracingReaders_*`, `_UploadTracingReaders_*`, `_PostWithDataNotTracingReaders_*`, `_Http2PostTracingReaders_*`; `HttpRequestBodyWriterTests.WriteAsync_Traced*`, `_TracingBodiesWithoutMeasuredLines_*`, `_StreamBodyThatIsNotAnUpload_*`; `ClientReaderResetTraceEventsTests.ReportInfo_TheRedirectsIssueAnotherRequestLine_*`; `CurlCompositionReadTraceTests`. `Measure-CodeQuality.ps1`: `Curl.Protocol.Http.UnitLibrary` and `Curl.Console` 100% line and branch; their remaining failing members (`Http2FrameTrace.Describe`, `CurlCommandRunner.TransferUrlAsync`, complexity 12) predate this task and were not touched.

## Log

- 2026-10-02: Created.
- 2026-10-02: Backlog -> Doing.
- 2026-10-02: Doing -> Done. --trace-config read writes curl's [READ] upload reader lines for -d and -T bodies and the reset line of each -L hop; follow-ups BL-1213..BL-1216
