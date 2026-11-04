---
id: BL-4839
title: Fix AF-0657: Surviving mutant: a pinned-public-key file of exactly 1 MiB can be refused
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary]
requirement: none
created: 2026-11-04
completed: 2026-11-04
---
# BL-4839 — Fix AF-0657: Surviving mutant: a pinned-public-key file of exactly 1 MiB can be refused

## Goal

The defect the audit office reported as AF-0657 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0657 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0657-surviving-mutant-a-pinned-public-key-file-of-exact.md`.

Location: `Curl.Networking.UnitLibrary/PinnedPublicKey.cs:165`

Location: `Curl.Networking.UnitLibrary/PinnedPublicKey.cs:165`

Mutant 'file.Length > MaximumFileSize' -> '>=' survived (MaximumFileSize = 1_048_576). A key file at the limit then reads as missing and pinning fails. No test covers the boundary.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Networking.UnitLibrary/PinnedPublicKey.cs:165:> -Member ReadKeyFile -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Added `PinnedPublicKeyTests.Matches_WithAFileOfExactly1MiB_Matches`: a PEM key padded to exactly 1048576 bytes must match. Applying the mutant (`>` to `>=`) by hand makes it fail, so the mutant is killed (a lane cannot run the mutation tool, so the re-audit confirms). Test-only change, no production code, so no quality run. Networking fast tests: 4030 passed, 0 failed.

## Log

- 2026-11-04: Created.
- 2026-11-04: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-11-04: Doing -> Done. Boundary test kills the 1 MiB mutant
