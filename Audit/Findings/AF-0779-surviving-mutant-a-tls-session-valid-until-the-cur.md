---
id: AF-0779
title: Surviving mutant: a TLS session valid until the current second can be dropped as expired
auditor: quality
severity: Medium
status: proposed
reason:
key: quality:Curl.Networking.UnitLibrary/TlsSessionCache.cs:RemoveExpired-lt:surviving-mutant
reproduction: mutation Curl.Networking.UnitLibrary/TlsSessionCache.cs:337:<
task: none
tasks:
found: 2026-11-15
found-at: d04b275ae7a1546a4d0feed894da1ba520945cf5
scorecard: 2026-11-15_0222.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0779 - Surviving mutant: a TLS session valid until the current second can be dropped as expired

## Summary

Medium finding from the quality auditor at `Curl.Networking.UnitLibrary/TlsSessionCache.cs:337`: Surviving mutant: a TLS session valid until the current second can be dropped as expired.

## Evidence

Location: `Curl.Networking.UnitLibrary/TlsSessionCache.cs:337`

peer.Sessions.RemoveAll(session => session.ValidUntil < now); mutated to <= survived (seed 68118 sample). The boundary of session expiry decides whether a session is resumed or a full handshake is made, and no test pins a session at its exact ValidUntil second.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Networking.UnitLibrary/TlsSessionCache.cs:337:< -Member RemoveExpired -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

## Log

- 2026-11-15: filed proposed.
