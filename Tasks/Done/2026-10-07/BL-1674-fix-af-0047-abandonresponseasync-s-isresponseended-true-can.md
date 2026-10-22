---
id: BL-1674
title: Fix AF-0047: AbandonResponseAsync's `isResponseEnded = true` can become false with no test failing
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-10-08
completed: 2026-10-07
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

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- The production code was right; the gap was a missing test. Added
  `Http2StreamConnectionTests.AbandonResponseAsync_Twice_ResetsTheStreamOnceAndDropsWhatThePeerStillSends`:
  it opens stream 1, abandons it twice, then reads, and asserts exactly one
  `RST_STREAM STREAM_CLOSED` (`00000403000000000100000005`) was written, the read returns 0,
  and no byte was read from the connection (the peer's HEADERS and DATA are dropped).
- Added `Curl.Protocol.Http.UnitTests` to `touches`: the test lives there, and no task in
  Doing on `origin/work/dark-factory` names it.
- Verification of the first criterion: lanes may not run `Audit/Tools/Invoke-MutationTest.ps1`
  (the audit guard refuses it), so the mutant was applied by hand instead - line 211's
  `isResponseEnded = true;` set to `false` - and the new test failed against it, then passed
  once reverted. The quality auditor's re-audit runs the tool itself to close AF-0047.
- Fast tests: 0 failed across every test project (Curl.Protocol.Http.UnitTests 1888 passed).

## Log

- 2026-10-08: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. A test now abandons an HTTP/2 stream twice and reads after it, killing the AF-0047 mutant
