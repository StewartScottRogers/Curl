---
id: AF-0724
title: Surviving mutant: --ws-repl can wait forever on a response that begins with an empty line
auditor: quality
severity: Medium
status: proposed
reason:
key: quality:Curl.Extension.WebSocketRepl.UnitLibrary/Handshake/WebSocketOpeningHandshake.cs:PerformAsync-lt:surviving-mutant
reproduction: mutation Curl.Extension.WebSocketRepl.UnitLibrary/Handshake/WebSocketOpeningHandshake.cs:35:<
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
# AF-0724 - Surviving mutant: --ws-repl can wait forever on a response that begins with an empty line

## Summary

Medium finding from the quality auditor at `Curl.Extension.WebSocketRepl.UnitLibrary/Handshake/WebSocketOpeningHandshake.cs:35`: Surviving mutant: --ws-repl can wait forever on a response that begins with an empty line. Reported by an auditor flagged unreliable in 2026-11-12_2105.md.

## Evidence

Location: `Curl.Extension.WebSocketRepl.UnitLibrary/Handshake/WebSocketOpeningHandshake.cs:35`

Mutant 'IndexOf("\r\n\r\n"u8)) < 0' -> '<= 0' survived. When the head end is found at offset 0 the mutant keeps reading. Because IndexOf finds offset 0 on every pass, it reads until the peer closes or the head limit is reached, so a server that sends CRLF CRLF and waits hangs the transfer where the original refuses it at once.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Extension.WebSocketRepl.UnitLibrary/Handshake/WebSocketOpeningHandshake.cs:35:< -Member PerformAsync -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

## Re-audits

## Log

- 2026-11-12: filed proposed.
