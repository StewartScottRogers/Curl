---
id: BL-2810
title: Fix AF-0283: RunAsync_AddHandleFails_AnswersAndCleansUp checks neither the answer's code nor the clean-up
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.LibTest.UnitTests]
requirement: none
created: 2026-10-19
completed: 2026-10-19
---
# BL-2810 — Fix AF-0283: RunAsync_AddHandleFails_AnswersAndCleansUp checks neither the answer's code nor the clean-up

## Goal

The defect the audit office reported as AF-0283 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0283 (Low, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0283-runasync-addhandlefails-answersandcleansup-checks.md`.

Location: `Curl.Conformance.LibTest.UnitTests/Lib539To553Tests.cs:143`

Location: `Curl.Conformance.LibTest.UnitTests/Lib539To553Tests.cs:143`

With curl_multi_add_handle scripted to answer BadEasyHandle, the only assertion is Assert.AreNotEqual(0, result.ExitCode). The name promises an answer and a clean-up, but no handle disposal or removal is checked, and any non-zero code passes.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Conformance.LibTest.UnitTests/Lib539To553Tests.cs -Pattern 'RunAsync_AddHandleFails_AnswersAndCleansUp' -Context 0,10
```

- Expected: Asserts the exact exit code and that the easy and multi handles were cleaned up.
- Actual: One Assert.AreNotEqual(0, result.ExitCode) only.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Test-only fix, made directly rather than through every `/feature` stage: no production code changes, so there was nothing to plan, implement or conformance-check. BL-2809 had already pinned the exit code to `LibTestErrorCodes.Multi`; this run adds the clean-up checks.
- `FakeMultiTransfer` now counts `Removals` and `Disposals`. The test asserts the refused easy handle is disposed once (`DisposedEasyCount`), a handle that was never added is not removed, the multi handle is disposed once, and the second request never starts.
- Verified: the reproduction now shows the exact code plus four clean-up assertions; `dotnet build` is clean; the fast tests pass (Curl.Conformance.LibTest.UnitTests 1183/1183, no failures solution-wide).

## Log

- 2026-10-19: Created.
- 2026-10-19: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-10-19: Doing -> Done. Lib540's add-handle-failure test pins the Multi code and checks the easy and multi handles are cleaned up
