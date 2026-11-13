---
id: AF-0723
title: Surviving mutant: --ws-repl can refuse a 101 status line with no reason phrase
auditor: quality
severity: High
status: proposed
reason:
key: quality:Curl.Extension.WebSocketRepl.UnitLibrary/Handshake/WebSocketHandshakeResponseValidator.cs:IsSwitchingProtocols-ge:surviving-mutant
reproduction: mutation Curl.Extension.WebSocketRepl.UnitLibrary/Handshake/WebSocketHandshakeResponseValidator.cs:30:>=
task: none
tasks:
found: 2026-11-12
found-at: acc2af773fb64825b3b57d9470bb6f96176fb127
scorecard: 2026-11-12_2105.md
duplicate-of:
closed:
closed-how:
closed-by:
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

## Log

- 2026-11-12: filed proposed.
