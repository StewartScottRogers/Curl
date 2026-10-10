---
id: AF-0138
title: HaproxyProtocolHeader.Build says it returns the line's ASCII bytes but encodes the PROXY line as UTF-8
auditor: truthfulness
severity: Low
status: closed
reason: Re-audit 2026-10-10_0123.md: the reproduction no longer reproduces.
key: truthfulness:Curl.Networking.UnitLibrary/HaproxyProtocolHeader.cs:Build:false-doc-comment
reproduction: none
task: BL-1890
tasks: BL-1890
found: 2026-10-09
found-at: 71f3acef7ec0d988d2d6b5d967a7b7300156cf44
scorecard: 2026-10-09_0647.md
duplicate-of:
closed: 2026-10-10
closed-how: reliable-reaudit
closed-by: 2026-10-10_0123.md
---
# AF-0138 - HaproxyProtocolHeader.Build says it returns the line's ASCII bytes but encodes the PROXY line as UTF-8

## Summary

Low finding from the truthfulness auditor at `Curl.Networking.UnitLibrary/HaproxyProtocolHeader.cs:30`: HaproxyProtocolHeader.Build says it returns the line's ASCII bytes but encodes the PROXY line as UTF-8. Reported by an auditor flagged unreliable in 2026-10-09_0647.md.

## Evidence

Location: `Curl.Networking.UnitLibrary/HaproxyProtocolHeader.cs:30`

Line 30: '/// <returns>The line''s ASCII bytes, ending in CRLF.</returns>'. Line 42: 'return Encoding.UTF8.GetBytes($"PROXY {family} {source} {destination} ...\r\n");'. Only the UNKNOWN line (line 37) uses Encoding.ASCII. The type doc says a --haproxy-clientip value is sent 'verbatim and unvalidated', so a non-ASCII client IP is written as multi-byte UTF-8, not as ASCII.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Networking.UnitLibrary/HaproxyProtocolHeader.cs -SimpleMatch 'ASCII bytes','Encoding.UTF8.GetBytes'
```

- Expected: The return doc names the encoding the code uses (or the code encodes as ASCII).
- Actual: Line 30 says 'ASCII bytes'; line 42 uses Encoding.UTF8.GetBytes.

## Re-audits

- 2026-10-09 | 2026-10-09_1435.md | reproduces: no | Ran the reproduction: 'ASCII bytes' no longer matches in Curl.Networking.UnitLibrary/HaproxyProtocolHeader.cs. Line 42 still uses Encoding.UTF8.GetBytes, and the doc (line 30) now says 'The line's UTF-8 bytes (plain ASCII unless a --haproxy-clientip value is not), ending in CRLF'. That agrees with the code.
- 2026-10-10 | 2026-10-10_0123.md | reproduces: no | 'ASCII bytes' no longer matches. HaproxyProtocolHeader.Build's <returns> now reads 'The line's UTF-8 bytes (plain ASCII unless a --haproxy-clientip value is not), ending in CRLF', matching Encoding.UTF8.GetBytes at line 42.

## Log

- 2026-10-09: filed proposed.
- 2026-10-09: proposed -> accepted.
- 2026-10-10: accepted -> closed. Re-audit 2026-10-10_0123.md: the reproduction no longer reproduces.
