---
id: AF-0846
title: Surviving mutant: the Socket.IO upgrade Host header's port boundary is untested
auditor: quality
severity: Medium
status: proposed
reason:
key: quality:Curl.Extended.Protocol.SocketIo.UnitLibrary/SocketIoUpgrade.cs:Request-lt:surviving-mutant
reproduction: mutation Curl.Extended.Protocol.SocketIo.UnitLibrary/SocketIoUpgrade.cs:36:<
task: none
tasks:
found: 2026-11-17
found-at: 10c240a4859c7868ec16b722cde6234f80cc8fcb
scorecard: 2026-11-17_0430.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0846 - Surviving mutant: the Socket.IO upgrade Host header's port boundary is untested

## Summary

Medium finding from the quality auditor at `Curl.Extended.Protocol.SocketIo.UnitLibrary/SocketIoUpgrade.cs:36`: Surviving mutant: the Socket.IO upgrade Host header's port boundary is untested.

## Evidence

Location: `Curl.Extended.Protocol.SocketIo.UnitLibrary/SocketIoUpgrade.cs:36`

url.Port < 0 -> url.Port <= 0 survived. A URL with an explicit port 0 would lose ':0' from the request's Host header (request bytes). Request_AsksForTheWebSocketTransport pins only a URL without a port.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Extended.Protocol.SocketIo.UnitLibrary/SocketIoUpgrade.cs:36:< -Member Request -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

## Re-audits

## Log

- 2026-11-17: filed proposed.
