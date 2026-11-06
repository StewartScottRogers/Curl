---
id: AF-0699
title: Surviving mutant: every write through an HTTP/2 proxy tunnel can carry END_STREAM
auditor: quality
severity: High
status: accepted
reason: Accepted under Stewart's standing accept of 2026-11-02.
key: quality:Curl.Networking.UnitLibrary/Http2ProxyTunnelConnection.cs:WriteAsync-false:surviving-mutant
reproduction: mutation Curl.Networking.UnitLibrary/Http2ProxyTunnelConnection.cs:146:false
task: BL-4893
tasks: BL-4893
found: 2026-11-05
found-at: 4eba3c63638befcc6a63aaa31ddf5419922fa939
scorecard: 2026-11-05_2025.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0699 - Surviving mutant: every write through an HTTP/2 proxy tunnel can carry END_STREAM

## Summary

High finding from the quality auditor at `Curl.Networking.UnitLibrary/Http2ProxyTunnelConnection.cs:146`: Surviving mutant: every write through an HTTP/2 proxy tunnel can carry END_STREAM.

## Evidence

Location: `Curl.Networking.UnitLibrary/Http2ProxyTunnelConnection.cs:146`

'await _http2.WriteDataAsync(_streamId, buffer, isEndStream: false, cancellationToken)' mutated to 'isEndStream: true' survived (Networking sample, seed 982898). The tunnel's first DATA frame would close the stream half, so the proxy ends the tunnelled request after its first write. That changes the request bytes on the wire. No test checks the DATA frame flags written through the tunnel.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Networking.UnitLibrary/Http2ProxyTunnelConnection.cs:146:false -Member WriteAsync -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

## Re-audits

## Log

- 2026-11-05: filed proposed.
