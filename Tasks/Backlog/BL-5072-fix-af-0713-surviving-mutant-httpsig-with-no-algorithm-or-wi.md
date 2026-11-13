---
id: BL-5072
title: Fix AF-0713: Surviving mutant: --httpsig with no algorithm, or with ed25519, can crash or be refused
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary]
requirement: none
created: 2026-11-13
completed:
---
# BL-5072 — Fix AF-0713: Surviving mutant: --httpsig with no algorithm, or with ed25519, can crash or be refused

## Goal

The defect the audit office reported as AF-0713 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0713 (High, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0713-surviving-mutant-httpsig-with-no-algorithm-or-with.md`.

Location: `Curl.Engine.UnitLibrary/HttpSignatureSetup.cs:62`

Location: `Curl.Engine.UnitLibrary/HttpSignatureSetup.cs:62`

Mutant 'given is null || given.Equals(Ed25519, ...)' -> 'given is null && given.Equals(...)' survived Curl.Engine.UnitTests. With no algorithm given the mutant falls through to given.Equals(HmacSha256...) on null (NullReferenceException), and an explicit 'ed25519' maps to null (unsupported). That breaks the default --httpsig path that decides the signature header bytes and the exit code, and no Engine test notices.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Engine.UnitLibrary/HttpSignatureSetup.cs:62:|| -Member AlgorithmOf -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-13: Created.
