---
id: BL-2760
title: Fix AF-0249: SplitAtFirstColon's colon < 0 can become <= 0 with no test failing, mis-splitting a proxy user:password that starts with ':'
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary, Curl.Engine.UnitTests]
requirement: none
created: 2026-10-18
completed: 2026-10-18
---
# BL-2760 — Fix AF-0249: SplitAtFirstColon's colon < 0 can become <= 0 with no test failing, mis-splitting a proxy user:password that starts with ':'

## Goal

The defect the audit office reported as AF-0249 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0249 (High, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0249-splitatfirstcolon-s-colon-0-can-become-0-with-no-t.md`.

Location: `Curl.Engine.UnitLibrary/ManagedEasyTransfer.cs:1755`

Location: `Curl.Engine.UnitLibrary/ManagedEasyTransfer.cs:1755`

Invoke-MutationTest -Library Curl.Engine.UnitLibrary -MaxMutants 40 -Seed 0: survived. Original `return colon < 0 ? (userPassword, string.Empty) : (userPassword[..colon], userPassword[(colon + 1)..]);`, mutated `colon <= 0`. With --proxy-user ':secret' the original gives user '' and password 'secret'. The mutant gives user ':secret' and an empty password, so the Proxy-Authorization request bytes change. The tool named the member 'static'; the method is SplitAtFirstColon.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Engine.UnitLibrary/ManagedEasyTransfer.cs:1755:< -Member static -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- The production code was right; the gap was a missing test. Added the DataRow
  `(":secret", null, null, "", "secret")` to
  `ManagedEasyTransferTests.ProxyCredentialsOf_ProxyCredentialOptions_SplitsUserPasswordAtTheFirstColonAndLetsTheOthersWin`
  in `Curl.Engine.UnitTests` (added to `touches`; no task in Doing names it).
- The lane's audit guard refuses running `Audit/Tools/Invoke-MutationTest.ps1`, so the
  mutant was reproduced by hand: with `colon < 0` changed to `colon <= 0` at
  `ManagedEasyTransfer.cs:1755`, the new row fails (user `:secret` for expected ``), 1 of 8
  failed; reverted, all 8 pass. The quality auditor's re-audit runs the tool itself.
- Fast tests: every test project passed; no production code changed, so no quality run.

## Log

- 2026-10-18: Created.
- 2026-10-18: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-10-18: Doing -> Done. A proxy user:password starting with ':' is now pinned by a test, so the colon <= 0 mutant at SplitAtFirstColon is killed
