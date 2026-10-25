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
completed: 2026-10-24
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

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Lane run: widened `touches` to the test file. Mutating line 441's `true` to `false` by hand and running a new test (a second read after the close still ends at once) still passed, so the mutant looks equivalent: a spent close entry and an empty queue both end in `ReadWithNothingOwedAsync` returning 0, because a closing reply sets `readsRequests` false and `idling`, `streaming` and `upgradedTrafficOpen` cannot be set with a Normal-mode close. No test can tell them apart without a production change that gives `IsClose` an observable effect. The reproduction script lives under `Audit/`, which a lane may not run. The draft test is left uncommitted in the worktree stash.
- Needs: Stewart or the audit office to accept this as an equivalent mutant, or to say which production change should make `IsClose` observable.
- 2026-10-24 (interactive session), decision: the mutant is equivalent, so the code was simplified rather than tested. Reasoning: `IsClose` was read only by `ReadSentAsync`'s loop guard. A close is always the last send queued (a closing reply sets `readsRequests` false, so `ServeCompleteRequests` serves nothing more; `CloseUpgradedTraffic` clears `upgradedTrafficOpen` on a connection whose `readsRequests` is already false; a tunnel relay needs a reply that does not close). With `IsClose` true the entry stays at the head and every later read returns 0; with false it is consumed as 0 bytes and every later read goes to `ReadWithNothingOwedAsync`, which also returns 0, since a close comes only in the Normal reply mode (no streaming, no idling) with no upgraded traffic open. Both wait for the same `SentAt` first, so timing is the same too. Reply bytes are never queued empty (`count > 0`), so an empty send already is a close. Commit ef398d90a removes `IsClose` and the bool parameter from `SwsServerSend`, and the guard from the loop, so no unkillable `true`/`false` remains; the type's doc and a comment on `ReadSentAsync` say why. The lane's draft test (left in its stash) was not used, as it cannot kill an equivalent mutant. No ADR: this sets no rule. The reproduction, run at ef398d90a: outcome `site-missing`, resolvedLine null (no `true` site is left in `Send`), so AF-0420 is expected to close only by Stewart (`closed-how: stewart`), as AF-0255 and AF-0257 did. Curl.Conformance.UnitTests fast tests: 4373 passed, 0 failed; `Measure-CodeQuality.ps1 -Library Curl.Conformance.UnitLibrary`: 100% line, 100% branch, 0 failing members.

## Log

- 2026-10-24: Created.
- 2026-10-24: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-10-24: Doing -> Blocked. Stewart: is AF-0420's mutant (SwsHttpServerConnection.cs:441 true->false) equivalent? A hand check shows no read differs, so no test can kill it
- 2026-10-24: Blocked -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-10-24: Doing -> Done. Decided equivalent: a close is always the last send and an empty queue also reads 0, so IsClose was dropped and a close is an empty send; reproduction now site-missing, not survived
