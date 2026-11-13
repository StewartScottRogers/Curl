---
id: BL-5073
title: Fix AF-0714: Surviving mutant: a handle's cookie jar can be dropped or taken from a non-HTTP transfer at cleanup
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary]
requirement: none
created: 2026-11-13
completed: 2026-11-13
---
# BL-5073 — Fix AF-0714: Surviving mutant: a handle's cookie jar can be dropped or taken from a non-HTTP transfer at cleanup

## Goal

The defect the audit office reported as AF-0714 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0714 (High, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0714-surviving-mutant-a-handle-s-cookie-jar-can-be-drop.md`.

Location: `Curl.Engine.UnitLibrary/ManagedEasyTransfer.SetUp.cs:78`

Location: `Curl.Engine.UnitLibrary/ManagedEasyTransfer.SetUp.cs:78`

Mutant 'cookies is not null && IsHttpUrl(...)' -> 'cookies is not null || IsHttpUrl(...)' survived. With it, an HTTP transfer with no cookie engine sets handleCookieJar to null (the jar written at cleanup loses its cookies), and an FTP transfer with cookies replaces the jar. Both change the bytes of the CURLOPT_COOKIEJAR file.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Engine.UnitLibrary/ManagedEasyTransfer.SetUp.cs:78:&& -Member CleanUpUnlessToolDoesAsync -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

The audit guard refuses a lane running Invoke-MutationTest.ps1, so the mutant (`&&` to `||` at ManagedEasyTransfer.SetUp.cs:78) was applied by hand: both new tests in ManagedEasyTransferTests.Files.cs (`DisposeAsync_HttpPerformWithNoCookieOptionsAfterOneWithAJar_StillWritesTheEarlierJar`, `DisposeAsync_NonHttpPerformWithCookiesAfterOneWithAJar_KeepsTheHttpJar`) failed with it and pass without it. Fast Engine tests green (3014 passed). Test-only change, no production code touched.

## Log

- 2026-11-13: Created.
- 2026-11-13: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-11-13: Doing -> Done. Two tests kill the mutant
