---
id: BL-2821
title: Close GF-0096: A request sent through a CONNECT tunnel the proxy already closed fails with 'Recv failure: Connection was reset' where curl prints 'Connection was aborted'
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests]
requirement: none
created: 2026-10-19
completed:
---
# BL-2821 — Close GF-0096: A request sent through a CONNECT tunnel the proxy already closed fails with 'Recv failure: Connection was reset' where curl prints 'Connection was aborted'

## Goal

Curl behaves as curl 8.22.0 does for every item of gap finding GF-0096 (A request sent through a CONNECT tunnel the proxy already closed fails with 'Recv failure: Connection was reset' where curl prints 'Connection was aborted'), so a later gap analysis measures each of `behaviour:test1802` as `match`.

## Context

- Finding: GF-0096, filed by the gap analysis office (ADR-0433).
- Area: behaviour. Severity: Medium. Introduced in: not stated upstream.
- Platform: windows, measured against the Schannel curl of Git for Windows.
- Items: `behaviour:test1802`.
- Targeted curl version: 8.22.0 (Gap/Baselines/target.json).
- Yardstick: `tests/data/test*` in the curl 8.22.0 release tarball.
- The gap closes only when a later gap analysis re-measures every item as `match` (ADR-0433 decision 5), never because this task reaches Done.

Evidence, copied from the finding:

behaviour:test1802 (HTTP CONNECT with custom headers for proxy and server; keywords HTTP, HTTP CONNECT) expected 'reference curl exits 56; stdout 0 bytes: ', actual 'stderr differs'; evidence 'was match; in-process and out-of-process differ'. The cross-check fixtures (C:/curl-crosscheck/recorded/1802) show the exit code (56), the request bytes (the CONNECT only) and stdout (empty) agree; only stderr differs: reference 'curl: (56) Recv failure: Connection was aborted', Curl 'curl: (56) Recv failure: Connection was reset'. Rerun on loopback: reference exit 56 'Connection was aborted' in 9 of 11 runs (once exit 1 'Received HTTP/0.9 when not allowed', once exit 43 'Invalid response header', when it read the 6 leftover bytes before the RST); Curl exit 56 'Connection was reset' in 6 of 6. The proxy answers the CONNECT with 200, 'Connection: close' and the 6 bytes '-foo-<LF>' and closes; both binaries ignore the Content-Length, send the GET into the closed tunnel, the peer answers it with an RST, and the next receive fails. Reproduce from the repository root: $r = ('HTTP/1.1 200 OK','Date: Tue, 09 Nov 2010 14:49:00 GMT','Accept-Ranges: bytes','Content-Length: 6','Connection: close','Content-Type: text/html','','') -join ([char]13+[char]10) + '-foo-' + [char]10; foreach ($c in 'C:/Program Files/Git/ucrt64/bin/curl.exe',(Resolve-Path ./Curl.Console/bin/Release/net10.0/curl.exe).Path) { ./Record-CurlExchange.ps1 -Port 18997 -Response $r -Connections 20 -RequestReadMilliseconds 3000 -Curl $c -OutDirectory (Join-Path $env:TEMP ('t1802-' + (Split-Path (Split-Path $c) -Leaf))) -CurlArgs @('http://hello/wanted/page','-p','-x','127.0.0.1:18997','--header','User-Agent: myapp/1.0','--proxy-header','User-Agent: Benjamin/2','--proxy-header','Host: todeloo','--header','Host: foo') }; then compare the two stderr.txt files (reference in t1802-bin, Curl in t1802-net10.0).

Suggestion, copied from the finding:

In Curl.Networking.UnitLibrary, StreamConnection.ReadAsync rewrites every Windows read that fails with SocketError.ConnectionAborted as SocketError.ConnectionReset (ReportsAbortedReadAsReset). Narrow that rewrite to the case it was measured for: a peer that resets a connection it never half-closed (RST arriving just after the request). When the peer had already closed its side (FIN seen, as StreamConnection.HasPeerClosed detects) before Curl wrote the request, and the RST is the answer to that write, keep the WSAECONNABORTED so CurlSocketErrorText.ReceiveFailure prints curl's 'Recv failure: Connection was aborted'. For example, record HasPeerClosed in WriteAsync before writing, and skip the rewrite on the next failed read when it was true. Re-measure both shapes (upstream test1802's closed CONNECT tunnel, and BL-1450's reset-after-accept URLs) with Record-CurlExchange.ps1 before pinning, and add focused unit tests in Curl.Networking.UnitTests (StreamConnection) for both (ADR-0482). Explained by ADR-0419: the aborted-to-reset rewrite was decided for a reset right after accept; it does not cover a peer that closed before the request, where the Schannel curl prints 'aborted' in 9 of 11 runs; the ADR should be amended with the narrower rule.

## Acceptance criteria

- [ ] `behaviour:test1802`: Curl answers what curl 8.22.0 answers, `reference curl exits 56; stdout 0 bytes: `, so the item measures `match`.
- [ ] `dotnet build -warnaserror` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) are green.
- [ ] When an option is added or changed, `curl --ai-help` is kept right (CLAUDE.md).

## Notes

## Log

- 2026-10-19: Created.
- 2026-10-19: Backlog -> Doing. Criteria at claim: 9d695601 1fd5de8c 28346828.
