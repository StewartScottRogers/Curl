---
id: BL-4241
title: Fix AF-0460: Dispose_NeverSignalled_ClosesTheListenerAndLaterSignalsDoNothing has no assertion, so the listener's closing is never checked
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitTests]
requirement: none
created: 2026-10-29
completed: 2026-10-29
---
# BL-4241 — Fix AF-0460: Dispose_NeverSignalled_ClosesTheListenerAndLaterSignalsDoNothing has no assertion, so the listener's closing is never checked

## Goal

The defect the audit office reported as AF-0460 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0460 (Low, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0460-dispose-neversignalled-closesthelistenerandlatersi.md`.

Location: `Curl.Engine.UnitTests/SocketActionGatedResolverTests.cs:109`

Location: `Curl.Engine.UnitTests/SocketActionGatedResolverTests.cs:109`

Found by Find-WeakTests.ps1 (no-assertion). The body is `LoopbackLookupWakeup wakeup = LoopbackLookupWakeup.Open(); wakeup.Dispose(); wakeup.Signal();` with no Assert of any kind. The name promises `ClosesTheListener`, but a Dispose that left the loopback listener open would pass. Only `does not throw` is tested.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Engine.UnitTests/SocketActionGatedResolverTests.cs -Pattern 'Dispose_NeverSignalled_ClosesTheListenerAndLaterSignalsDoNothing' -Context 0,8
```

- Expected: The test asserts the listener is closed after Dispose.
- Actual: Lines 109-116 call Open, Dispose and Signal and assert nothing.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Delivered directly (one test fix, no production change, so the feature pipeline's plan stage and a quality run were not needed). The test now reads the listener's loopback end point before Dispose, calls Signal after it (which would throw if it reached the disposed listener), and asserts a connect to that end point is refused with ConnectionRefused, so a Dispose that left the listener open fails. ConnectionRefused is the answer on Windows, Linux and macOS, so the test is platform-neutral.
- Reproduction now shows Assert lines in the test body; build clean, fast tests green on Windows.

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-10-29: Doing -> Done. Dispose_NeverSignalled test now asserts the listener's port refuses connections after Dispose
