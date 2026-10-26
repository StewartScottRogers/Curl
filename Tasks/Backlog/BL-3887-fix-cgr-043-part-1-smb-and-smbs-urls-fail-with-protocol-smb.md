---
id: BL-3887
title: Fix CGR-043 part 1: smb:// and smbs:// URLs fail with Protocol "smb" is disabled (exit 1)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary/CommandLineProtocolSet.cs, Curl.Cli.UnitTests/Cgr043SmbUrlTests.cs]
requirement: none
created: 2026-10-26
completed:
---
# BL-3887 — Fix CGR-043 part 1: smb:// and smbs:// URLs fail with Protocol "smb" is disabled (exit 1)

## Goal

On Windows `curl -sS smb://127.0.0.1:20002/share/x` (and the `smbs://` form) behaves as curl 8.22.0 Schannel does: stderr `curl: (1) Protocol "smb" is disabled`, exit 1, no connection attempted, since smb and smbs are not in the `-V` protocol list.

## Context

Part 1 of 3 split from BL-3572 (CGR-043 of `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md`, related GF-0037, GF-0091). Today Curl tries to connect and gives exit 7. Start at `Curl.Cli.UnitLibrary/CommandLineProtocolSet.cs` and the SMB protocol registration. Rebuild the repro with `Record-CurlExchange.ps1` (no Python); measure real curl before pinning text. Parts 2 and 3 (BL-3888, BL-3889) cover `--proto =smb` and `--proto-default smb`.

## Acceptance criteria

- [ ] `curl -sS smb://127.0.0.1:20002/share/x` through real curl and through Curl gives the same exit code (1), stdout bytes, stderr text and no request bytes on the wire; commands and both results are recorded in Notes.
- [ ] The `smbs://` form is measured the same way and matches; the result is recorded in Notes.
- [ ] A focused unit test in `Curl.Cli.UnitTests/Cgr043SmbUrlTests.cs` fails before the fix and passes after it (ADR-0482), and is platform-neutral or marked for Windows.
- [ ] Repro variants that already matched curl still match.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Split from BL-3572, which went over its cost cap.
- Continues the stashed work of BL-3572.

## Log

- 2026-10-26: Created.
