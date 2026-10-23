---
id: BL-3274
title: Make the differential host fail loudly on a dead server, a hung stdin write and leftover files
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Conformance.Differential.IntegrationTests/Host]
requirement: none
created: 2026-10-23
completed:
---
# BL-3274 — Make the differential host fail loudly on a dead server, a hung stdin write and leftover files

## Goal

The differential host (`Curl.Conformance.Differential.IntegrationTests/Host/`) never reports two binaries as matching because its own server died, never hangs past a case's timeout, and never lets one case see another case's files.

## Context

Found by the shift-end review (ADR-0710) of 6b344d38b...af2f6a9bd, silent-failure-hunter and type-design-analyzer.

1. `DifferentialLoopbackServer.cs` accept loop (around line 62-67) returns quietly on a `SocketException` even when not stopping. The routes port then stops accepting while the control port still answers, so every later case fails with exit 7 in both Curl and curl and compares equal. `ServeAsync` is fire-and-forget (`_ = ServeAsync(...)`, line 70) and swallows only IO/cancel/socket/dispose exceptions; any other responder exception becomes an unobserved task exception and looks like a connection reset. `DisposeAsync` does not wait for in-flight `ServeAsync` tasks.
2. `DifferentialProcessRunner.cs` `RunAsync` (around line 35-36) awaits `WriteStandardInputAsync` before `WaitForExitAsync` arms the case's timeout. The new `{"repeat": ..., "count": N}` byte form (ADR-0785) makes multi-megabyte stdin normal; a binary that neither reads stdin nor exits blocks the write forever. The stdin catch also takes every `IOException` and skips closing stdin on failure.
3. `WorkingDirectoryOf` (line 19) maps names through `\W` → `_`, so `w-size_upload` and `w-size-upload` share a directory, and repeated names (`w-stderr`, `w-onerror` in `CliHttpCases.json`) reuse one; `Directory.CreateDirectory` does not clear it, and `CollectFiles` then reports the earlier case's output as this case's.

## Acceptance criteria

- [ ] The accept loop stops quietly only when the server is stopping; any other accept or responder exception is recorded and fails the run (for example thrown from `DisposeAsync` or before the next case), and `DisposeAsync` awaits in-flight connections.
- [ ] The case's timeout covers the stdin write (its token is passed to the write), and a hung write ends as a timeout, not a hang.
- [ ] Each case runs in a directory no other case uses and that starts empty.
- [ ] Unit tests (in `Curl.Conformance.Differential.UnitTests` where the code is pure) or Integration tests in this project pin each of the three; `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-10-23: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: 065bc64c f42bb3e7 dc4dec2e fc7e5cc3.
