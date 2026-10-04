---
id: BL-1261
title: Fix AF-0005: Brainpool point decoding: changing || to && in the below-modulus check is not caught by any test
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cryptography.UnitLibrary, Curl.Cryptography.UnitTests]
requirement: none
created: 2026-10-03
completed: 2026-10-02
---
# BL-1261 — Fix AF-0005: Brainpool point decoding: changing || to && in the below-modulus check is not caught by any test

## Goal

The defect the audit office reported as AF-0005 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0005 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0005-brainpool-point-decoding-changing-to-in-the-below.md`.

Location: `Curl.Cryptography.UnitLibrary/BrainpoolPoint.cs:161`

Location: `Curl.Cryptography.UnitLibrary/BrainpoolPoint.cs:161`

Mutant survived (seed 0): `if (!domain.Field.IsBelowModulus(x) || !domain.Field.IsBelowModulus(y))` became `... && ...`. A point with only one out-of-range coordinate would then be accepted, which weakens the public-key validation decision. No test supplies exactly one out-of-range coordinate. Mutation score for the library was 0.95 (36 killed, 2 timed out, 2 survived).

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Library Curl.Cryptography.UnitLibrary -MaxMutants 40 -Seed 0 -TimeoutSeconds 300 -OutFile $env:TEMP\mutation-crypto.json
```

- Expected: The mutant at Curl.Cryptography.UnitLibrary/BrainpoolPoint.cs:161 (||) is killed.
- Actual: survived  Curl.Cryptography.UnitLibrary/BrainpoolPoint.cs:161 ||

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Why the mutant survived: the existing tests send x = p or y = p, which with `&&` slip past
  the range check but then fail the on-curve check (p reduces to 0), so the result was false
  either way. Killing it needs a coordinate at or above p that still reduces to a curve point.
- Fix (test only, production code unchanged): `BrainpoolEcdhTests.TryComputeSharedSecret_PeerPointWithOnlyYRaisedByThePrime_ReturnsFalseAndZeroesTheSecret`
  sends the RFC 7027 peer x with y + p, or (p - y) + p (the negated point) where y + p does not
  fit the field length, on all three curves. Only the `||` check refuses it.
- Verified by hand-applying the mutant (`||` -> `&&` at BrainpoolPoint.cs:161): all three
  data rows fail; reverted, all pass. The lane cannot run `Audit/Tools/Invoke-MutationTest.ps1`
  (audit paths are guarded from lanes), so the first box is ticked on that equivalent check;
  the quality auditor's re-audit confirms it.
- Added `Curl.Cryptography.UnitTests` to `touches`: the fix is a test. No other Doing task
  (BL-1258: Networking, Console) names it.

## Log

- 2026-10-03: Created.
- 2026-10-02: Backlog -> Doing.
- 2026-10-02: Doing -> Done. A Brainpool peer point with only y at or above p is refused by a test, killing AF-0005's || -> && mutant
