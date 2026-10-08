---
id: BL-1674
title: Fix AF-0047: AbandonResponseAsync's `isResponseEnded = true` can become false with no test failing
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary]
requirement: none
created: 2026-10-08
completed:
---
# BL-1674 — Fix AF-0047: AbandonResponseAsync's `isResponseEnded = true` can become false with no test failing

## Goal

The defect the audit office reported as AF-0047 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0047 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0047-abandonresponseasync-s-isresponseended-true-can-be.md`.

Location: `Curl.Protocol.Http.UnitLibrary/Http2StreamConnection.cs:211`

Location: `Curl.Protocol.Http.UnitLibrary/Http2StreamConnection.cs:211`

Invoke-MutationTest.ps1 -Library Curl.Protocol.Http.UnitLibrary -MaxMutants 40 -Seed 0: 'isResponseEnded = true;' -> 'isResponseEnded = false;' survived. The doc comment says the stream is reset with STREAM_CLOSED once and 'the frames the peer still sends on it are dropped'. With the mutant the stream never counts as ended: a second AbandonResponseAsync writes a second RST_STREAM (extra request bytes), and the reads at lines 112, 171 and 260 keep waiting for or taking frames on the reset stream instead of dropping them. No test abandons twice or reads after abandoning.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Protocol.Http.UnitLibrary/Http2StreamConnection.cs:211:true -Member AbandonResponseAsync -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: The mutant at Curl.Protocol.Http.UnitLibrary/Http2StreamConnection.cs:211 (true to false) is killed.
- Actual: survived  Curl.Protocol.Http.UnitLibrary/Http2StreamConnection.cs:211 true

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-08: Created.
