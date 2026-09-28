---
id: BL-411
title: Interleave the progress meter with -v lines as curl does
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-478]
touches: [Curl.Console, Curl.Console.UnitTests, Documentation/Planning/Decisions]
requirement: none
created: 2026-09-27
completed: 2026-09-27
---
# BL-411 — Interleave the progress meter with -v lines as curl does

## Goal

Without `-s`, `-v` lines and the progress meter appear on standard error in the order curl 8.21.0 writes them.

## Context

- `Curl.Console` writes the meter after the transfer (BL-131), so under `-v` every `-v` line comes first and the meter after. curl writes the meter as the transfer goes.
- Measured 2026-09-27 on curl 8.21.0 (mingw, Schannel): `Record-CurlExchange.ps1 -Port 18421 -Response 'HTTP/1.1 200 OK\r\nContent-Type: text/plain\r\nContent-Length: 6\r\n\r\nhello\n' -CurlArgs @('-v','http://127.0.0.1:18421/f.txt','-o','o1')` wrote `Trying` and `Established connection`, then the two meter header lines and the zero status line with no line end, then `* using HTTP/1.x` straight after it, the request and response lines and `{ [6 bytes data]`, then three `\r100 ...` status lines and a line feed, then `* Connection #0 to host 127.0.0.1:18421 left intact`.

## Acceptance criteria

- [x] Over a scripted handler, `-v` without `-s` writes standard error byte for byte as measured, meter and `-v` lines interleaved.
- [x] `dotnet build -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes and no new test needs `TestCategory=Integration`; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Console`.

## Notes

- Filed from BL-242 (2026-09-27), which wired `-v` and `--trace` in `Curl.Console`.
- 2026-09-27 (lane 1): re-measured with `Record-CurlExchange.ps1` (887 bytes, as Context says). A test driving `CurlCommandRunner` with `-v`, no `-s`, over a scripted handler that reports `ReportTransferStarted` after the connection opens showed the start already matches: the live meter (BL-383) writes the header lines and zero status line before `* using HTTP/1.x`. Only the end differs: `Curl.Console` draws the done status lines and newline after the handler returns, and the HTTP handler reports `* Connection #0 ... left intact` before it returns, so that line comes before the meter instead of after it.
- Fixing the end needs the handler to say the transfer is done before it reports the connection end; `Curl.Console` cannot know it otherwise without matching `-v` line text, which would break on `closing`/`shutting down` and on other protocols. That is a contract change in `Curl.Protocol.Abstractions.UnitLibrary` (held by BL-474) and `Curl.Protocol.Http.UnitLibrary` (held by BL-471), filed as BL-478. Back to Backlog depending on it.
- Console side once BL-478 is Done: `TransferProgressRecorder` gets a `ReportTransferDone` that calls `Finish(succeeded: true)` and live-writes the lines plus the newline; `CurlCommandRunner.WriteProgressAsync` then writes only what is left (no second `Finish`, no second newline). The test `CurlCommandRunnerVerboseProgressMeterTests.RunAsync_VerboseWithoutSilent_InterleavesTheMeterWithTheVerboseLinesAsMeasured` (left uncommitted by lane 1, so the shift stashed it) pins the measured bytes, with the scripted handler calling `ReportTransferDone` before the `left intact` line; the three done lines read 200, 200, 199 in the measurement, so the test clock must step between the last two draws.
- 2026-09-27 (lane 2, BL-411 run): lane 1's stashed test was not reachable from this lane, so the test was rewritten. Delivered in-session rather than through the full `/feature` agent chain: a Console-only change whose seam BL-478 already made.
- Decision (ADR-0116, decided by Claude under Stewart's delegation): drawing the done lines at `ReportTransferDone` would guess the outcome, and ADR-0111 reports done for failed exchanges too (curl then draws one update, not three). Instead the runner writes `-v`/trace output through a new `HoldableStream` over standard error; `TransferProgressRecorder.ReportTransferDone` holds it (only while the meter is written live and the transfer has started), and `WriteProgressAsync` releases it after writing the meter's end for the real result. A repeated `ReportTransferStarted` (next `-L` hop) releases it before the redirect draws, so redirect bytes are unchanged.
- Re-measured 2026-09-27 with `Record-CurlExchange.ps1`: success, 887 bytes as Context says (speeds 232/231/230, so the done lines differ only in the speed column; the test pins the manual clock's 150 three times). Failure with `Content-Length: 10` over six bytes (exit 18): `{ [6 bytes data]`, `* end of response with 4 bytes missing`, the meter's line feed alone, `* closing connection #0`, `curl: (18) ...`; pinned too.
- `Documentation/Planning/Decisions` added to `touches` for ADR-0116; no task in Doing names it.
- Tests: `CurlCommandRunnerVerboseProgressMeterTests` (success as measured, failure as measured, `-s -v` unchanged), `HoldableStreamTests`, five `TransferProgressRecorderTests.ReportTransferDone_*`/`ReportTransferStarted_NextHopAfterDone_*`. Fast tests all green (Curl.Console.UnitTests 1014 passed); Measure-CodeQuality: Curl.Console 100% line, 100% branch, 0 failing, worst CRAP 10.

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Backlog. Waits on BL-478: the HTTP handler must report the transfer done before its connection-end -v line
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. -v without -s writes the progress meter's end before the connection-end line, as curl 8.21.0 does
