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
completed: 2026-10-07
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

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Added `ExecuteAsync_Http2ServerSettingsThenHeadersAndData_LogsTheSettingsLineExactlyOnce` in `Curl.Protocol.Http.UnitTests/HttpProtocolHandlerTests.FrameLog.cs`: a SETTINGS, HEADERS, DATA exchange at verbose must log exactly one `SETTINGS received:` line. The test project sits beside the library in `touches` as its tests always do; no production change was needed.
- Lanes cannot run `Audit/Tools/Invoke-MutationTest.ps1` (the audit-path guard refuses them), so the mutant was applied by hand: with `&&` changed to `||` at Http2Session.cs:432 the new test fails (`Assert.AreEqual(1, settingsLines)`), and passes with the original. The quality auditor's re-audit confirms it with the script.

## Log

- 2026-10-08: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. A test now kills the AF-0046 mutant: the HTTP/2 SETTINGS received line must be logged exactly once
