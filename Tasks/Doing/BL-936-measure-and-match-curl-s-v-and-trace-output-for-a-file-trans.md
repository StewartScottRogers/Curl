---
id: BL-936
title: Measure and match curl's -v and --trace output for a file:// transfer
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.File.UnitLibrary, Curl.Protocol.File.UnitTests, Curl.Console.UnitTests, Documentation/Planning/Decisions]
requirement: none
created: 2026-09-29
completed:
---
# BL-936 — Measure and match curl's -v and --trace output for a file:// transfer

## Goal

A `file://` transfer's `-v`, `--trace` and `--trace-ascii` output is measured against curl 8.21.0 and matched byte for byte, for a download, a `-T` upload, `-I`, and a missing file.

## Context

- Audit 2026-09-29, part B: `FileProtocolHandler.cs` reports only the header-write failure line (line ~741) through `context.Events`; no task on the board or in the archive records curl's `-v` or `--trace` output for `file://` as measured, so whether curl writes further info lines or data blocks for it is unverified.
- Where: `Curl.Protocol.File.UnitLibrary/FileProtocolHandler.cs` and `FileTransferMessages` (keep measured texts there).
- Measure first with `Record-CurlExchange.ps1 -NoServer` (real curl, no server): `-v`, `--trace-ascii -` and `--trace -` for `file:///<tempdir>/a.txt` download, `-T a.txt file:///<tempdir>/b.txt`, `-I file:///<tempdir>/a.txt`, and a missing file (exit 37). Copy the output into Notes with curl's version and build. If curl writes nothing beyond what Curl already writes, the task's result is tests pinning that, not new code.
- Tests must pass on Linux and macOS: use drive-less `file:///dir/x` URLs over the injected `IFileSystem`, never a drive letter (root `CLAUDE.md`).

## Acceptance criteria

- [x] Measured output for the four cases is copied into Notes.
- [ ] `Curl.Protocol.File.UnitTests` pin, through a recording `ITransferEvents`, exactly the measured events for the four cases (including, where curl writes none, that none are reported).
- [ ] Existing tests pass unmodified.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.
- [ ] `Measure-CodeQuality.ps1 -Library Curl.Protocol.File.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

### Measured (2026-09-29, lane 2)

curl 8.21.0 (x86_64-w64-mingw32) libcurl/8.21.0 Schannel, Git for Windows' `mingw64\bin\curl.exe`, through `Record-CurlExchange.ps1 -NoServer`, with `C:\Temp\bl936\a.txt` = `hello\n` (6 bytes). (A temp dir under a path with a space, `C:\Users\Stewart Rogers\...`, makes curl exit 3: measure from a space-free directory.)

- Download, `--trace-ascii - file:///C:/Temp/bl936/a.txt`, stdout:
  ```
  <= Recv data, 6 bytes (0x6)
  0000: hello.
  hello
  * shutting down connection #0
  ```
  `--trace -` is the same with `0000: 68 65 6c 6c 6f 0a                               hello.`; `-v` writes `{ [6 bytes data]` after the meter, then the meter's line end, then `* shutting down connection #0`.
- Upload, `-T a.txt file:///C:/Temp/bl936/b.txt`: `* shutting down connection #0` only - no `=> Send data`. `-v`: the meter, its line end, then that line.
- `-I file:///C:/Temp/bl936/a.txt`: the pseudo-headers to stdout, then `* shutting down connection #0` - no `<= Recv header` in the trace.
- Missing file (exit 37): `* Could not open file C:/Temp/bl936/nope.txt` then `curl: (37) Could not open file C:/Temp/bl936/nope.txt` - no connection line, no meter.
- More measured cases, all `--trace-ascii -`: `--max-filesize 3` traces all 6 bytes, writes `hel`, `* Exceeded the maximum allowed file size (3) with 3 bytes`, `* shutting down connection #0`; `-o <missing dir>/x` traces the 6 bytes, `* client returned ERROR on write of 6 bytes`, `* closing connection #0`; `-C 100` `* failed to resume file:// transfer`, `* shutting down connection #0`; unmet `-z` and an empty file: `* shutting down connection #0` only; `-T` to a missing dir `* cannot open C:\Temp\bl936\nodir\b.txt for writing`, `* closing connection #0`; `-T` a source that ran short `* client read function EOF fail, only 0/6 of needed bytes read`, `* closing connection #0`; `-T` into a byte-range-locked destination (exit 55): no line of its own, `* shutting down connection #0`; `-v -D - -o body.txt` with stdout closed: `curl: Failed writing headers to -`, `* client returned ERROR on write of 19 bytes`, the meter's line end, `* closing connection #0`, `curl: (23) ...`.
- `curl -v a.txt a.txt nope.txt a.txt`: `#0`, `#1`, the failure with no number, `#2`.
- A 1000000-byte file traces 102399-byte `<= Recv data` blocks (last 78409) - filed as BL-976.

### Design (implemented, uncommitted in the lane-2 stash)

Decided by Claude under Stewart's delegation (ADR still to write under `Documentation/Planning/Decisions`, next free number):
- `FileProtocolHandler` reports each chunk read as `ReportDataReceived` before writing it (download only; `CopyAsync` gained a `reportChunkRead` action, the upload passes a no-op).
- Every failure's message goes out as `ReportInfo` (`ReportFailure`), except exit 55, whose text is only `curl_easy_strerror`'s; the header-write failure no longer reports its own line.
- A transfer past its open (download after a successful open; an upload always, since curl opens the destination in the transfer phase) takes a connection number from the handler's own counter (`lastConnectionNumber`, `Interlocked.Increment`) and ends with `ReportConnectionEnd`: the failure line, `Progress.ReportTransferDone()` (so `-v`'s meter ends its line first), then `closing connection #N` for exit 23 and 26 (libcurl's `multi_done` counts those premature) or `shutting down connection #N` otherwise. `FileTransferMessages.ShuttingDownConnection`/`ClosingConnection` hold the texts. Mixed-scheme numbering is BL-977.
- Tests: new `FileProtocolHandlerTransferEventTests` (17 tests); `RecordingTransferEvents` records an ordered `Transcript`; `RecordingTransferProgress` gained `ReportTransferDone` and an optional `Transcript`. File tests 330/330 pass; the 12 `-v`/`--trace`/`--trace-ascii` recordings match `dotnet run --project Curl.Console` byte for byte (Last-Modified aside).
- Two existing tests pinned what the measurement shows was incomplete, so "existing tests pass unmodified" cannot hold for them: `FileProtocolHandlerDecisionTests.ExecuteAsync_HeaderOutputFails_ReportsTheWriteErrorAsAnInformationLine` (updated in the stash to expect `closing connection #0` after the failure) and `Curl.Console.UnitTests/CurlCommandRunnerDumpHeaderTests.RunAsync_DumpHeaderToFailingStandardOutputUnderVerbose_PrintsFailedWritingHeadersBeforeTheVerboseLine`, which needs `"* closing connection #0" + "\n"` after the `* client returned ERROR on write of 20 bytes` line (measured above). That is the only failing test in the fast suite.
- Remaining on resume: update that Console test, write the ADR, run `Measure-CodeQuality.ps1 -Library Curl.Protocol.File.UnitLibrary`, build, fast tests, commit.

### Board

- Needed `Curl.Console.UnitTests` (the Console test above) and `Documentation/Planning/Decisions` (the ADR), added to `touches`. `Curl.Console.UnitTests` is in BL-650's `touches` (in Doing), so this task goes back to Backlog until BL-650 is done.

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Backlog. Needs Curl.Console.UnitTests (one -v -D test must expect the measured '* closing connection #0'), which BL-650 in Doing touches; code is done in the lane stash, resume after BL-650
- 2026-10-01: Backlog -> Doing.
