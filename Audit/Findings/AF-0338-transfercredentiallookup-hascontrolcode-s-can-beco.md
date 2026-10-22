---
id: AF-0338
title: TransferCredentialLookup.HasControlCode's && can become || with no test failing, so every non-null URL credential on a non-http scheme is refused
auditor: quality
severity: High
status: proposed
reason:
key: quality:Curl.Engine.UnitLibrary/TransferCredentialLookup.cs:HasControlCode-and:surviving-mutant
reproduction: mutation Curl.Engine.UnitLibrary/TransferCredentialLookup.cs:238:&&
task: none
tasks:
found: 2026-10-22
found-at: 608a755586679cf34031a0e5b137f01b9d7fb329
scorecard: 2026-10-22_1120.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0338 - TransferCredentialLookup.HasControlCode's && can become || with no test failing, so every non-null URL credential on a non-http scheme is refused

## Summary

High finding from the quality auditor at `Curl.Engine.UnitLibrary/TransferCredentialLookup.cs:238`: TransferCredentialLookup.HasControlCode's && can become || with no test failing, so every non-null URL credential on a non-http scheme is refused.

## Evidence

Location: `Curl.Engine.UnitLibrary/TransferCredentialLookup.cs:238`

Sampled mutant (seed 0) survived: 'text is not null && text.AsSpan().IndexOfAnyInRange(...) >= 0' became 'text is not null || ...'. HasControlCode feeds HasRefusedControlCode, which line 168 uses on the URL's decoded user name and password for every scheme but http, https, ws and wss. With the mutant, any user name or password in such a URL (ftp://user:pw@host/) counts as holding a control code, so the transfer fails with curl: (3) error extracting credentials from URL before connecting. That is a user-visible exit code, and no Curl.Engine.UnitTests test noticed.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Engine.UnitLibrary/TransferCredentialLookup.cs:238:&& -Member HasControlCode -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

## Re-audits

## Log

- 2026-10-22: filed proposed.
