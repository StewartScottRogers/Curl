---
id: BL-4005
title: Fix CGR-135 part 2: measure every CGR-135 repro against real curl, close the cltwo gap and the quality gates
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-3667, BL-3668, BL-3979, BL-4004]
touches: [Curl.Protocol.Http.UnitLibrary/Http2Session.cs, Curl.Protocol.Http.UnitLibrary/HttpTransferMessages.cs, Curl.Protocol.Http.UnitTests/Cgr135Tests.cs]
requirement: none
created: 2026-10-28
completed:
---
# BL-4005 — Fix CGR-135 part 2: measure every CGR-135 repro against real curl, close the cltwo gap and the quality gates

## Goal

Every CGR-135 repro, run through real curl and through Curl, gives the same result, including `num_headers` for the `cltwo` case, and the library stays inside its quality gates.

## Context

Second half of BL-3664 (split); BL-4004 lands the fix first. BL-3664's Notes hold the REF-LINUX measurements and the PORT results of the earlier run: every repro matched except `cltwo` (`[2 200 1 0 32 0]` against REF's `[2 200 1 0 32 1]`), which BL-3979 addressed.

## Acceptance criteria

- [ ] Each repro command in CGR-135 (see BL-3664's Notes) is run through real curl and through Curl; the commands and both results are recorded in Notes, all matching (exit code, stdout bytes, stderr text, GOAWAY on the wire), including `cltwo` with num_headers 1.
- [ ] If a gap remains, a focused test in `Cgr135Tests.cs` and the fix close it.
- [ ] `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Protocol.Http.UnitLibrary` passes (100% line/branch coverage on the new code, complexity, CRAP).
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Split from BL-3664 (over its cost cap).

## Log

- 2026-10-28: Created.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 7cb8fe79 b61262fc e58d9009 779790c0.
