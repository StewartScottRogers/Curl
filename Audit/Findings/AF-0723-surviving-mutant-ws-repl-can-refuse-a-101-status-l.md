---
id: AF-0723
title: Surviving mutant: --ws-repl can refuse a 101 status line with no reason phrase
auditor: quality
severity: High
status: closed
reason: Re-audit 2026-11-13_2321.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
key: quality:Curl.Extension.WebSocketRepl.UnitLibrary/Handshake/WebSocketHandshakeResponseValidator.cs:IsSwitchingProtocols-ge:surviving-mutant
reproduction: mutation Curl.Extension.WebSocketRepl.UnitLibrary/Handshake/WebSocketHandshakeResponseValidator.cs:30:>=
task: BL-5082
tasks: BL-5082
found: 2026-11-12
found-at: acc2af773fb64825b3b57d9470bb6f96176fb127
scorecard: 2026-11-12_2105.md
duplicate-of:
closed: 2026-11-13
closed-how: mechanical
closed-by: 2026-11-13_2321.md
---
# AF-0723 - Surviving mutant: --ws-repl can refuse a 101 status line with no reason phrase

## Summary

High finding from the quality auditor at `Curl.Extension.WebSocketRepl.UnitLibrary/Handshake/WebSocketHandshakeResponseValidator.cs:30`: Surviving mutant: --ws-repl can refuse a 101 status line with no reason phrase. Reported by an auditor flagged unreliable in 2026-11-12_2105.md.

## Evidence

Location: `Curl.Extension.WebSocketRepl.UnitLibrary/Handshake/WebSocketHandshakeResponseValidator.cs:30`

Mutant 'parts.Length >= 2' -> '> 2' survived. 'HTTP/1.1 101' split on ' ' gives two parts, so the mutant refuses a valid upgrade (exit WeirdServerReply, 'Refused WebSocket upgrade'). Every test status line carries 'Switching Protocols'.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Extension.WebSocketRepl.UnitLibrary/Handshake/WebSocketHandshakeResponseValidator.cs:30:>= -Member IsSwitchingProtocols -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

## Re-audits

- 2026-11-13 | 2026-11-13_2321.md | reproduces: no | Ran the -Site command: outcome killed (parts.Length >= 2 -> > 2). Runner's targeted mutation rerun on the clean audited commit: killed.

## Log

- 2026-11-12: filed proposed.
- 2026-11-13: proposed -> accepted.
- 2026-11-13: accepted -> closed. Re-audit 2026-11-13_2321.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
