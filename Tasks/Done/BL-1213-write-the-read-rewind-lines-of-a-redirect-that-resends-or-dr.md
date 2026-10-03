---
id: BL-1213
title: Write the [READ] rewind lines of a redirect that resends or drops a request body
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-10-02
completed: 2026-10-02
---
# BL-1213 — Write the [READ] rewind lines of a redirect that resends or drops a request body

## Goal

Under `--trace-config read` a `-L` hop after a request with a body writes curl 8.21.0's rewind lines, and plain `-v` writes `Need to rewind upload for next request`.

## Context

- Split from BL-1189 (ADR-0383). Measured 2026-10-02 with `Record-CurlExchange.ps1`, `-d ab -L` to `/a` answered `302`/`307` `Location: /b` (`Connection: close`): after the 302's status line `[READ] client reader needs rewind before next request` and the plain `-v` line `Need to rewind upload for next request`; then `[READ] client_reset, will rewind reader` in place of `clear readers` before `shutting down connection #0` (or `left intact`); then `[READ] client start, rewind readers` before `Issue another request to this URL`. A 307 resends the body with the buffer reader's three lines again; a 302 sends GET.
- Curl today writes none of these; `Need to rewind upload for next request` is missing even under plain `-v`.

## Acceptance criteria

- [x] Re-measured with `Record-CurlExchange.ps1` for `-d ab -L` on a 302 and a 307, closing and kept-alive; stderr in Notes.
- [x] Tests pin each case's lines, the `-v` line without `--trace-config read`, and no `[READ]` line without `read` or `all`.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage for each library changed.

## Notes

- Measured 2026-10-02, curl 8.21.0 (mingw, Schannel), `Record-CurlExchange.ps1 -Connections 2`, `-v --trace-config read -d ab -L http://127.0.0.1:47811/a`, first answer `302 Found` or `307 Temporary Redirect` with `Location: /b`, `Content-Length: 0` and, closing, `Connection: close`; second `200` `hi`. Fixtures in `%TEMP%\bl1213\<code>-<close|keep>`. `*`, `<` and `}` lines:
  - 302 closing: `[READ] client_reset, clear readers`, `Trying`, `Established`, `using HTTP/1.x`, `[READ] add buf reader, len=2 -> 0`, `[READ] cr_buf_read(len=65387) -> 0, nread=2, eos=1`, `[READ] client_read(len=65387) -> 0, nread=2, eos=1`, `} [2 bytes data]`, `upload completely sent off: 2 bytes`, `< HTTP/1.1 302 Found`, `[READ] client reader needs rewind before next request`, `Need to rewind upload for next request`, `< Location`..., `[READ] client_reset, will rewind reader`, `shutting down connection #0`, `[READ] client start, rewind readers`, `Issue another request to this URL: 'http://127.0.0.1:47811/b'`, `[READ] client_reset, clear readers`, `Hostname 127.0.0.1 was found in DNS cache`, `Trying`, `Established`, `Request completely sent off` (GET), `[READ] client_reset, clear readers`, `Connection #1 ... left intact`.
  - 302 kept alive: the same up to the status line; then `Ignoring the response-body`, `setting size while ignoring`, `[READ] client_reset, will rewind reader`, `Connection #0 ... left intact`, `[READ] client start, rewind readers`, `Issue another request`, `[READ] client_reset, clear readers`, `Connection 0 seems to be dead`, `shutting down connection #0`, then the new connection as above.
  - 307 closing and kept alive: as the 302's, but the second hop sends the body again with the three buffer reader lines, `} [2 bytes data]` and `upload completely sent off: 2 bytes`.
  - Plain `-v` (no `--trace-config read`): `< HTTP/1.1 302 Found`, `Need to rewind upload for next request`, `< Location: /b`, and no `[READ]` line.
  - Not written: without `-L` (302), with `-L` after a `404`, and for `-d ''` under `-L` on a 302 (`Request completely sent off`, no rewind lines). A `304` with `Location` under `-L` wrote the rewind lines and was followed; the recorder hung on it, so that case is not pinned.
- Decided by Claude under Stewart's delegation (ADR-0392): `HttpProtocolHandler` writes `Need to rewind upload for next request` after any `3xx` status line under `-L` when the request has a body whose length is not 0, through a new nullable `HttpResponseHeadReader.StatusLineReported` hook; `ClientReaderResetTraceEvents` (only in the chain under `read`/`all`) adds the three `[READ]` lines around it.
- Verified with Curl's own build through `Record-CurlExchange.ps1 -Curl`: 302 and 307 closing match real curl's stderr line for line (ports aside). Kept alive: every rewind line matches; after them Curl reuses the dead connection and retries (`Reusing existing http: connection`, `Connection died, retrying a fresh connect`) where curl wrote `Connection 0 seems to be dead`. That is a timing race in detecting the dead connection: real curl's own plain `-v` run took the reuse path too. It is not part of this task.
- Tests: `HttpProtocolHandlerTests.ExecuteAsync_FollowedRedirectAfterABody_*`, `_RedirectAfterABodyNotFollowed_*`, `_FollowedRedirectAfterAnEmptyBody_*`, `_ResponseThatIsNotARedirectAfterABody_*`, `_FollowedRedirectWithoutABody_*`; `ClientReaderResetTraceEventsTests.ReportInfo_ARedirectHopThatNeedsItsBodyRewound_*`; `CurlCommandRunnerTransferEventTests.RunAsync_VerboseRewindWithoutTraceConfigRead_*`, `_TraceConfigReadRewind_*`.
- `Measure-CodeQuality.ps1`: `Curl.Protocol.Http.UnitLibrary` and `Curl.Console` 100% line and branch; Curl.Console has no failing member. Http still has three members over the complexity cap: `HttpRequestBodyWriter.ReportStreamRead` (12), `HttpProtocolHandler.ExchangeAsync` (12) and `HttpResponseHeadReader..ctor` (12). This task added no branch to any of them; the constructor briefly measured 14 with the new hook as a cached default lambda, so the hook is nullable instead.

## Log

- 2026-10-02: Created.
- 2026-10-02: Backlog -> Doing.
- 2026-10-02: Doing -> Done. -L after a request body writes Need to rewind upload for next request, and --trace-config read adds curl's three rewind [READ] lines
