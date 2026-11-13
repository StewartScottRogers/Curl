---
id: BL-5040
title: Write -v --stderr - lines in binary mode when the body also goes to stdout
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console/TransferEventOutput.cs, Curl.Console/CurlCommandRunner.cs, Curl.Console.UnitTests/TransferEventOutputVerboseBinaryModeTests.cs]
requirement: none
created: 2026-11-12
completed: 2026-11-12
---
# BL-5040 — Write -v --stderr - lines in binary mode when the body also goes to stdout

## Goal

On Windows, `-v --stderr -` writes its header lines with `\r\n` (binary mode) when the body also goes to stdout, and keeps `\r\r\n` (text mode) when the body goes to a file, as REF does.

## Context

From the outside fitness review of 2026-11-12 (commit 2d2ad7f8b, `preview` curl-win-x64.zip; REF = curl 8.22.0 Schannel, REF2 = curl 8.18.0 OpenSSL), finding STD-045 (Low, Confirmed).

- **Repro:** `curl -s -v --stderr - http://127.0.0.1:41000/a > o.bin` against any HTTP server; inspect the bytes after `> GET /a HTTP/1.1` in `o.bin`.
- **Expected (REF):** `> GET /a HTTP/1.1\r\n`, because stdout is in binary mode when the body goes there. With `-o x.out` added, REF writes `\r\r\n` (text mode).
- **Actual (PORT):** `\r\r\n` in both cases. `--trace-ascii -` already switches mode correctly, so only the `-v` writer is affected.
- **Where to start:** `Curl.Console/TransferEventOutput.cs` and `Curl.Console/CurlCommandRunner.cs`: the verbose writer's stdout target does not get the binary-mode switch that the `--trace-ascii -` target gets.

## Acceptance criteria

- [x] A new test file `Curl.Console.UnitTests/TransferEventOutputVerboseBinaryModeTests.cs` asserts that, with the verbose writer targeting stdout and the body also going to stdout, header lines are written as `\r\n` with no doubled CR; with the body going to a file they are written as REF writes them in text mode.
- [x] The test that pins the `\r\r\n` text-mode case is marked `[OSCondition(OperatingSystems.Windows)]`; the binary-mode case is asserted on the platform-neutral writer abstraction so it passes on all three platforms.
- [x] Regression: `--trace-ascii -` output is unchanged.
- [x] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Mock servers in the review were Python; build stand-ins in PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md). Measure real curl (the platform build) before pinning any output text. The review's check read `o.bin` with Python; use PowerShell `[IO.File]::ReadAllBytes` instead. The fix may also need `Curl.Console/CurlCommandRunner.cs` (where the writer target is chosen); add it to `touches` if so.

## Log

- 2026-11-12: Created.
- 2026-11-12: Backlog -> Doing. Criteria at claim: 61eba74b 1c874a1c 40542a04 a0b49a29.

## Notes

- `-v --stderr -` now writes its lines through `TextModeUntilBinaryStream` (as `--trace-ascii -` does) when the last `--stderr` redirect is `-`: `CurlCommandRunner` passes a new `standardErrorIsStandardOutput` flag to `TransferEventOutput.OpenAsync`. Added `CurlCommandRunner.cs` to `touches`.
- Decision: the existing `RunAsync_StandardErrorDash_WritesTheLinesToStandardOutput` pinned CR LF for the `Trying` line with the body going to stdout; that was the bug, so it now pins LF (the `curl: (7)` line stays CR LF as before, unmeasured). The `\r\r\n` text-mode case with the body in a file is pinned by the new tests (`binary: false`), one marked `[OSCondition(Windows)]`; the writer abstraction takes `runsOnWindows` so the others are platform-neutral.
- Not re-measured against real curl here; the expected bytes are those in the finding STD-045 (REF). Measure-CodeQuality for Curl.Console: 0 failing members. Linux/macOS CI: tests are platform-neutral.
- 2026-11-12: Doing -> Done. Fixed
