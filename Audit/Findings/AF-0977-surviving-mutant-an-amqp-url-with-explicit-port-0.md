---
id: AF-0977
title: Surviving mutant: an amqp:// URL with explicit port 0 can be dialled on the default port 5672 (>= -> >)
auditor: quality
severity: High
status: proposed
reason:
key: quality:Curl.Extended.Protocol.Amqp0.UnitLibrary/Client/Amqp0ProtocolHandler.cs:TargetOf-ge:surviving-mutant
reproduction: mutation Curl.Extended.Protocol.Amqp0.UnitLibrary/Client/Amqp0ProtocolHandler.cs:41:>=
task: none
tasks:
found: 2026-11-22
found-at: fdfc7d13ff07752cdeb7bf999a56dd653660b7a2
scorecard: 2026-11-22_1351.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0977 - Surviving mutant: an amqp:// URL with explicit port 0 can be dialled on the default port 5672 (>= -> >)

## Summary

High finding from the quality auditor at `Curl.Extended.Protocol.Amqp0.UnitLibrary/Client/Amqp0ProtocolHandler.cs:41`: Surviving mutant: an amqp:// URL with explicit port 0 can be dialled on the default port 5672 (>= -> >).

## Evidence

Location: `Curl.Extended.Protocol.Amqp0.UnitLibrary/Client/Amqp0ProtocolHandler.cs:41`

Invoke-MutationTest -Library Curl.Extended.Protocol.Amqp0.UnitLibrary -MaxMutants 25 -Seed 891023: survived. Mutated 'int port = url.Port >= 0 ? url.Port : useTls ? SecureDefaultPort : DefaultPort;' to 'url.Port > 0'. CurlUrl accepts port 0 (CurlUrlAuthority.TryParsePort rejects only values past MaximumPort), so amqp://h:0/q/x would connect to 5672 (or 5671) instead of port 0, reaching a different server. ExecuteAsync_Scheme_ConnectsWithTlsAndPortFromConnector has no port-0 row.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Extended.Protocol.Amqp0.UnitLibrary/Client/Amqp0ProtocolHandler.cs:41:>= -Member TargetOf -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

## Re-audits

## Log

- 2026-11-22: filed proposed.
