---
id: BL-1455
title: Write the -v line Proxy CONNECT aborted when the proxy cuts a CONNECT reply head short
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests]
requirement: FR-091
created: 2026-10-04
completed: 2026-10-07
---
# BL-1455 — Write the -v line Proxy CONNECT aborted when the proxy cuts a CONNECT reply head short

## Goal

When the proxy closes or resets the connection after part of a CONNECT reply head, Curl's -v output carries curl 8.21.0's * Proxy CONNECT aborted line before * closing connection #0, as real curl's does.

## Context

- Measured 2026-10-04 (BL-1449), Windows: Record-CurlExchange.ps1 -ResetAfterResponse -Response @('HTTP/1.1 200 OK\r\n') -CurlArgs @('-v','-m','5','-p','-x','http://127.0.0.1:<port>','http://example.test/'). Real curl writes * Proxy CONNECT aborted, * closing connection #0, curl: (56) Proxy CONNECT aborted; Curl.Console writes the last two only.
- ConnectTunnelVerboseLines.ReportReplyFailure (Curl.Networking.UnitLibrary) writes the failure line for exit 8 and, since BL-1449, for Recv failure: ...; HttpProxyTunnel.ReadReplyAsync returns Proxy CONNECT aborted for a cut-short head. Check whether real curl also writes the line for CONNECT response too large and Too large response headers before widening it to those.

## Acceptance criteria

- [x] A test in Curl.Networking.UnitTests pins * Proxy CONNECT aborted in the transcript of a CONNECT whose reply head is cut short.
- [x] Re-running the measurement above against Curl.Console gives the same three closing lines as real curl (recorded in Notes).
- [x] dotnet build is clean and dotnet test --filter "TestCategory!=Integration" passes; Measure-CodeQuality.ps1 -Library Curl.Networking.UnitLibrary reports no failing member.
## Notes

- Measured 2026-10-07 with curl 8.21.0 (Schannel) and Record-CurlExchange.ps1 as the proxy, `-v -m 5 -p -x http://127.0.0.1:41455 http://example.test/`: curl writes its failure as a `*` line before `* closing connection #0` in every reply-head failure, because `failf` also writes to the verbose trace:
  - reset after `HTTP/1.1 200 OK\r\n` (-ResetAfterResponse): `* Proxy CONNECT aborted`, `* closing connection #0`, `curl: (56) Proxy CONNECT aborted`;
  - clean close after that line, and a close before any byte: the same three lines;
  - a 17000-byte header line: `* CONNECT response too large`, then the same closing pair with that message;
  - 320 header lines of 1003 bytes: `* Too large response headers: 307547 > 307200`, then the same closing pair.
- So `ConnectTunnelVerboseLines.ReportReplyFailure` now writes any failed reply's message (exit 8's `Unsupported Content-Length value` and BL-1449's `Recv failure: ...` were already such messages); the `Recv failure: ` prefix check is gone.
- Re-measured against Curl.Console (Debug curl.exe) for the reset, clean-close, empty-close and too-long-line cases: the same closing three lines and exit 56; the reset case's `*`/`<`/`>` lines are identical to real curl's.
- Measure-CodeQuality.ps1 -Library Curl.Networking.UnitLibrary: 0 failing members.

## Log

- 2026-10-04: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. -v now writes * Proxy CONNECT aborted (and the too-large failures) before * closing connection #0, as curl does
