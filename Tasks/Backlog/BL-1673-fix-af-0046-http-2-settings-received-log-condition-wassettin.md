---
id: BL-1673
title: Fix AF-0046: HTTP/2 'SETTINGS received' log condition `!wasSettingsReceived && Frames.IsPeerSettingsReceived` can become || with no test failing
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary]
requirement: none
created: 2026-10-08
completed:
---
# BL-1673 — Fix AF-0046: HTTP/2 'SETTINGS received' log condition `!wasSettingsReceived && Frames.IsPeerSettingsReceived` can become || with no test failing

## Goal

The defect the audit office reported as AF-0046 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0046 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0046-http-2-settings-received-log-condition-wassettings.md`.

Location: `Curl.Protocol.Http.UnitLibrary/Http2Session.cs:432`

Location: `Curl.Protocol.Http.UnitLibrary/Http2Session.cs:432`

Invoke-MutationTest.ps1 -Library Curl.Protocol.Http.UnitLibrary -MaxMutants 40 -Seed 0: 'if (!wasSettingsReceived && Frames.IsPeerSettingsReceived)' -> 'if (!wasSettingsReceived || Frames.IsPeerSettingsReceived)' survived (library score 0.9474). With ||, connectionLog.SettingsReceived writes the server's SETTINGS line before any SETTINGS has arrived, and again after every later frame. No test checks that the line is written exactly once, when the first SETTINGS arrives.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Protocol.Http.UnitLibrary/Http2Session.cs:432:&& -Member LogPeerConnectionFrames -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: The mutant at Curl.Protocol.Http.UnitLibrary/Http2Session.cs:432 (&& to ||) is killed.
- Actual: survived  Curl.Protocol.Http.UnitLibrary/Http2Session.cs:432 &&

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-08: Created.
