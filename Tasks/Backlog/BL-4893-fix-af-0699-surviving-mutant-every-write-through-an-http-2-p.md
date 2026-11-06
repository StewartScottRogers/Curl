---
id: BL-4893
title: Fix AF-0699: Surviving mutant: every write through an HTTP/2 proxy tunnel can carry END_STREAM
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary]
requirement: none
created: 2026-11-06
completed:
---
# BL-4893 — Fix AF-0699: Surviving mutant: every write through an HTTP/2 proxy tunnel can carry END_STREAM

## Goal

The defect the audit office reported as AF-0699 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0699 (High, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0699-surviving-mutant-every-write-through-an-http-2-pro.md`.

Location: `Curl.Networking.UnitLibrary/Http2ProxyTunnelConnection.cs:146`

Location: `Curl.Networking.UnitLibrary/Http2ProxyTunnelConnection.cs:146`

'await _http2.WriteDataAsync(_streamId, buffer, isEndStream: false, cancellationToken)' mutated to 'isEndStream: true' survived (Networking sample, seed 982898). The tunnel's first DATA frame would close the stream half, so the proxy ends the tunnelled request after its first write. That changes the request bytes on the wire. No test checks the DATA frame flags written through the tunnel.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Networking.UnitLibrary/Http2ProxyTunnelConnection.cs:146:false -Member WriteAsync -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-06: Created.
