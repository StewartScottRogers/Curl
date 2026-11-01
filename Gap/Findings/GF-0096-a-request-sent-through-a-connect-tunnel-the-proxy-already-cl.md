---
id: GF-0096
title: A request sent through a CONNECT tunnel the proxy already closed fails with 'Recv failure: Connection was reset' where curl prints 'Connection was aborted'
area: behaviour
platform: windows
key: behaviour:aborted-read-after-peer-close-reported-as-reset
severity: Medium
status: closed
scope: target
introduced-in:
opened: 2026-10-19_0804
closed: 2026-10-19_1329
regression: false
items: [behaviour:test1802]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests]
task: BL-2821
tasks: [BL-2821]
---
# GF-0096 - A request sent through a CONNECT tunnel the proxy already closed fails with 'Recv failure: Connection was reset' where curl prints 'Connection was aborted'

## Summary

Curl differs from upstream curl in behaviour: A request sent through a CONNECT tunnel the proxy already closed fails with 'Recv failure: Connection was reset' where curl prints 'Connection was aborted'.

## Evidence

behaviour:test1802 (HTTP CONNECT with custom headers for proxy and server; keywords HTTP, HTTP CONNECT) expected 'reference curl exits 56; stdout 0 bytes: ', actual 'stderr differs'; evidence 'was match; in-process and out-of-process differ'. The cross-check fixtures (C:/curl-crosscheck/recorded/1802) show the exit code (56), the request bytes (the CONNECT only) and stdout (empty) agree; only stderr differs: reference 'curl: (56) Recv failure: Connection was aborted', Curl 'curl: (56) Recv failure: Connection was reset'. Rerun on loopback: reference exit 56 'Connection was aborted' in 9 of 11 runs (once exit 1 'Received HTTP/0.9 when not allowed', once exit 43 'Invalid response header', when it read the 6 leftover bytes before the RST); Curl exit 56 'Connection was reset' in 6 of 6. The proxy answers the CONNECT with 200, 'Connection: close' and the 6 bytes '-foo-<LF>' and closes; both binaries ignore the Content-Length, send the GET into the closed tunnel, the peer answers it with an RST, and the next receive fails. Reproduce from the repository root: $r = ('HTTP/1.1 200 OK','Date: Tue, 09 Nov 2010 14:49:00 GMT','Accept-Ranges: bytes','Content-Length: 6','Connection: close','Content-Type: text/html','','') -join ([char]13+[char]10) + '-foo-' + [char]10; foreach ($c in 'C:/Program Files/Git/ucrt64/bin/curl.exe',(Resolve-Path ./Curl.Console/bin/Release/net10.0/curl.exe).Path) { ./Record-CurlExchange.ps1 -Port 18997 -Response $r -Connections 20 -RequestReadMilliseconds 3000 -Curl $c -OutDirectory (Join-Path $env:TEMP ('t1802-' + (Split-Path (Split-Path $c) -Leaf))) -CurlArgs @('http://hello/wanted/page','-p','-x','127.0.0.1:18997','--header','User-Agent: myapp/1.0','--proxy-header','User-Agent: Benjamin/2','--proxy-header','Host: todeloo','--header','Host: foo') }; then compare the two stderr.txt files (reference in t1802-bin, Curl in t1802-net10.0).

## Suggestion

In Curl.Networking.UnitLibrary, StreamConnection.ReadAsync rewrites every Windows read that fails with SocketError.ConnectionAborted as SocketError.ConnectionReset (ReportsAbortedReadAsReset). Narrow that rewrite to the case it was measured for: a peer that resets a connection it never half-closed (RST arriving just after the request). When the peer had already closed its side (FIN seen, as StreamConnection.HasPeerClosed detects) before Curl wrote the request, and the RST is the answer to that write, keep the WSAECONNABORTED so CurlSocketErrorText.ReceiveFailure prints curl's 'Recv failure: Connection was aborted'. For example, record HasPeerClosed in WriteAsync before writing, and skip the rewrite on the next failed read when it was true. Re-measure both shapes (upstream test1802's closed CONNECT tunnel, and BL-1450's reset-after-accept URLs) with Record-CurlExchange.ps1 before pinning, and add focused unit tests in Curl.Networking.UnitTests (StreamConnection) for both (ADR-0482). Explained by ADR-0419: the aborted-to-reset rewrite was decided for a reset right after accept; it does not cover a peer that closed before the request, where the Schannel curl prints 'aborted' in 9 of 11 runs; the ADR should be amended with the narrower rule.

## Measurements

- 2026-10-19_0804: 1 of 1 items are gaps.
- 2026-10-19_1329: 0 of 1 items are gaps.
- 2026-10-22_0148: 0 of 1 items are gaps.
- 2026-10-23_1637: 0 of 1 items are gaps.
- 2026-10-25_1542: 0 of 1 items are gaps.
- 2026-10-30_2103: 0 of 1 items are gaps.
- 2026-10-31_1828: 0 of 1 items are gaps.

## Log

- 2026-10-19_0804: Opened by gap-behaviour.
- 2026-10-19_0927: Filed BL-2821.
- 2026-10-19_1329: Closed: run 2026-10-19_1329 measured every item as match or excluded on windows.
