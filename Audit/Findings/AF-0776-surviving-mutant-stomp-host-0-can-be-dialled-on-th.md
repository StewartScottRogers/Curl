---
id: AF-0776
title: Surviving mutant: stomp://host:0/ can be dialled on the default port 61613 instead of port 0
auditor: quality
severity: High
status: closed
reason: Re-audit 2026-11-15_1159.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
key: quality:Curl.Extended.Protocol.Stomp.UnitLibrary/Client/StompProtocolHandler.cs:TargetOf-ge:surviving-mutant
reproduction: mutation Curl.Extended.Protocol.Stomp.UnitLibrary/Client/StompProtocolHandler.cs:44:>=
task: BL-5227
tasks: BL-5227
found: 2026-11-15
found-at: d04b275ae7a1546a4d0feed894da1ba520945cf5
scorecard: 2026-11-15_0222.md
duplicate-of:
closed: 2026-11-15
closed-how: mechanical
closed-by: 2026-11-15_1159.md
---
# AF-0776 - Surviving mutant: stomp://host:0/ can be dialled on the default port 61613 instead of port 0

## Summary

High finding from the quality auditor at `Curl.Extended.Protocol.Stomp.UnitLibrary/Client/StompProtocolHandler.cs:44`: Surviving mutant: stomp://host:0/ can be dialled on the default port 61613 instead of port 0.

## Evidence

Location: `Curl.Extended.Protocol.Stomp.UnitLibrary/Client/StompProtocolHandler.cs:44`

int port = url.Port >= 0 ? url.Port : useTls ? SecureDefaultPort : DefaultPort; mutated to url.Port > 0 survived (seed 68118 sample). With the mutant, an explicit port 0 is replaced by the scheme default, so the transfer connects somewhere the user did not name instead of failing to connect (exit 7). ExecuteAsync_Url_ConnectsToItsPortWithTlsForStomps has no port-0 row. Same pattern as AF-0769 and AF-0774 in NATS and WebTransport.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Extended.Protocol.Stomp.UnitLibrary/Client/StompProtocolHandler.cs:44:>= -Member TargetOf -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

- 2026-11-15 | 2026-11-15_1159.md | reproduces: no | Ran the -Site reproduction: StompProtocolHandler.cs:44 >= resolved at line 44, outcome killed. Runner's targeted mutation rerun on the clean audited commit: killed.

## Log

- 2026-11-15: filed proposed.
- 2026-11-15: accepted -> closed. Re-audit 2026-11-15_1159.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
