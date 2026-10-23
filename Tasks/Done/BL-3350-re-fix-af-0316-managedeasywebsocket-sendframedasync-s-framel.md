---
id: BL-3350
title: Re-fix AF-0316: ManagedEasyWebSocket.SendFramedAsync's frameLength >= 0 can become > 0 with no test failing, so a CURLWS_OFFSET send with fragment size 0 is sent instead of refused
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary]
requirement: none
created: 2026-10-23
completed: 2026-10-23
---
# BL-3350 — Re-fix AF-0316: ManagedEasyWebSocket.SendFramedAsync's frameLength >= 0 can become > 0 with no test failing, so a CURLWS_OFFSET send with fragment size 0 is sent instead of refused

## Goal

The defect the audit office reported as AF-0316 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0316 (High, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0316-managedeasywebsocket-sendframedasync-s-framelength.md`.

Re-fix: the earlier task(s) BL-3069 reached Done, and a later re-audit by the quality auditor found the reproduction still reproduces:

- 2026-10-23 | 2026-10-23_0618.md | reproduces: yes (runner rerun on clean commit) | Ran the -Site command: Curl.Engine.UnitLibrary/ManagedEasyWebSocket.cs:231 '>=' in SendFramedAsync (frameLength >= 0 && buffer.Length > frameLength) resolved to line 231. Outcome survived; no baseline exclusions. Runner's targeted mutation rerun on the clean audited commit: survived.

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

- Why BL-3069 did not kill the mutant: on its input (fragment size 0, a one-byte part, `CURLWS_TEXT`) the mutant `> 0` skips the check, but `SendPartAsync` then refuses the part too (`1 > sendFrameLeft 0`) before writing the head, so the result and the bytes written are the same. The mutant is not equivalent, though: `StartFrame` has already run, and under `CURLWS_CONT` it leaves `continuesSentMessage` set, so the next `CURLWS_TEXT` send goes out as a continuation frame (opcode 0) instead of a new text frame.
- New test `SendAsync_AfterARefusedUnfinishedFirstPartWithFragmentSizeZero_StartsANewTextMessage` pins this: after the refused `TEXT|CONT|OFFSET` send of fragment size 0, a plain `TEXT` send writes a first byte of 0x81. Checked by hand against the mutant (`>=` turned into `>` with the Edit tool, then put back): only this test failed (1 of 45 in `ManagedEasyWebSocketTests`). The audit script itself is the audit office's, which a lane does not run; box 1 is ticked on that hand check.
- Production code is unchanged, so no quality run was needed. The test lives in `Curl.Engine.UnitTests`, the test project of the library this task touches.

## Log

- 2026-10-23: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-10-23: Doing -> Done. a test kills the mutant: a refused CURLWS_OFFSET send with fragment size 0 leaves the next send a new message
