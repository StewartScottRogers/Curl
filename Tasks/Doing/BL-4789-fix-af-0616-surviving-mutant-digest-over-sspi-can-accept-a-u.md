---
id: BL-4789
title: Fix AF-0616: Surviving mutant: Digest over SSPI can accept a user name whose first character is a control character
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Authentication.UnitLibrary]
requirement: none
created: 2026-11-03
completed:
---
# BL-4789 — Fix AF-0616: Surviving mutant: Digest over SSPI can accept a user name whose first character is a control character

## Goal

The defect the audit office reported as AF-0616 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0616 (High, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0616-surviving-mutant-digest-over-sspi-can-accept-a-use.md`.

Location: `Curl.Authentication.UnitLibrary/DigestAuthenticator.cs:101`

Location: `Curl.Authentication.UnitLibrary/DigestAuthenticator.cs:101`

Mutant 'user.AsSpan().IndexOfAnyInRange('\0', '\u001f') >= 0' -> '> 0' survived (seed 791054 sample). With it, a user name starting with a control byte (e.g. '\u0001u') no longer fails with exit 94 'An authentication function returned an error' and a Digest answer is sent instead. Cgr077Tests' DataRows put the control character later in the name ('hello\n\r', 'us\tr'), never at index 0. The exit code and the request bytes change.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Authentication.UnitLibrary/DigestAuthenticator.cs:101:>= -Member CreateSspiAnswer -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-03: Created.
- 2026-11-03: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
