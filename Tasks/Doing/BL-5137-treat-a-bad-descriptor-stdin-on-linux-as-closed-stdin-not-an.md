---
id: BL-5137
title: Treat a bad-descriptor stdin on Linux as closed stdin, not an unhandled exception
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Core.UnitLibrary/ClosedStandardInputStream.cs, Curl.Core.UnitTests/ClosedStandardInputStreamBadDescriptorTests.cs]
requirement: none
created: 2026-11-13
completed:
---
# BL-5137 — Treat a bad-descriptor stdin on Linux as closed stdin, not an unhandled exception

## Goal

With stdin closed on Linux, `-d @-`, `-T -`, `-K -`, `-F f=@-` and `file:///dev/stdin` fail the way the port's ADR-0949 specifies (exit 26 with `curl: option -d: error encountered when reading a file`), not with an unhandled .NET exception and SIGABRT.

## Context

From the outside fitness review of 2026-11-13 (commit 56cb97ede, `preview` build), finding STD-060 (Low, Confirmed for 5 variants). Area: CLI stdin handling, Linux build.

**Repro (Linux).** `curl -sS -d @- http://127.0.0.1:25080/ <&-; echo $?`

**Expected.** The port's ADR-0949: exit 26 with `curl: option -d: error encountered when reading a file`; the Windows build does exactly that. (Upstream's curl on Linux blocks instead, because its `main_checkfds` puts a pipe on fd 0; it does not crash.)

**Actual (Linux).** Exit 134 (SIGABRT); stderr `Unhandled exception. System.UnauthorizedAccessException: Access to the path is denied. ---> System.IO.IOException: Bad file descriptor` with a stack trace.

**Where to start.** `Curl.Core.UnitLibrary/ClosedStandardInputStream.cs`, `InPlaceOfNull` (called from `Curl.Console/Program.cs`). On Linux `Console.OpenStandardInput()` returns a stream on the closed fd 0 that throws EBADF, not `Stream.Null`. Probe fd 0 (`fcntl(F_GETFD)`) or catch EBADF and use the closed-stdin path.

## Acceptance criteria

- [ ] New tests in `Curl.Core.UnitTests/ClosedStandardInputStreamBadDescriptorTests.cs` prove that a stdin stream whose reads throw an `IOException` for a bad file descriptor (EBADF, simulated with a stand-in stream) is treated as closed stdin, the same as `Stream.Null`, and that other IO failures are not swallowed.
- [ ] A test proves the stream `InPlaceOfNull` returns for a bad-descriptor stdin behaves as the existing closed-stdin stream does, so the existing `-d @-`, `-T -`, `-K -`, `-F f=@-` and `file:///dev/stdin` closed-stdin handling (ADR-0949, exit 26 with the option-specific message) applies unchanged; the Log records how each of the five variants was checked by hand.
- [ ] Tests are platform-neutral (a stand-in stream, not a real fd).
- [ ] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Mock servers must be PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md).
- Review acceptance check, as a hand check on Linux: the command exits 26 with the curl message and stderr has no `Unhandled exception`; the same for `-T -`, `-K -`, `-F f=@-` and `file:///dev/stdin`.

## Log

- 2026-11-13: Created.
- 2026-11-13: Backlog -> Doing. Criteria at claim: 3226be2a 7c68ebbf b4616601 a0b49a29.
