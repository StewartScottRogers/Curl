---
id: AF-0769
title: Surviving mutant: nats://host:0/ can be sent to the default port 4222
auditor: quality
severity: Medium
status: proposed
reason:
key: quality:Curl.Extended.Protocol.Nats.UnitLibrary/NatsProtocolHandler.cs:TransferAsync-ge:surviving-mutant
reproduction: mutation Curl.Extended.Protocol.Nats.UnitLibrary/NatsProtocolHandler.cs:57:>=
task: none
tasks:
found: 2026-11-14
found-at: 92f6fc980babe08d1ebff5f02d7e076e42c67fd7
scorecard: 2026-11-14_1801.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0769 - Surviving mutant: nats://host:0/ can be sent to the default port 4222

## Summary

Medium finding from the quality auditor at `Curl.Extended.Protocol.Nats.UnitLibrary/NatsProtocolHandler.cs:57`: Surviving mutant: nats://host:0/ can be sent to the default port 4222. Reported by an auditor flagged unreliable in 2026-11-14_1801.md.

## Evidence

Location: `Curl.Extended.Protocol.Nats.UnitLibrary/NatsProtocolHandler.cs:57`

Mutant 'url.Port >= 0 ? url.Port : DefaultPort' -> 'url.Port > 0' survived. ExecuteAsync_TheScheme_ChoosesTheDefaultPortAndTls covers no port (4222) and :4443, never an explicit :0. Under the mutant, an explicit port 0 dials 4222 instead of the port the URL names, which changes the connect target.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Extended.Protocol.Nats.UnitLibrary/NatsProtocolHandler.cs:57:>= -Member TransferAsync -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

## Log

- 2026-11-14: filed proposed.
