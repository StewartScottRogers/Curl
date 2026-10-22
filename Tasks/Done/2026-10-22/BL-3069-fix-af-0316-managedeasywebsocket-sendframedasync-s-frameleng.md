---
id: BL-3069
title: Fix AF-0316: ManagedEasyWebSocket.SendFramedAsync's frameLength >= 0 can become > 0 with no test failing, so a CURLWS_OFFSET send with fragment size 0 is sent instead of refused
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary]
requirement: none
created: 2026-10-22
completed: 2026-10-22
---
# BL-3069 — Fix AF-0316: ManagedEasyWebSocket.SendFramedAsync's frameLength >= 0 can become > 0 with no test failing, so a CURLWS_OFFSET send with fragment size 0 is sent instead of refused

## Goal

The defect the audit office reported as AF-0316 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0316 (High, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0316-managedeasywebsocket-sendframedasync-s-framelength.md`.

Location: `Curl.Engine.UnitLibrary/ManagedEasyWebSocket.cs:231`

Location: `Curl.Engine.UnitLibrary/ManagedEasyWebSocket.cs:231`

Sampled mutant (seed 0) survived: 'if (frameLength >= 0 && buffer.Length > frameLength) return new CurlWebSocketSendResult(CurlCode.BadFunctionArgument, 0);' with >= turned into >. frameLength is fragmentSize under CurlWebSocketFlags.Offset. With fragmentSize 0 and a non-empty buffer, the original refuses with BadFunctionArgument, as lib/ws.c does. The mutant skips the check and writes the frame, so a refused input reaches the server and the API caller gets the wrong CurlCode.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Engine.UnitLibrary/ManagedEasyWebSocket.cs:231:>= -Member SendFramedAsync -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- The finding's reproduction script belongs to the audit office, which a lane may not run. Box 1 is checked by reasoning: with fragmentSize 0 and a one-byte buffer, `>= 0` refuses, while the mutant `> 0` skips the check and writes a frame. The new test SendAsync_NonEmptyFirstPartWithFragmentSizeZero_AnswersBadFunctionArgumentAndWritesNothing asserts the refusal and that nothing is written, so it fails under the mutant. Not run against the mutant.

## Log

- 2026-10-22: Created.
- 2026-10-22: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-10-22: Doing -> Done. test pins the CURLWS_OFFSET fragment-size-0 refusal, killing the mutant
