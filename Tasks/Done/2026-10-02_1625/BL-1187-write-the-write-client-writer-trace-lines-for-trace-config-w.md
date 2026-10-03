---
id: BL-1187
title: Write the [WRITE] client writer trace lines for --trace-config write and -vvv
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-1159]
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-10-02
completed: 2026-10-02
---
# BL-1187 — Write the [WRITE] client writer trace lines for --trace-config write and -vvv

## Goal

Curl writes curl 8.21.0's `[WRITE]` client writer lines under `--trace-config write`, `all`, `-vvv` and `-vvvv`.

## Context

- Split from BL-1159 (ADR-0357 amendment); `-vvv` already puts `write` in `CommandLineOptions.TraceComponents`. BL-1103 Notes hold the measured lines: after each `< ` header line `[WRITE] [OUT] wrote 17 header bytes -> 17`, `[WRITE] [PAUSE] writing 17/17 bytes of type c -> 0`, `[WRITE] download_write header(type=c, blen=17) -> 0`, `[WRITE] client_write(type=c, len=17) -> 0` (later headers type 4, preceded by `[WRITE] header_collect pushed(type=1, len=19) -> 0`); after the body `[OUT] wrote 2 body bytes -> 2`, type 1, `xfer_write_resp(len=40, eos=0) -> 0`, then `[WRITE] [OUT] done`. Re-measure with `Record-CurlExchange.ps1` before pinning. Written where the HTTP handler hands header and body bytes on.

## Acceptance criteria

- [x] Tests pin the `[WRITE]` lines of a plain HTTP 200 with a two-byte body, and that none appears without `write` or `all`; an ADR-0357 amendment records how `xfer_write_resp`'s length is produced.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage for each library changed.

## Notes

- Measured 2026-10-02, curl 8.21.0 (mingw, Schannel), `Record-CurlExchange.ps1` on 127.0.0.1:47813, fixtures in `%TEMP%\bl1187\<case>`:
  - `plain` (`200`, `Content-Length: 2`, `hi`, `-s -v --trace-config write`): the lines in Context, `xfer_write_resp(len=40, eos=0)`, then `[WRITE] [OUT] done` before `Connection #0 ... left intact`.
  - `two` (an extra `X-A: b` header): its own `header_collect pushed(type=1, len=8)` and type-4 lines; `xfer_write_resp(len=48)`, the head plus body bytes of the one read.
  - `empty` (`Content-Length: 0`): after the blank line's lines, `xfer_write_resp(len=38, eos=0)` then `[OUT] done`; no body lines.
  - `rw` (`read,write`): `[WRITE] [OUT] done` comes before `[READ] client_reset, clear readers`. `nov` (no `-v`): nothing.
- Delivered in `Curl.Console` only (no change to `Curl.Protocol.Http` was needed): `ClientWriterTraceEvents` wraps the transfer's events outermost in `CurlCommandRunner.WithTraceLineEvents` under the new `CurlComposition.TracesWrite` (`write` or `all`). How `xfer_write_resp`'s length is produced: ADR-0357 amendment (BL-1187).
- Tests: `ClientWriterTraceEventsTests`; `CurlCommandRunnerTransferEventTests.RunAsync_TraceConfigWrite_*`, `RunAsync_TraceConfigWithoutWrite_WritesNoWriteLine` (`network`, `read`, `-write`), `RunAsync_TraceConfigWriteWithoutVerbose_WritesNothing`, `RunAsync_ThreeOrMoreVs_WriteTheClientWriterDoneLine`. `Measure-CodeQuality.ps1 -Library Curl.Console`: 100% line and branch; its one failing member, `CurlCommandRunner.TransferUrlAsync` (complexity 12 by the script's count, CA1502 passes the build), is unchanged by this task.

## Log

- 2026-10-02: Created.
- 2026-10-02: Backlog -> Doing.
- 2026-10-02: Doing -> Done. --trace-config write, all, -vvv and -vvvv write curl's [WRITE] client writer lines after each response header and body block and [OUT] done
