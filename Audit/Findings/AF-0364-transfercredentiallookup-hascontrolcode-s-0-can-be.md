---
id: AF-0364
title: TransferCredentialLookup.HasControlCode's >= 0 can become > 0 with no test failing, so a credential starting with a control code is accepted
auditor: quality
severity: High
status: accepted
reason: 
key: quality:Curl.Engine.UnitLibrary/TransferCredentialLookup.cs:HasControlCode-ge:surviving-mutant
reproduction: mutation Curl.Engine.UnitLibrary/TransferCredentialLookup.cs:238:>=
task: none
tasks:
found: 2026-10-22
found-at: 9ada2459e76878e927865fe930a3afac67191f22
scorecard: 2026-10-22_1902.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0364 - TransferCredentialLookup.HasControlCode's >= 0 can become > 0 with no test failing, so a credential starting with a control code is accepted

## Summary

High finding from the quality auditor at `Curl.Engine.UnitLibrary/TransferCredentialLookup.cs:238`: TransferCredentialLookup.HasControlCode's >= 0 can become > 0 with no test failing, so a credential starting with a control code is accepted. Reported by an auditor flagged unreliable in 2026-10-22_1902.md.

## Evidence

Location: `Curl.Engine.UnitLibrary/TransferCredentialLookup.cs:238`

Invoke-MutationTest -Library Curl.Engine.UnitLibrary -MaxMutants 40 -Seed 0: survived. Original: text is not null && text.AsSpan().IndexOfAnyInRange('\0', '\u001f') >= 0; mutated: > 0. A URL user name or password that percent-decodes to a control character at index 0 (e.g. ftp://%01u:p@host/) is then not detected, so the transfer goes ahead instead of failing with curl: (3) error extracting credentials from URL (BL-1411): an exit code changes. The same member's && site (AF-0338) is now killed, but no test puts the control code first.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Engine.UnitLibrary/TransferCredentialLookup.cs:238:>= -Member HasControlCode -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

## Log

- 2026-10-22: filed proposed.
- 2026-10-22: proposed -> accepted.
