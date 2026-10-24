---
id: BL-3446
title: Fix AF-0399: SocksServerConnection.FindSocks4End's DSTIP - 1 < 255 can become + 1 with no test failing, so the SOCKS4a stand-in misreads 0.0.0.0 and 0.0.0.254-255
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitLibrary]
requirement: none
created: 2026-10-24
completed:
---
# BL-3446 — Fix AF-0399: SocksServerConnection.FindSocks4End's DSTIP - 1 < 255 can become + 1 with no test failing, so the SOCKS4a stand-in misreads 0.0.0.0 and 0.0.0.254-255

## Goal

The defect the audit office reported as AF-0399 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0399 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0399-socksserverconnection-findsocks4end-s-dstip-1-255.md`.

Location: `Curl.Conformance.UnitLibrary/SocksServerConnection.cs:106`

Location: `Curl.Conformance.UnitLibrary/SocksServerConnection.cs:106`

Sampled mutant (seed 0) survived: 'ReadUInt32BigEndian(request.AsSpan(4)) - 1 < 255' -> '... + 1 < 255'. The original is true exactly for DSTIP 0.0.0.1-0.0.0.255 (SOCKS4a's 'a host name follows'); the mutant is true for 0.0.0.0-0.0.0.253, so a plain SOCKS4 request to 0.0.0.0 waits for a host name that never comes and a SOCKS4a request with 0.0.0.254 or 0.0.0.255 is answered without reading its host. No test sends those boundary addresses. (The tool names the member 'static' because FindSocks4End returns a tuple.)

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Conformance.UnitLibrary/SocksServerConnection.cs:106:-1 -Member static -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-24: Created.
