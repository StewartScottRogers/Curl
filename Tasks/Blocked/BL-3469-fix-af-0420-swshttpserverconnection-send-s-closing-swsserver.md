---
id: BL-3469
title: Fix AF-0420: SwsHttpServerConnection.Send's closing SwsServerSend(..., true) can become false with no test failing, so a closing reply never marks the close
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitLibrary, Curl.Conformance.UnitTests/SwsHttpServerConnectorTests.cs]
requirement: none
created: 2026-10-24
completed:
---
# BL-3469 — Fix AF-0420: SwsHttpServerConnection.Send's closing SwsServerSend(..., true) can become false with no test failing, so a closing reply never marks the close

## Goal

The defect the audit office reported as AF-0420 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0420 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0420-swshttpserverconnection-send-s-closing-swsserverse.md`.

Location: `Curl.Conformance.UnitLibrary/SwsHttpServerConnection.cs:441`

Location: `Curl.Conformance.UnitLibrary/SwsHttpServerConnection.cs:441`

Invoke-MutationTest.ps1 -Library Curl.Conformance.UnitLibrary -MaxMutants 40 -Seed 0: survived, 'pendingSends.Add(new SwsServerSend([], serverBusyUntil, true));' -> 'false'. The third argument is isClose: the send queued after a closing reply (swsclose, close after the reply) is what ends the connection. With false, the stand-in server no longer closes after such a reply. No test checks that the connection ends.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Conformance.UnitLibrary/SwsHttpServerConnection.cs:441:true -Member Send -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Lane run: widened `touches` to the test file. Mutating line 441's `true` to `false` by hand and running a new test (a second read after the close still ends at once) still passed, so the mutant looks equivalent: a spent close entry and an empty queue both end in `ReadWithNothingOwedAsync` returning 0, because a closing reply sets `readsRequests` false and `idling`, `streaming` and `upgradedTrafficOpen` cannot be set with a Normal-mode close. No test can tell them apart without a production change that gives `IsClose` an observable effect. The reproduction script lives under `Audit/`, which a lane may not run. The draft test is left uncommitted in the worktree stash.
- Needs: Stewart or the audit office to accept this as an equivalent mutant, or to say which production change should make `IsClose` observable.

## Log

- 2026-10-24: Created.
- 2026-10-24: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-10-24: Doing -> Blocked. Stewart: is AF-0420's mutant (SwsHttpServerConnection.cs:441 true->false) equivalent? A hand check shows no read differs, so no test can kill it
