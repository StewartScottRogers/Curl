---
id: BL-3070
title: Fix AF-0317: DnsServerQuery.FoldAsciiLetter's <= 'Z' can become < 'Z' with no test failing, so a --dns-servers reply whose echoed question differs only in the case of Z is rejected
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitTests/DnsServerQueryTests.cs]
requirement: none
created: 2026-10-22
completed: 2026-10-22
---
# BL-3070 — Fix AF-0317: DnsServerQuery.FoldAsciiLetter's <= 'Z' can become < 'Z' with no test failing, so a --dns-servers reply whose echoed question differs only in the case of Z is rejected

## Goal

The defect the audit office reported as AF-0317 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0317 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0317-dnsserverquery-foldasciiletter-s-z-can-become-z-wi.md`.

Location: `Curl.Networking.UnitLibrary/DnsServerQuery.cs:143`

Location: `Curl.Networking.UnitLibrary/DnsServerQuery.cs:143`

Sampled mutant (seed 0) survived: 'value is >= (byte)'A' and <= (byte)'Z' ? (byte)(value | 0x20) : value' with <= turned into <. FoldAsciiLetter feeds AsciiEqualsIgnoringCase, which line 68 uses to match a reply's question section against the query. Under the mutant, 'Z' is not folded, so a reply that echoes a name with 'z' for 'Z' (or the reverse) no longer matches and is dropped. The lookup then times out or fails with exit 6. No test pins case-insensitive matching on the last letter.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Networking.UnitLibrary/DnsServerQuery.cs:143:<= -Member FoldAsciiLetter -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- `touches` changed from `Curl.Networking.UnitLibrary` to `Curl.Networking.UnitTests/DnsServerQueryTests.cs`: the fix is test-only. `FoldAsciiLetter` was right, but no test pinned case folding on the last letter. No other task in Doing on `origin/work/dark-factory` names that file.
- Added `Match_TheReplyWithTheLastLetterInAnotherCase_IsComplete`: a query for `zz.example` whose reply echoes `Zz.example` must match `Complete`.
- Reproduction: a lane may not run `Audit/Tools`, so the mutant was applied by hand. With `<= (byte)'Z'` turned into `< (byte)'Z'` on `DnsServerQuery.cs:143`, the new test failed (`Mismatch`), so the mutant is killed. The change was then reverted. The audit office's re-audit confirms this with its own tool.
- Build clean; fast tests: every assembly passed, none failed (Curl.Networking.UnitTests 3372 passed, 29 skipped). No production code changed, so no Measure-CodeQuality run was needed.

## Log

- 2026-10-22: Created.
- 2026-10-22: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-10-22: Doing -> Done. DnsServerQuery's reply match is pinned to fold the last letter Z, killing AF-0317's mutant
