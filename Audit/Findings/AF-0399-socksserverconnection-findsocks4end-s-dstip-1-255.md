---
id: AF-0399
title: SocksServerConnection.FindSocks4End's DSTIP - 1 < 255 can become + 1 with no test failing, so the SOCKS4a stand-in misreads 0.0.0.0 and 0.0.0.254-255
auditor: quality
severity: Medium
status: closed
reason: Re-audit 2026-10-24_0845.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
key: quality:Curl.Conformance.UnitLibrary/SocksServerConnection.cs:static-minus1:surviving-mutant
reproduction: mutation Curl.Conformance.UnitLibrary/SocksServerConnection.cs:106:-1
task: BL-3446
tasks: BL-3446
found: 2026-10-23
found-at: 94da765e1b4eb040511b48ff68f7b42c5226e207
scorecard: 2026-10-23_2322.md
duplicate-of:
closed: 2026-10-24
closed-how: mechanical
closed-by: 2026-10-24_0845.md
---
# AF-0399 - SocksServerConnection.FindSocks4End's DSTIP - 1 < 255 can become + 1 with no test failing, so the SOCKS4a stand-in misreads 0.0.0.0 and 0.0.0.254-255

## Summary

Medium finding from the quality auditor at `Curl.Conformance.UnitLibrary/SocksServerConnection.cs:106`: SocksServerConnection.FindSocks4End's DSTIP - 1 < 255 can become + 1 with no test failing, so the SOCKS4a stand-in misreads 0.0.0.0 and 0.0.0.254-255. Reported by an auditor flagged unreliable in 2026-10-23_2322.md.

## Evidence

Location: `Curl.Conformance.UnitLibrary/SocksServerConnection.cs:106`

Sampled mutant (seed 0) survived: 'ReadUInt32BigEndian(request.AsSpan(4)) - 1 < 255' -> '... + 1 < 255'. The original is true exactly for DSTIP 0.0.0.1-0.0.0.255 (SOCKS4a's 'a host name follows'); the mutant is true for 0.0.0.0-0.0.0.253, so a plain SOCKS4 request to 0.0.0.0 waits for a host name that never comes and a SOCKS4a request with 0.0.0.254 or 0.0.0.255 is answered without reading its host. No test sends those boundary addresses. (The tool names the member 'static' because FindSocks4End returns a tuple.)

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Conformance.UnitLibrary/SocksServerConnection.cs:106:-1 -Member static -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

- 2026-10-24 | 2026-10-24_0845.md | reproduces: no | Ran the -Site Curl.Conformance.UnitLibrary/SocksServerConnection.cs:106:-1 run: resolvedLine 106, 'ReadUInt32BigEndian(request.AsSpan(4)) - 1 < 255' -> '+ 1 < 255', outcome killed. Runner's targeted mutation rerun on the clean audited commit: killed.

## Log

- 2026-10-23: filed proposed.
- 2026-10-24: proposed -> accepted.
- 2026-10-24: accepted -> closed. Re-audit 2026-10-24_0845.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
