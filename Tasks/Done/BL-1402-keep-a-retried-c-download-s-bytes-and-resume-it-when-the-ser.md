---
id: BL-1402
title: Keep a retried -C - download's bytes and resume it when the server takes ranges, with curl's 'Keeping N bytes' and 'Throwing away N bytes' notes
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1396]
touches: [Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-10-03
completed: 2026-10-03
---
# BL-1402 — Keep a retried -C - download's bytes and resume it when the server takes ranges, with curl's 'Keeping N bytes' and 'Throwing away N bytes' notes

## Goal

Under `--retry` with `-o <file>`, a failed attempt's bytes are handled as curl 8.21.0's `retrycheck` handles them: with `-C -` on an HTTP(S) GET whose response was a `206` to a resume or a `200` carrying `Accept-Ranges: bytes`, the bytes are kept (`Note: Keeping N bytes` under `-v`) and the next attempt resumes after them; otherwise the file is cut back as today and `-v` shows `Note: Throwing away N bytes`.

## Context

- Today `Curl.Console/CurlCommandRunner.cs` `WriteRetryLinesAsync` (around line 4498) always calls `outputFile?.TruncateForRetry()`, and `FollowRetryingAsync` builds every attempt's context with the same resolved `-C` offset, so a retried `-C -` download starts again from the original offset; neither note text exists in the solution.
- curl 8.21.0 (tag `curl-8_21_0`), `src/tool_operate.c`:
  - `is_outfile_auto_resumable` (lines 343-357): `-C -` was given (`use_resume && resume_from_current`), the output is a regular file it opened, bytes were written this attempt, no `-X`, no upload, the request is a plain GET, and the attempt's result is neither exit 23 nor exit 33.
  - `retrycheck` (lines 512-600): when that holds and the scheme is `http`/`https`, the effective method is `GET`, and the response is `206` with a resume offset set or `200` with an `Accept-Ranges: bytes` header, it writes `notef("Keeping %" CURL_FORMAT_CURL_OFF_T " bytes", outs->bytes)`, adds the bytes to the resume offset and sets `CURLOPT_RESUME_FROM_LARGE` for the next attempt, and does not truncate. Otherwise, when bytes were written to a regular output file, it writes `notef("Throwing away %" CURL_FORMAT_CURL_OFF_T " bytes", outs->bytes)` and truncates back to where the attempt started. `notef` writes only under `-v`.
- Measured 2026-10-03 with curl 8.21.0 (mingw, Schannel) and `Record-CurlExchange.ps1 -Connections 2`, `--no-progress-meter --retry 1 --retry-all-errors --retry-delay 1 -o <file>`:
  - `-v -C -`, first response `HTTP/1.1 200 OK\r\nAccept-Ranges: bytes\r\nContent-Length: 10\r\n\r\nhello` (closed short), second `HTTP/1.1 206 Partial\r\nContent-Range: bytes 5-9/10\r\nContent-Length: 5\r\n\r\n56789`: stderr has `curl: (18) end of response with 5 bytes missing`, `Warning: Problem (retrying all errors). Retrying in 1 second. 1 retry left.`, `Note: Keeping 5 bytes`; the second request carries `Range: bytes=5-`; the file is `hello56789`; exit 0.
  - `-v -C -` with no `Accept-Ranges` on the first response and `HTTP/1.1 200 OK\r\nContent-Length: 10\r\n\r\n0123456789` second: `Note: Throwing away 5 bytes` after the warning; the second request has no `Range`; the file is `0123456789`.
  - without `-C -`, with `Accept-Ranges: bytes` on the first: no `Range` on the retry and the file holds only the second body (truncated as today); without `-v` no note.

## Acceptance criteria

- [x] Tests in `Curl.Console.UnitTests` over fake connectors pin the first measured case: the note line after the retry warning, the retry's `Range: bytes=5-`, the output file `hello56789`, exit 0.
- [x] Tests pin the second case (the throw-away note, no `Range`, the file `0123456789`), the same with `-v` absent (no note), and the case without `-C -` (truncated, no `Keeping` note, `Throwing away 5 bytes` under `-v`).
- [x] Tests pin no keeping for `-C -` with `-X GET`, with `-d`, and for an attempt that failed with exit 23; and nothing kept or noted when the body goes to standard output.
- [x] `dotnet build Curl.Console.UnitTests -warnaserror` is clean; `dotnet test Curl.Console.UnitTests --filter "TestCategory!=Integration"` passes; `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Console` reports no failing member in the code this task changed.

## Notes

- `Curl.Core.UnitLibrary/TransferRetrier.cs` says every attempt runs on a context "unchanged" and leaves readying the output to its caller; keep the resume decision in `Curl.Console`, where the attempt context is built (`createAttemptContext`), unless the retrier's contract has to change, in which case say so and file it.
- Depends on BL-1396 only because both change `Curl.Console`. No option changes, so `--ai-help` is unaffected.
- Done (2026-10-03): the decision stays in `Curl.Console`; `TransferRetrier`'s contract is unchanged. `FollowRetryingAsync` now creates each attempt's context from that attempt's `-C` offset (`Func<long?, TransferContext>`), and its retry callback asks `AttemptBytesKeptForResume` (`IsAutoResumable`, `TakesTheResume`, `AcceptsByteRanges`) how many bytes to keep. `WriteRetryLinesAsync` then calls `KeepOrThrowAwayAttemptBytesAsync`, which keeps the bytes (`DeferredOutputFileStream.KeepForRetry`) or cuts them back (`TruncateForRetry`) and writes the note under `-v` or a `--trace` option, the same gate as the other `Note:` lines. `DeferredOutputFileStream.AttemptBytesWritten` counts the attempt's bytes on a seekable file only, the way curl counts only a regular file.
- Choices: the "plain GET" test uses the attempt report's effective `Method` (so `-d`, `-T` and `-I` are excluded), and `-X` of any value excludes. A `206` keeps the bytes only when the attempt resumed from an offset above zero, and `Accept-Ranges` is matched without regard to case.
- Results: 13 new tests in `CurlCommandRunnerRetryTests` (2565 Console fast tests pass); `Measure-CodeQuality.ps1 -Library Curl.Console` gives 100% line and branch coverage, 0 failing members, worst CRAP 10.

## Log

- 2026-10-03: Created.
- 2026-10-03: Backlog -> Doing.
- 2026-10-03: Doing -> Done. A retried -C - download keeps its bytes and resumes when the server takes ranges, with curl's Keeping/Throwing away notes under -v
