---
id: BL-1890
title: Fix AF-0138: HaproxyProtocolHeader.Build says it returns the line's ASCII bytes but encodes the PROXY line as UTF-8
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Curl.Networking.UnitLibrary]
requirement: none
created: 2026-10-09
completed:
---
# BL-1890 — Fix AF-0138: HaproxyProtocolHeader.Build says it returns the line's ASCII bytes but encodes the PROXY line as UTF-8

## Goal

The defect the audit office reported as AF-0138 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0138 (Low, truthfulness auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0138-haproxyprotocolheader-build-says-it-returns-the-li.md`.

Location: `Curl.Networking.UnitLibrary/HaproxyProtocolHeader.cs:30`

Location: `Curl.Networking.UnitLibrary/HaproxyProtocolHeader.cs:30`

Line 30: '/// <returns>The line''s ASCII bytes, ending in CRLF.</returns>'. Line 42: 'return Encoding.UTF8.GetBytes($"PROXY {family} {source} {destination} ...\r\n");'. Only the UNKNOWN line (line 37) uses Encoding.ASCII. The type doc says a --haproxy-clientip value is sent 'verbatim and unvalidated', so a non-ASCII client IP is written as multi-byte UTF-8, not as ASCII.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Networking.UnitLibrary/HaproxyProtocolHeader.cs -SimpleMatch 'ASCII bytes','Encoding.UTF8.GetBytes'
```

- Expected: The return doc names the encoding the code uses (or the code encodes as ASCII).
- Actual: Line 30 says 'ASCII bytes'; line 42 uses Encoding.UTF8.GetBytes.

The finding closes only when a later re-audit by the truthfulness auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-09: Created.
