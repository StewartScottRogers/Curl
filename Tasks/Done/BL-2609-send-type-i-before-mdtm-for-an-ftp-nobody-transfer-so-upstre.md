---
id: BL-2609
title: Send TYPE I before MDTM for an FTP NOBODY transfer so upstream case 542 passes
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Protocol.Ftp.UnitLibrary, Curl.Protocol.Ftp.UnitTests, Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-17
completed: 2026-10-18
---
# BL-2609 — Send TYPE I before MDTM for an FTP NOBODY transfer so upstream case 542 passes

## Goal

Upstream case 542 passes through the runner and is on the ratchet.

## Context

Found by BL-2530 (lib542: FTP with `CURLOPT_NOBODY` 1 and `CURLOPT_HEADER` 0). `<verify><protocol> differs at byte 43 (line 4): expected "TYPE I", got "MDTM 542"` - curl 8.22.0 sends `TYPE I` before `MDTM` and `SIZE` for a NOBODY FTP transfer (see `lib/ftp.c` `ftp_state_type`/`ftp_state_mdtm`). The fix is likely in `Curl.Protocol.Ftp.UnitLibrary`, not Libcurl; correct the `touches` before the first edit.

## Acceptance criteria

- [x] Case 542 passes through the runner and is added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`.
- [x] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.
- [x] A focused unit test in the fixed library's own `.UnitTests` project covers the fix (ADR-0482), and the cases' output does not depend on the platform.

## Notes

The fix was in `FtpSession.AsksForModificationTime` (Curl.Protocol.Ftp.UnitLibrary), not Libcurl: `NoBody` alone no longer sends `MDTM`; `RemoteTime` (libcurl `CURLOPT_FILETIME`, which the command line sets for `-I` through `EasyOptionsFromCommandLine`) and `-z` still do. `touches` corrected accordingly. Measure-CodeQuality.ps1 not run: the change removes one `||` operand from a one-line property (complexity and branches only fall; both remaining operands stay covered).

## Log

- 2026-10-17: Created.
- 2026-10-17: Backlog -> Doing. Criteria at claim: 42d45351 4272a6b9 7d0e6df3.
- 2026-10-18: Doing -> Done. Case 542 passes: NOBODY alone no longer sends MDTM
