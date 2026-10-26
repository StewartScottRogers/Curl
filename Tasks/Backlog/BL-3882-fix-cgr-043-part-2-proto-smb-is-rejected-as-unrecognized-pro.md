---
id: BL-3882
title: Fix CGR-043 part 2: --proto =smb is rejected as unrecognized protocol (exit 2)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-3881]
touches: [Curl.Cli.UnitLibrary/CommandLineProtocolSet.cs, Curl.Cli.UnitTests/Cgr043SmbProtoTests.cs]
requirement: none
created: 2026-10-26
completed:
---
# BL-3882 — Fix CGR-043 part 2: --proto =smb is rejected as unrecognized protocol (exit 2)

## Goal

On Windows `curl --proto =smb -sS smb://127.0.0.1:20002/share/x` behaves as curl 8.22.0 Schannel does: stderr `curl: unrecognized protocol 'smb'` and `option --proto: is badly used here`, exit 2.

## Context

Part 2 of 3 split from BL-3572 (CGR-043 of `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md`). Today Curl accepts `--proto =smb` and tries to connect (exit 7). Start at `Curl.Cli.UnitLibrary/CommandLineProtocolSet.cs`, which part 1 (BL-3881) also changes, so it runs after it. Measure real curl before pinning text.

## Acceptance criteria

- [ ] `curl --proto =smb -sS smb://127.0.0.1:20002/share/x` through real curl and through Curl gives the same exit code (2), stdout bytes, stderr text and no request bytes on the wire; commands and both results are recorded in Notes.
- [ ] A focused unit test in `Curl.Cli.UnitTests/Cgr043SmbProtoTests.cs` fails before the fix and passes after it (ADR-0482).
- [ ] Repro variants that already matched curl still match.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Split from BL-3572, which went over its cost cap.

## Log

- 2026-10-26: Created.
