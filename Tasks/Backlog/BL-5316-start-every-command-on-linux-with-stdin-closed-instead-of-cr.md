---
id: BL-5316
title: Start every command on Linux with stdin closed instead of crashing with exit 134
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console/Program.cs, Curl.Core.UnitLibrary/ClosedStandardInputStream.cs, Curl.Core.UnitTests/ClosedStandardInputStreamLazyOpenTests.cs]
requirement: none
created: 2026-11-16
completed:
---
# BL-5316 — Start every command on Linux with stdin closed instead of crashing with exit 134

## Goal

On Linux, every command run with stdin closed (`<&-`) behaves as it does with empty stdin (a plain GET prints the body and exits 0, `curl -V` prints the version, an option that reads stdin sees empty input or reports a read error) instead of crashing with exit 134 and an unhandled `System.UnauthorizedAccessException` / `Bad file descriptor`.

## Context

From the outside fitness review of 2026-11-16 (commit 64e9144, `preview` build), finding STD-060 (Medium, raised from Low: every command fails under a launcher that closes fd 0; Confirmed: 12 commands, twice). Area: CLI startup, Linux build. **Re-reported:** the earlier fix BL-5137 (`ClosedStandardInputStream.InPlaceOfNull` probes with a zero-length read and treats EBADF as closed stdin) was incomplete: the crash still happens, now on commands that never read stdin, so the stream is still opened or touched eagerly somewhere at startup.

**Repro (Linux).** `curl -sS http://127.0.0.1:35080/ <&-`; also `curl -V <&-`, `curl file:///etc/hostname <&-`, and `-d @-`, `-T -`, `-K -`, `-F f=@-`, `--data-binary @-`, `-H @-`, `--json @-`, `--url-query @-`, `--variable foo@-`, each with `<&-`.

**Expected (reference curl 8.18).** The plain GET prints the body and exits 0; `-V` prints the version; options that read stdin behave as with empty input or report a read error (never a crash). (Upstream's `main_checkfds` puts a pipe on fd 0 when it is closed, so reads see end of file or block, and nothing crashes.)

**Actual (port).** Each exits 134 with `Unhandled exception. System.UnauthorizedAccessException: Access to the path is denied. ---> System.IO.IOException: Bad file descriptor`.

**Where to start.** `Curl.Console/Program.cs` line 19: `Curl.Core.ClosedStandardInputStream.InPlaceOfNull(System.Console.OpenStandardInput())` runs before anything else, and `Console.OpenStandardInput()` or the probe inside `InPlaceOfNull` (`Curl.Core.UnitLibrary/ClosedStandardInputStream.cs`) still throws on Linux with a closed fd 0 (the zero-length read BL-5137 added may not fail with EBADF on the stream .NET returns, or `OpenStandardInput` itself may throw, or later code calls `Console.IsInputRedirected` / `Console.In`). Find the exact throwing frame from the stack trace in the review (reproduce with stdin closed on Linux if a Linux host is at hand; otherwise reason from the stack), and fix it: open stdin lazily (a stream that opens the real one on first read) and treat EBADF at any point, including the open itself, as an empty stream. Check the other startup reads of the console (`Console.IsInputRedirected`, `Console.In`, `Console.KeyAvailable`).

## Acceptance criteria

- [ ] New tests in `Curl.Core.UnitTests/ClosedStandardInputStreamLazyOpenTests.cs` (stand-in opener, platform-neutral) prove that a stdin opener which throws the Linux bad-descriptor failure (an `UnauthorizedAccessException` wrapping an `IOException` with errno 9) when called is not an error: the stream returned reads as closed stdin, and the opener is not invoked until the first read.
- [ ] A test proves a run that never reads stdin does not touch the opener at all, and one that does read it with a bad descriptor sees end of input.
- [ ] A test proves other IO failures are still not swallowed (as BL-5137's tests do).
- [ ] If `Curl.Console/Program.cs` changes shape (for example it now passes an opener, not a stream), a test in `Curl.Console.UnitTests` proves the startup path leaves stdin unopened for a command that does not read it.
- [ ] The Log records the review's hand check on Linux, or says it could not be run: `curl -sS http://127.0.0.1:35080/ <&-` prints the body and exits 0; `curl -V <&-` exits 0; `curl -sS -d @- http://127.0.0.1:35080/ <&-` sends an empty body and does not crash.
- [ ] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Related, already Done: BL-5137 (the first, incomplete fix; its tests in `Curl.Core.UnitTests/ClosedStandardInputStreamBadDescriptorTests.cs` must keep passing). ADR-0949 specifies the option-specific exit 26 behaviour when stdin is closed and an option needs it; do not change it.
- Mock servers must be PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md).

## Log

- 2026-11-16: Created.
