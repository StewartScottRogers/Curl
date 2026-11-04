---
id: BL-4835
title: Fix AF-0653: Surviving mutant: a MASQUE 101 status line without a reason phrase can be refused as malformed
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extension.Masque.UnitLibrary, Curl.Extension.Masque.UnitTests/MasqueUpgradeTests.cs]
requirement: none
created: 2026-11-04
completed: 2026-11-04
---
# BL-4835 — Fix AF-0653: Surviving mutant: a MASQUE 101 status line without a reason phrase can be refused as malformed

## Goal

The defect the audit office reported as AF-0653 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0653 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0653-surviving-mutant-a-masque-101-status-line-without.md`.

Location: `Curl.Extension.Masque.UnitLibrary/Wire/MasqueUpgrade.cs:61`

Location: `Curl.Extension.Masque.UnitLibrary/Wire/MasqueUpgrade.cs:61`

Mutant 'parts.Length < 2' -> 'parts.Length <= 2' survived. statusLine.Split(' ', 3) of 'HTTP/1.1 101' gives 2 parts, so the mutant refuses a valid upgrade reply and the tunnel fails with exit 7. No test sends a status line without a reason phrase.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Extension.Masque.UnitLibrary/Wire/MasqueUpgrade.cs:61:< -Member ParseStatus -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Added `Curl.Extension.Masque.UnitTests/MasqueUpgradeTests.cs` to touches: the fix is a test, and that file is the library's own test class for `MasqueUpgrade` (no other Doing task names it).
- Fix: `CheckReply_101WithTheUpgradeHeaders_Opens` gains a row `HTTP/1.1 101` with no reason phrase. No production change.
- Reproduction: a lane may not run `Audit/Tools`, so the mutant was applied by hand (`parts.Length <= 2`) and the Masque unit tests run: the new row failed (killed); reverted, 126/126 pass. `dotnet build` clean, fast tests green across the solution.

## Log

- 2026-11-04: Created.
- 2026-11-04: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-11-04: Doing -> Done. MASQUE 101 status line without a reason phrase is now tested; mutant at MasqueUpgrade.cs:61 is killed
