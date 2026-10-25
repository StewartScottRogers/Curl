---
id: BL-3494
title: Fix AF-0428: SwsHttpReplySelector.Select's 404 for a request without a path can stop closing the connection with no test failing
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitLibrary, Curl.Conformance.UnitTests/SwsHttpReplySelectorTests.cs]
requirement: none
created: 2026-10-25
completed: 2026-10-25
---
# BL-3494 — Fix AF-0428: SwsHttpReplySelector.Select's 404 for a request without a path can stop closing the connection with no test failing

## Goal

The defect the audit office reported as AF-0428 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0428 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0428-swshttpreplyselector-select-s-404-for-a-request-wi.md`.

Location: `Curl.Conformance.UnitLibrary/SwsHttpReplySelector.cs:77`

Location: `Curl.Conformance.UnitLibrary/SwsHttpReplySelector.cs:77`

Sampled mutant (seed 0) survived: 'return new SwsHttpReply(Encoding.Latin1.GetBytes(NotFoundDocument), true, false);' -> '... false, false);'. SwsHttpReply's second parameter is closesConnection (SwsHttpReply.cs:4). With the mutant, the sws stand-in keeps a connection open after answering an unparseable request line with 404, where upstream's sws closes it. This decides what the conformance harness serves, so a ratchet case could pass or hang differently than against the real server, and no test notices.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Conformance.UnitLibrary/SwsHttpReplySelector.cs:77:true -Member Select -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- The 404 reply's `closesConnection` changes nothing a connection does: `SwsHttpServerConnection.Serve` sends a reply that is not from the test case with a close whatever it says, so the end-to-end test `MalformedRequestLine_GetsTheNotFoundDocumentAndACLose` cannot see the mutant. Decision (test only, no production change): `SwsHttpReplySelectorTests` pins what `Select` itself returns (404 closes, not from the test case; a normal part keeps the connection open). Added `Curl.Conformance.UnitTests/SwsHttpReplySelectorTests.cs` to `touches` for it; no task in Doing on `origin/work/dark-factory` names it.
- A lane may not run `Audit/Tools/Invoke-MutationTest.ps1`, so the reproduction was checked by applying the mutant by hand (line 77 `true, false` to `false, false`): 3 of the 4 new tests fail, and they pass on the original code. The re-audit runs the tool itself.
- No production code changed, so Measure-CodeQuality.ps1 was not needed. Build clean, fast tests green (46 test projects, 0 failures).

## Log

- 2026-10-25: Created.
- 2026-10-25: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-10-25: Doing -> Done. SwsHttpReplySelectorTests pins that Select's 404 for a request without a path closes the connection, killing AF-0428's mutant
