---
id: AF-0724
title: Surviving mutant: --ws-repl can wait forever on a response that begins with an empty line
auditor: quality
severity: Medium
status: closed
reason: Re-audit 2026-11-13_2321.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
key: quality:Curl.Extension.WebSocketRepl.UnitLibrary/Handshake/WebSocketOpeningHandshake.cs:PerformAsync-lt:surviving-mutant
reproduction: mutation Curl.Extension.WebSocketRepl.UnitLibrary/Handshake/WebSocketOpeningHandshake.cs:35:<
task: BL-5083
tasks: BL-5083
found: 2026-11-12
found-at: acc2af773fb64825b3b57d9470bb6f96176fb127
scorecard: 2026-11-12_2105.md
duplicate-of:
closed: 2026-11-13
closed-how: mechanical
closed-by: 2026-11-13_2321.md
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

- 2026-11-13 | 2026-11-13_2321.md | reproduces: no | Ran the -Site command: outcome killed (IndexOf(CRLFCRLF) < 0 -> <= 0). Runner's targeted mutation rerun on the clean audited commit: killed.

## Log

- 2026-11-12: filed proposed.
- 2026-11-13: proposed -> accepted.
- 2026-11-13: accepted -> closed. Re-audit 2026-11-13_2321.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
