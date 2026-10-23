---
id: BL-3230
title: Fix AF-0364: TransferCredentialLookup.HasControlCode's >= 0 can become > 0 with no test failing, so a credential starting with a control code is accepted
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary/TransferCredentialLookup.cs]
requirement: none
created: 2026-10-23
completed:
---
# BL-3230 — Fix AF-0364: TransferCredentialLookup.HasControlCode's >= 0 can become > 0 with no test failing, so a credential starting with a control code is accepted

## Goal

The defect the audit office reported as AF-0364 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0364 (High, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0364-transfercredentiallookup-hascontrolcode-s-0-can-be.md`.

Location: `Curl.Engine.UnitLibrary/TransferCredentialLookup.cs:238`

Location: `Curl.Engine.UnitLibrary/TransferCredentialLookup.cs:238`

Invoke-MutationTest -Library Curl.Engine.UnitLibrary -MaxMutants 40 -Seed 0: survived. Original: text is not null && text.AsSpan().IndexOfAnyInRange('\0', '\u001f') >= 0; mutated: > 0. A URL user name or password that percent-decodes to a control character at index 0 (e.g. ftp://%01u:p@host/) is then not detected, so the transfer goes ahead instead of failing with curl: (3) error extracting credentials from URL (BL-1411): an exit code changes. The same member's && site (AF-0338) is now killed, but no test puts the control code first.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Engine.UnitLibrary/TransferCredentialLookup.cs:238:>= -Member HasControlCode -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-23: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
