---
id: BL-2766
title: Fix AF-0255: TryLookUpNetrcCredentials' || can become && with no test failing, so tool-driven or netrc-ignored transfers still read .netrc
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary/ManagedEasyTransfer.cs, Curl.Engine.UnitTests/ManagedEasyTransferTests.Netrc.cs]
requirement: none
created: 2026-10-18
completed: 2026-10-18
---
# BL-2766 — Fix AF-0255: TryLookUpNetrcCredentials' || can become && with no test failing, so tool-driven or netrc-ignored transfers still read .netrc

## Goal

The defect the audit office reported as AF-0255 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0255 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0255-trylookupnetrccredentials-can-become-with-no-test.md`.

Location: `Curl.Engine.UnitLibrary/ManagedEasyTransfer.cs:707`

Location: `Curl.Engine.UnitLibrary/ManagedEasyTransfer.cs:707`

Engine sample, seed 0: survived. Original `if (ToolInputs is not null || options.Authentication.Netrc == NetrcUsage.Ignored) return true;`, mutated `&&`. With the mutant, a transfer with ToolInputs and netrc enabled, or one with netrc Ignored and no ToolInputs, runs the engine's netrc lookup it should skip. That can add credentials, an info line, or an exit-26 failure. No test pins the skip.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Engine.UnitLibrary/ManagedEasyTransfer.cs:707:|| -Member TryLookUpNetrcCredentials -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- 2026-10-18: touches narrowed from Curl.Engine.UnitLibrary to files, by reading ManagedEasyTransfer.TryLookUpNetrcCredentials (line 707), so it can run beside other Engine tasks.
- 2026-10-18: No production change. BL-2684 rewrote the method as `TryLookUpUrlAndNetrcCredentials` (now near line 758): the `||` is gone, the Ignored case moved into `TransferCredentialLookup` (pinned by `PerformAsync_NetrcIgnored_ReadsNoFileAndPutsNoCredentialsOnTheContext`), and only the `ToolInputs is not null` skip remains. Added `PerformAsync_NetrcRequiredWithToolInputs_ReadsNoFileAndPutsNoCredentialsOnTheContext` and `PerformAsync_NetrcRequiredWithToolInputsAndTheFileUnreadable_RunsTheTransfer` to pin that skip; a hand mutant making the skip also require Ignored (the finding's `&&`) fails both. A lane cannot run the finding's mutation tool (audit path guard), and its `:707:||` site no longer exists, so the re-audit should target the new skip line.

## Log

- 2026-10-18: Created.
- 2026-10-18: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-10-18: Doing -> Done. Two tests pin the ToolInputs netrc skip; the and-mutant fails both
