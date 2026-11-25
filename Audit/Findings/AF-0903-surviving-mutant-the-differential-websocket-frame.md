---
id: AF-0903
title: Surviving mutant: the differential WebSocket frame builder can encode a 126-byte payload in the short form
auditor: quality
severity: Medium
status: closed
reason: Re-audit 2026-11-20_0456.md: the reproduction no longer reproduces.
key: quality:Curl.Conformance.Differential.UnitLibrary/Libcurl/WebSocket/LibcurlWebSocketFrames.cs:Frame-lt:surviving-mutant
reproduction: none
task: BL-5591
tasks: BL-5591
found: 2026-11-19
found-at: 8469990748da6c83b0388ea33dec77d81d9255ae
scorecard: 2026-11-19_1633.md
duplicate-of:
closed: 2026-11-20
closed-how: reliable-reaudit
closed-by: 2026-11-20_0456.md
---
# AF-0903 - Surviving mutant: the differential WebSocket frame builder can encode a 126-byte payload in the short form

## Summary

Medium finding from the quality auditor at `Curl.Conformance.Differential.UnitLibrary/Libcurl/WebSocket/LibcurlWebSocketFrames.cs:35`: Surviving mutant: the differential WebSocket frame builder can encode a 126-byte payload in the short form.

## Evidence

Location: `Curl.Conformance.Differential.UnitLibrary/Libcurl/WebSocket/LibcurlWebSocketFrames.cs:35`

Mutant payload.Length < ShortLengthLimit -> <= survived. RFC 6455 reserves length 126 as the 16-bit extended-length marker. Mutated, a 126-byte payload is written with length byte 126 and no extended length, which is a malformed frame sent to both libcurls. No test builds a frame at the 126-byte boundary.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Conformance.Differential.UnitLibrary/Libcurl/WebSocket/LibcurlWebSocketFrames.cs:35:< -Member Frame -ExcludeBaselineFailures -TimeoutSeconds 240
```

- Expected: killed
- Actual: survived

## Re-audits

- 2026-11-20 | 2026-11-20_0456.md | reproduces: no | Ran the -Site command: LibcurlWebSocketFrames.cs:35 '<' (payload.Length < ShortLengthLimit -> <=) in Frame, resolvedLine 35, outcome killed.

## Log

- 2026-11-19: filed proposed.
- 2026-11-20: accepted -> closed. Re-audit 2026-11-20_0456.md: the reproduction no longer reproduces.
