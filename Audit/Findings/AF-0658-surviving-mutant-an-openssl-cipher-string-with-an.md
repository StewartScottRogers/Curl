---
id: AF-0658
title: Surviving mutant: an OpenSSL cipher string with an empty alias word can be accepted instead of refused
auditor: quality
severity: High
status: accepted
reason: 
key: quality:Curl.Networking.UnitLibrary/OpenSslCipherString.cs:ApplyNextRule-false:surviving-mutant
reproduction: mutation Curl.Networking.UnitLibrary/OpenSslCipherString.cs:214:false
task: none
tasks:
found: 2026-11-04
found-at: 650d20c75108547e7bc74da2e86fbee9c49f7931
scorecard: 2026-11-04_0643.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0658 - Surviving mutant: an OpenSSL cipher string with an empty alias word can be accepted instead of refused

## Summary

High finding from the quality auditor at `Curl.Networking.UnitLibrary/OpenSslCipherString.cs:214`: Surviving mutant: an OpenSSL cipher string with an empty alias word can be accepted instead of refused.

## Evidence

Location: `Curl.Networking.UnitLibrary/OpenSslCipherString.cs:214`

Mutant 'return (false, null);' -> 'return (true, null);' survived. In ReadAliases, an empty word (for example a trailing '+') is then accepted as valid, so a cipher list curl refuses with exit 59 (SSL cipher) would be accepted. No test covers an empty alias word.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Networking.UnitLibrary/OpenSslCipherString.cs:214:false -Member ApplyNextRule -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

## Re-audits

## Log

- 2026-11-04: filed proposed.
- 2026-11-04: proposed -> accepted.
