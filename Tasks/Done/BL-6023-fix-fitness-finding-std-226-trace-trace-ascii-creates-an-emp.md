---
id: BL-6023
title: Fix fitness finding STD-226: --trace / --trace-ascii creates an empty trace file when the transfer fails at setup
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console/TransferEventOutput.cs, Curl.Console/TraceFileOpenedOnFirstWriteStream.cs, Curl.Console/CLAUDE.md, Curl.Console.UnitTests/TraceFileOpenedOnFirstWriteStreamTests.cs, Curl.Console.UnitTests/FormFileSizedBeforeTraceTests.cs]
requirement: none
created: 2026-11-26
completed: 2026-11-26
---
# BL-6023 — Fix fitness finding STD-226: --trace / --trace-ascii creates an empty trace file when the transfer fails at setup

## Goal

STD-226 from the outside fitness review of 2026-11-26 07:04 no longer reproduces: --trace / --trace-ascii creates an empty trace file when the transfer fails at setup. Curl behaves as the finding's acceptance check states.

## Context

From the outside fitness review `Curl-Fitness-Review-2026-11-26T0704` (commit `23b62c2`, the `preview` release binaries).

In the finding, .NET is this Curl, REF is curl 8.22.0 Schannel on Windows (Git for Windows `curl.exe`), REF2 is curl 8.18.0 OpenSSL on WSL Ubuntu. Measure real curl with `Record-CurlExchange.ps1` before pinning output text.

The reviewer used Python mocks and real servers in Docker (pinned images listed in the review workspace's TestServers.md); rebuild any mock you need in PowerShell or C# (never Python). Mock servers bind 127.0.0.1 and port numbers are examples. Check that it still reproduces on the current branch first.

The finding, verbatim:

- **Severity:** Low · **Confidence:** Confirmed (`-F` and `-T`) · **Area:** trace output, Windows build.
- **Repro** (any HTTP URL, e.g. a local server on `127.0.0.1:24000`, run in an empty directory): `curl -sS --trace tr.txt -F f=@nofile.txt http://127.0.0.1:24000/echo/; ls tr.txt` (also with `-T nofile.txt`).
- **Expected (REF):** exit 26 and no `tr.txt`.
- **Actual (.NET):** exit 26, and a 0-byte `tr.txt` is left.
- **Acceptance check:** after the repro, `ls tr.txt` reports "No such file".

## Acceptance criteria

- [x] after the repro, `ls tr.txt` reports "No such file".
- [x] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Reproduced before the fix: `-T nofile` left a 0-byte trace file. Cause: `TransferEventOutput`
  opened the file before the transfer's setup. It now opens it on the first trace write
  (`TraceFileOpenedOnFirstWriteStream`), as curl does. Decision and why: ADR-1425.
- Touches widened for the new stream, its tests, `FormFileSizedBeforeTraceTests` and
  `Curl.Console/CLAUDE.md`; no other task in Doing names them. `CurlCommandRunner.cs` (held by
  BL-6022) is not changed.
- A trace file that is also a `-T` value is still opened up front, so AF-0513's upload sizing
  holds without touching the runner.
- `FormFileSizedBeforeTraceTests`: its fake handlers now report a trace event, as a real one does
  before reading the body, and its fake file system empties a handle already open, as a real one does.
- Measured with `curl.exe` against curl 8.21.0 (Git for Windows): the repro (`-F` and `-T`) leaves
  no `tr.txt`; `-T f --trace-ascii f` and `-F f=@f --trace-ascii f` end with the same exit 26 and
  message as curl's. An unsupported scheme and an unresolved host still leave a trace file in both.
- Fast tests: the whole solution ran once; only `Curl.Console.UnitTests` failed (the tests above),
  and it passes after the fix. `Measure-CodeQuality.ps1 -Library Curl.Console`: 100% line and
  branch, 0 failing members.
- Platform-neutral: no path, OS error text or certificate in the tests; CI on Linux and macOS is
  left to the shift's watch.

## Log

- 2026-11-26: Created from fitness review 2026-11-26T0704.
- 2026-11-26: Backlog -> Doing. Criteria at claim: 0bcc2c9c 69116afe 3d836d5e.
- 2026-11-26: Doing -> Done. A run that fails before its first trace event now leaves no --trace file, and an existing one untouched (ADR-1425).
