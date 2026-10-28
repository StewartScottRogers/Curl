---
id: BL-3889
title: Fix CGR-043 part 3: --proto-default smb is rejected as unsupported protocol (exit 1)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-3888]
touches: [Curl.Cli.UnitLibrary/CommandLineProtocolSet.cs, Curl.Cli.UnitTests/Cgr043SmbProtoDefaultTests.cs]
requirement: none
created: 2026-10-26
completed:
---
# BL-3889 — Fix CGR-043 part 3: --proto-default smb is rejected as unsupported protocol (exit 1)

## Goal

On Windows `curl --proto-default smb -sS 127.0.0.1:20002/share/x` behaves as curl 8.22.0 Schannel does: stderr `a specified protocol is unsupported by libcurl`, exit 1.

## Context

Part 3 of 3 split from BL-3572 (CGR-043 of `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md`). Today Curl accepts `--proto-default smb` and tries to connect (exit 7). Start at `Curl.Cli.UnitLibrary/CommandLineProtocolSet.cs`, which parts 1 and 2 also change, so it runs after them. Measure real curl before pinning text (the report's wording may lack the `curl: (1)` prefix).

## Acceptance criteria

- [ ] `curl --proto-default smb -sS 127.0.0.1:20002/share/x` through real curl and through Curl gives the same exit code (1), stdout bytes, stderr text and no request bytes on the wire; commands and both results are recorded in Notes.
- [ ] A focused unit test in `Curl.Cli.UnitTests/Cgr043SmbProtoDefaultTests.cs` fails before the fix and passes after it (ADR-0482).
- [ ] Repro variants in CGR-043 that already matched curl still match.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Split from BL-3572, which went over its cost cap.

## Log

- 2026-10-26: Created.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 037eea96 50a598e7 4d4927c7 779790c0.
