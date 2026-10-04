---
id: BL-1454
title: Write the -v line Proxy CONNECT aborted when the proxy cuts a CONNECT reply head short
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests]
requirement: FR-091
created: 2026-10-04
completed:
---
# BL-1454 — Write the -v line Proxy CONNECT aborted when the proxy cuts a CONNECT reply head short

## Goal

When the proxy closes or resets the connection after part of a CONNECT reply head, Curl's -v output carries curl 8.21.0's * Proxy CONNECT aborted line before * closing connection #0, as real curl's does.

## Context

- Measured 2026-10-04 (BL-1449), Windows: Record-CurlExchange.ps1 -ResetAfterResponse -Response @('HTTP/1.1 200 OK\r\n') -CurlArgs @('-v','-m','5','-p','-x','http://127.0.0.1:<port>','http://example.test/'). Real curl writes * Proxy CONNECT aborted, * closing connection #0, curl: (56) Proxy CONNECT aborted; Curl.Console writes the last two only.
- ConnectTunnelVerboseLines.ReportReplyFailure (Curl.Networking.UnitLibrary) writes the failure line for exit 8 and, since BL-1449, for Recv failure: ...; HttpProxyTunnel.ReadReplyAsync returns Proxy CONNECT aborted for a cut-short head. Check whether real curl also writes the line for CONNECT response too large and Too large response headers before widening it to those.

## Acceptance criteria

- [ ] A test in Curl.Networking.UnitTests pins * Proxy CONNECT aborted in the transcript of a CONNECT whose reply head is cut short.
- [ ] Re-running the measurement above against Curl.Console gives the same three closing lines as real curl (recorded in Notes).
- [ ] dotnet build is clean and dotnet test --filter "TestCategory!=Integration" passes; Measure-CodeQuality.ps1 -Library Curl.Networking.UnitLibrary reports no failing member.
## Notes

## Log

- 2026-10-04: Created.
