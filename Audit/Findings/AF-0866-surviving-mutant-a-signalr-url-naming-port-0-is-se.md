---
id: AF-0866
title: Surviving mutant: a SignalR URL naming port 0 is sent to the default port
auditor: quality
severity: Medium
status: closed
reason: Re-audit 2026-11-19_0134.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
key: quality:Curl.Extended.Protocol.SignalR.UnitLibrary/Client/SignalRProtocolHandler.cs:EndpointUrlOf-ge:surviving-mutant
reproduction: mutation Curl.Extended.Protocol.SignalR.UnitLibrary/Client/SignalRProtocolHandler.cs:60:>=
task: BL-5508
tasks: BL-5508
found: 2026-11-18
found-at: 4f902456d88c3fda856546dbe0c9a7674b1f04c4
scorecard: 2026-11-18_0810.md
duplicate-of:
closed: 2026-11-19
closed-how: mechanical
closed-by: 2026-11-19_0134.md
---
# AF-0866 - Surviving mutant: a SignalR URL naming port 0 is sent to the default port

## Summary

Medium finding from the quality auditor at `Curl.Extended.Protocol.SignalR.UnitLibrary/Client/SignalRProtocolHandler.cs:60`: Surviving mutant: a SignalR URL naming port 0 is sent to the default port. Reported by an auditor flagged unreliable in 2026-11-18_0810.md.

## Evidence

Location: `Curl.Extended.Protocol.SignalR.UnitLibrary/Client/SignalRProtocolHandler.cs:60`

Mutant 'int port = url.Port >= 0 ? url.Port : ...' -> 'url.Port > 0' survived Curl.Extended.Protocol.SignalR.UnitTests. A URL that names port 0 would then connect to 80 or 443 instead of the port given, changing the connect target. EndpointUrlOf_MapsTheSchemeToHttpAndKeepsPortPathAndQuery has no port-0 row.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Extended.Protocol.SignalR.UnitLibrary/Client/SignalRProtocolHandler.cs:60:>= -Member EndpointUrlOf -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

- 2026-11-19 | 2026-11-19_0134.md | reproduces: no | Targeted run Curl.Extended.Protocol.SignalR.UnitLibrary/Client/SignalRProtocolHandler.cs:60:>= (resolvedLine 60) gave outcome killed. This is the rerun result after a worktree-name collision. Runner's targeted mutation rerun on the clean audited commit: killed.

## Log

- 2026-11-18: filed proposed.
- 2026-11-19: accepted -> closed. Re-audit 2026-11-19_0134.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
