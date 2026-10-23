---
id: AF-0363
title: TransferCredentialLookup.CredentialsWrittenInUrl's && can become || with no test failing, so http://zz@host/ sends no credentials
auditor: quality
severity: High
status: accepted
reason: 
key: quality:Curl.Engine.UnitLibrary/TransferCredentialLookup.cs:CredentialsWrittenInUrl-and:surviving-mutant
reproduction: mutation Curl.Engine.UnitLibrary/TransferCredentialLookup.cs:284:&&
task: BL-3229
tasks: BL-3229
found: 2026-10-22
found-at: 9ada2459e76878e927865fe930a3afac67191f22
scorecard: 2026-10-22_1902.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0363 - TransferCredentialLookup.CredentialsWrittenInUrl's && can become || with no test failing, so http://zz@host/ sends no credentials

## Summary

High finding from the quality auditor at `Curl.Engine.UnitLibrary/TransferCredentialLookup.cs:284`: TransferCredentialLookup.CredentialsWrittenInUrl's && can become || with no test failing, so http://zz@host/ sends no credentials. Reported by an auditor flagged unreliable in 2026-10-22_1902.md.

## Evidence

Location: `Curl.Engine.UnitLibrary/TransferCredentialLookup.cs:284`

Invoke-MutationTest -Library Curl.Engine.UnitLibrary -MaxMutants 40 -Seed 0: survived. Original: return user is null && password is null ? null : new NetworkCredential(user ?? string.Empty, password ?? string.Empty); mutated: user is null || password is null. The doc comment and the Curl.Engine CLAUDE.md pin curl 8.21.0 sending `zz:` for http://zz@host/ and `:x` for http://:x@host/. Under the mutant a URL with only a user name or only a password yields null credentials, so no Authorization header is sent: request bytes change and no Curl.Engine.UnitTests test notices.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Engine.UnitLibrary/TransferCredentialLookup.cs:284:&& -Member CredentialsWrittenInUrl -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

## Log

- 2026-10-22: filed proposed.
- 2026-10-22: proposed -> accepted.
