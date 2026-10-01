---
id: AF-0003
title: RC4 lookup depends on the key
auditor: security
severity: High
status: rejected
key: security:Curl.Cryptography.UnitLibrary/Rc4.cs:NextKeyStreamByte:secret-dependent-lookup
task: none
found: 2026-10-01
found-at: 1111111
scorecard: 2026-10-01_0900.md
closed: 
closed-by: 
---
# AF-0003 - RC4 lookup depends on the key

## Summary

Fixture finding.

## Evidence

Location: `fixture`

Fixture evidence.

## Reproduction

Run from the repository root:

```powershell
Write-Output fixture
```

- Expected: fixture
- Actual: fixture

## Re-audits
