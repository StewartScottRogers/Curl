---
id: AF-0833
title: Protocols.md's SMB row says disabled in every build, exit 1, not in curl -V; the code serves smb/smbs and lists them
auditor: truthfulness
severity: High
status: proposed
reason:
key: truthfulness:Documentation/Protocols.md:SMB:false-statement
reproduction: none
task: none
tasks:
found: 2026-11-16
found-at: f28eba04920fb5d8dd2a1c70283f268a1891b7e5
scorecard: 2026-11-16_1333.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0833 - Protocols.md's SMB row says disabled in every build, exit 1, not in curl -V; the code serves smb/smbs and lists them

## Summary

High finding from the truthfulness auditor at `Documentation/Protocols.md:35`: Protocols.md's SMB row says disabled in every build, exit 1, not in curl -V; the code serves smb/smbs and lists them.

## Evidence

Location: `Documentation/Protocols.md:35`

Protocols.md:35, SMB row: 'Disabled in every build, as in curl 8.22.0: a transfer fails with exit 1 and `Protocol "smb" is disabled`, and `curl -V` does not list them (ADR-0835, ADR-1263). The library holds the SMBv1 client a build with SMB would use.' The code: Curl.Engine.UnitLibrary/EngineComposition.cs:197 registers SmbProtocolHandler for ["smb", "smbs"]; ManagedCurlVersion.cs:18 lists smb and smbs in -V (commit e019d9e5a8, BL-5286, ADR-1281).

## Reproduction

Run from the repository root:

```powershell
(Select-String -Path Documentation/Protocols.md -Pattern 'Disabled in every build').Count; (Select-String -Path Curl.Engine.UnitLibrary/EngineComposition.cs -Pattern 'new SmbProtocolHandler').Count
```

- Expected: 0, 1: the SMB row says the client serves smb and smbs.
- Actual: 1, 1: the row says disabled while the composition serves them.

## Re-audits

## Log

- 2026-11-16: filed proposed.
