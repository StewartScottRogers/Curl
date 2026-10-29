---
id: BL-4240
title: Fix AF-0459: Of_NeitherCallbackSet_GivesTheBuiltInMeterAlone asserts only that CallbackTransferProgress.Of returned something
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitTests]
requirement: none
created: 2026-10-29
completed: 2026-10-29
---
# BL-4240 — Fix AF-0459: Of_NeitherCallbackSet_GivesTheBuiltInMeterAlone asserts only that CallbackTransferProgress.Of returned something

## Goal

The defect the audit office reported as AF-0459 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0459 (Low, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0459-of-neithercallbackset-givesthebuiltinmeteralone-as.md`.

Location: `Curl.Engine.UnitTests/CallbackTransferProgressTests.cs:46`

Location: `Curl.Engine.UnitTests/CallbackTransferProgressTests.cs:46`

Found by Find-WeakTests.ps1 (weak-assertion, only Assert.IsNotNull). The whole body is `Assert.IsNotNull(CallbackTransferProgress.Of(noProgress: false, new EasyCallbackOptions(), abort, () => Stream.Null, TimeProvider.System));`. CallbackTransferProgress.Of (Curl.Engine.UnitLibrary/CallbackTransferProgress.cs:54-68) returns a callback-calling progress when TransferInfo or Progress is set, and the built-in meter otherwise. The test would also pass if the no-callback branch returned a callback-wrapping progress, so `the built-in meter alone` is never checked.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Engine.UnitTests/CallbackTransferProgressTests.cs -Pattern 'Of_NeitherCallbackSet_GivesTheBuiltInMeterAlone' -Context 0,5
```

- Expected: The test asserts the result is the built-in meter with no callback (for example, that reporting progress calls nothing and draws the meter).
- Actual: Its only assertion is Assert.IsNotNull(CallbackTransferProgress.Of(...)).

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- The test now draws a download through the meter onto a MemoryStream and asserts the header lines, the closing newline (which only the no-callback meter writes: the callback-wrapping progress has EndsWithNewLine false) and that nothing aborted. Test-only change, so no Measure-CodeQuality run was needed. The reproduction now shows those assertions, not Assert.IsNotNull.
- Narrowing touches to the file (ADR-0694) was skipped: the push to work/dark-factory was refused in this run, and too broad is safe.
- dotnet build clean; fast tests green (64 test projects, 0 failed; Curl.Engine.UnitTests 2928 passed). Platform-neutral: Environment.NewLine and a MemoryStream only.

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-10-29: Doing -> Done. Of_NeitherCallbackSet_GivesTheBuiltInMeterAlone now checks the built-in meter draws its header and closing newline with nothing aborted
