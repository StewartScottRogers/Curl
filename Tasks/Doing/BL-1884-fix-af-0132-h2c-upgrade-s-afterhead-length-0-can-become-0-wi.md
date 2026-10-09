---
id: BL-1884
title: Fix AF-0132: h2c upgrade's 'afterHead.Length > 0' can become '>= 0' with no test failing: a spurious 'Copied HTTP/2 data ... len=0' -v line
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary]
requirement: none
created: 2026-10-09
completed:
---
# BL-1884 — Fix AF-0132: h2c upgrade's 'afterHead.Length > 0' can become '>= 0' with no test failing: a spurious 'Copied HTTP/2 data ... len=0' -v line

## Goal

The defect the audit office reported as AF-0132 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0132 (High, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0132-h2c-upgrade-s-afterhead-length-0-can-become-0-with.md`.

Location: `Curl.Protocol.Http.UnitLibrary/HttpH2cUpgradeConnection.cs:179`

Location: `Curl.Protocol.Http.UnitLibrary/HttpH2cUpgradeConnection.cs:179`

Invoke-MutationTest.ps1 -Library Curl.Protocol.Http.UnitLibrary -MaxMutants 40 -Seed 0 reported 'survived  Curl.Protocol.Http.UnitLibrary/HttpH2cUpgradeConnection.cs:179 >' (member SwitchAsync, 'if (afterHead.Length > 0)' -> 'if (afterHead.Length >= 0)'), and the targeted -Site run confirmed it survived. The guard decides whether -v writes curl's 'Copied HTTP/2 data in stream buffer to connection buffer after upgrade: len=N' line after the 101 head. With the mutant, an upgrade where no HTTP/2 bytes arrived in the same read as the 101 writes 'len=0', a line curl never prints, so verbose stderr bytes differ. The only tests touching the line (HttpProtocolHandlerTests.H2cUpgrade.cs:107, HttpProtocolHandlerTests.Http2Trace.cs:105) assert it when data followed (len=31, len=42). No test asserts that it is absent when nothing followed the head.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Protocol.Http.UnitLibrary/HttpH2cUpgradeConnection.cs:179:> -Member SwitchAsync -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-09: Created.
- 2026-10-09: Backlog -> Doing.
