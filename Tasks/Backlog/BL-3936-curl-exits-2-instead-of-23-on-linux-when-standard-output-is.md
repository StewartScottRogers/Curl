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
completed:
---
# BL-3936 — Curl exits 2 instead of 23 on Linux when standard output is closed (1>&-)

## Goal

On Linux, Curl run with standard output closed (`1>&-`) writes curl's error and exits 23, as curl does, instead of `curl: (2) Access to the path is denied.` and exit 2.

## Context

Found by BL-3824 on 2026-10-28 in WSL (Ubuntu): `curl -sS file:///etc/hostname 1>&-; echo $?` prints `curl: Failed writing body` and exits 23 with curl 8.18.0 (OpenSSL); Curl (linux-x64 publish of `Curl.Console`) prints `curl: (2) Access to the path is denied.` and exits 2. curl's `main_checkfds` opens `/dev/null` on a closed descriptor 1 (read-only, hence the failed write). The .NET runtime may take descriptor 1 for its own pipe at start-up (ADR-0869, which fixed descriptor 2 with `NativeInheritedFileDescriptor.IsInherited`). Start in `Curl.Console/StandardOutputOpener.cs`: use `NativeInheritedFileDescriptor.IsInherited(1)` off Windows and give a stream whose writes fail as curl's `/dev/null` write does. Measure real curl's stderr bytes before pinning them.

## Acceptance criteria

- [ ] On Linux, `curl -sS file:///etc/hostname 1>&-; echo $?` gives the same standard error bytes and exit code through Curl as through real curl, with the commands and both results recorded in Notes.
- [ ] A focused unit test in `Curl.Console.UnitTests` fails before the fix and passes after it (ADR-0482).
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

## Log

- 2026-10-28: Created.
