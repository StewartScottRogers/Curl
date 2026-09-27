---
id: BL-242
title: Wire -v and --trace output in Curl.Console
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-228, BL-229, BL-195, BL-173, BL-231]
touches: [Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-26
completed: 2026-09-27
---
# BL-242 — Wire -v and --trace output in Curl.Console

## Goal

`-v`, `--trace`, `--trace-ascii`, `--trace-time` and `--stderr` route BL-163 events through BL-228/BL-229 formatters to the right stream.

## Context

- Filed from the Phase 1 HTTP plan (protocol-architect, 2026-09-26), item W13. Upstream references: https://curl.se/docs/manpage.html and https://curl.se/libcurl/c/libcurl-errors.html; behaviour measured on curl 8.21.0.
- BL-231 added as a dependency beyond the plan.
- Where a criterion says *measured*, run curl 8.21.0 - the mingw build `/mingw64/bin/curl`, first on PATH, which is the Windows reference (ADR-0009 and the BL-153 ADR) - against a loopback server (the BL-165 recording script `Record-CurlExchange.ps1` once it exists), record the exact command and the bytes it produced in `Notes`, then pin them in a test. Never pin text that was not measured.

## Acceptance criteria

- [x] `-v` over a fake HTTP exchange writes the measured lines to stderr; `--trace file` writes the measured dump.
- [x] `dotnet build Curl.Console -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes and no new test needs `TestCategory=Integration`; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Console`.

## Notes

- Plan item: W13 in the Phase 1 HTTP plan (2026-09-26); plan keys in this file were replaced by their task IDs.
- Delivered in-session rather than through the full `/feature` stages: ADR-0046 fixes the design and BL-228/BL-229 own the wording, so this task only routes. New `Curl.Console/TransferEventOutput.cs` opens the run's sink from `CommandLineOptions.Trace`, `TraceFile` and `TraceTime`; `CurlCommandRunner` opens it once the first URL has parsed as a glob, passes `Events` to every context through the new `events` parameter of `TransferContextFactory.Create`, and closes it after the last transfer. Tests: `Curl.Console.UnitTests/CurlCommandRunnerTransferEventTests.cs` (11).
- Measured curl 8.21.0 (mingw, Schannel) on 2026-09-27 with `Record-CurlExchange.ps1 -Response 'HTTP/1.1 200 OK\r\nContent-Type: text/plain\r\nContent-Length: 6\r\n\r\nhello\n'`:
  - `-CurlArgs @('-s','-v','http://127.0.0.1:18441/f.txt','-o',...)`: standard error, pinned verbatim in `RunAsync_VerboseToOutputFile_WritesTheMeasuredLinesToStandardError` (local port 55116). Info lines end CR LF and header lines CR CR LF.
  - `-v` with the body on standard output (not a terminal): the `{ [6 bytes data]` line is still printed (port 18423).
  - `--trace <file>` (port 18422, local 51405): the file, pinned as `MeasuredHexDump`; CR LF line ends; nothing on standard error with `-s`.
  - `-s --trace-ascii - --trace-time` (port 18442): the stamped dump on standard output, CR LF, pinned in `RunAsync_TraceAsciiToStandardOutputWithTraceTime_WritesTheMeasuredStampedDump` with the measured stamps from an injected clock.
  - `--trace-ascii %`: the dump goes to standard error. `--trace <dir that does not exist>/t.txt`: the dump goes to standard error, with no warning, exit 0.
  - Two URLs with one `--trace` file: both transfers' dumps in the one file, opened once. `--trace t.txt http://[bad` (a glob error, exit 3): no trace file made. A `file://` transfer and a `file://` open failure both wrote their events into the file, so curl opens it at the first event.
  - Without `-s`, curl interleaves the meter with the `-v` lines as the transfer goes; `Curl.Console` writes the meter after the transfer, so the tests pin `-s -v` and BL-405 takes the interleaving.
- Decisions (sensible defaults; ADR-0046 already fixes the design, so no new ADR):
  - The trace file is opened once the first command-line URL parses as a glob, not at the first event: `ITransferEvents` members are synchronous and `IFileSystem` opens asynchronously, and the runner emits none of curl's pre-connect info lines (`URL rejected` and the like). The measured glob-error case makes no file, as curl does.
  - `[N bytes data]` lines are shown unless standard output is a terminal, the rule BL-228 took from `tool_debug_cb`; `-v` always writes to standard error until `--stderr` is wired.
  - `--trace -` writes through the runner's standard output stream in text mode (CR LF on Windows), as measured with the body in a file. With the body on standard output as well, curl switches that stream to binary once the body is written; not measured, left as text mode.
- Scope: the fake exchange is a scripted handler reporting the measured events, because the real HTTP handler and TCP connector do not report them yet. `--stderr` is in the goal but not the criteria and reroutes every standard error write; it is its own task. Follow-ups filed: BL-401 (HTTP handler reports header and data events), BL-402 (TCP connector reports `Trying`, connection opened and connect failures), BL-403 (`RedirectFollower.NextHop` copies `Events`; `Curl.Core` is in BL-359's `touches`), BL-404 (`--stderr`), BL-405 (meter interleaved with `-v`), BL-406 (`-v --trace-time` in the console, after BL-358).
- Verified: `dotnet build -warnaserror` clean; fast tests green solution-wide (Curl.Console.UnitTests 803 passed); `dotnet format --verify-no-changes` clean for both projects; `Measure-CodeQuality.ps1 -Library Curl.Console -IncludeIntegration` 100% line, 100% branch, 0 failing members, worst CRAP 10. Without `-IncludeIntegration` the only failing member is `DiskWriteOutFileOpener.TryOpen`, which is covered only by its Integration tests by design (BL-280) and is unchanged here.

## Log

- 2026-09-26: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. -v writes curl 8.21.0's lines to standard error and --trace/--trace-ascii (with --trace-time) write the measured dump to a file, standard output or standard error, from every transfer's events
