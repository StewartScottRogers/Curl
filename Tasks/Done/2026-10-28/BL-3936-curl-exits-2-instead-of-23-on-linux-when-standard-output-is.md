---
id: BL-3936
title: Curl exits 2 instead of 23 on Linux when standard output is closed (1>&-)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console/StandardOutputOpener.cs, Curl.Console.UnitTests/StandardOutputOpenerTests.cs]
requirement: none
created: 2026-10-28
completed: 2026-10-28
---
# BL-3936 — Curl exits 2 instead of 23 on Linux when standard output is closed (1>&-)

## Goal

On Linux, Curl run with standard output closed (`1>&-`) writes curl's error and exits 23, as curl does, instead of `curl: (2) Access to the path is denied.` and exit 2.

## Context

Found by BL-3824 on 2026-10-28 in WSL (Ubuntu): `curl -sS file:///etc/hostname 1>&-; echo $?` prints `curl: Failed writing body` and exits 23 with curl 8.18.0 (OpenSSL); Curl (linux-x64 publish of `Curl.Console`) prints `curl: (2) Access to the path is denied.` and exits 2. curl's `main_checkfds` opens `/dev/null` on a closed descriptor 1 (read-only, hence the failed write). The .NET runtime may take descriptor 1 for its own pipe at start-up (ADR-0869, which fixed descriptor 2 with `NativeInheritedFileDescriptor.IsInherited`). Start in `Curl.Console/StandardOutputOpener.cs`: use `NativeInheritedFileDescriptor.IsInherited(1)` off Windows and give a stream whose writes fail as curl's `/dev/null` write does. Measure real curl's stderr bytes before pinning them.

## Acceptance criteria

- [x] On Linux, `curl -sS file:///etc/hostname 1>&-; echo $?` gives the same standard error bytes and exit code through Curl as through real curl, with the commands and both results recorded in Notes.
- [x] A focused unit test in `Curl.Console.UnitTests` fails before the fix and passes after it (ADR-0482).
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Pipeline `feature` run directly: a two-line change in one file, the same shape as BL-3824's fix, so no architect stage was needed.
- Decision (ADR-0870): off Windows `StandardOutputOpener.ReadStandardOutputHandleValue` answers `-1` (closed) when `NativeInheritedFileDescriptor.IsInherited(1)` is false, so standard output becomes the existing `ClosedStandardOutputStream` and the write fails as curl's write to its read-only `/dev/null` does.
- Measured in WSL Ubuntu with a self-contained linux-x64 publish (`dotnet publish Curl.Console -c Release -r linux-x64 --self-contained -p:PublishAot=false`), standard error to a file: `curl -sS file:///etc/hostname 1>&- 2>/tmp/r.txt; echo $?` gives `23` and `curl: Failed writing body\n` (curl 8.18.0, OpenSSL 3.5.5); Curl before the fix `curl: (2) Access to the path is denied.` and 2 (BL-3824's measurement), after the fix 23 and the same bytes (`cmp` equal). A pipe and `> file` still give the body and exit 0.
- Focused test: `StandardOutputOpenerTests.ReadStandardOutputHandleValue_ElsewhereNotInherited_OpensAStreamWhoseWritesFail` (all platforms; does not compile, so fails, before the fix).
- `dotnet build` clean; fast tests 46 projects passed, 0 failed. `Measure-CodeQuality.ps1 -Library Curl.Console`: 0 failing members. Platform-neutral: the Windows path is unchanged and the test fakes the descriptor check.

## Log

- 2026-10-28: Created.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 91513d3a def504f7 779790c0.
- 2026-10-28: Doing -> Done. On Linux and macOS, 1>&- gives curl's 'curl: Failed writing body' and exit 23 instead of exit 2
