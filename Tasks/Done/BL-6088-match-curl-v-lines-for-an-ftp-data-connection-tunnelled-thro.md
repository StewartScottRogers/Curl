---
id: BL-6088
title: Match curl -v lines for an FTP data connection tunnelled through a -p proxy
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-6083]
touches: [Curl.Original.Protocol.Ftp.UnitLibrary/FtpSession.cs, Curl.Original.Protocol.Ftp.UnitLibrary/FtpDataConnectEvents.cs, Curl.Original.Protocol.Ftp.UnitTests/FtpProtocolHandlerProxyDataLineTests.cs, Curl.Original.Protocol.Ftp.UnitTests/FtpProtocolHandlerDataConnectFailureTests.cs]
requirement: none
created: 2026-11-27
completed: 2026-11-27
---
# BL-6088 — Match curl -v lines for an FTP data connection tunnelled through a -p proxy

## Goal

The `-v` lines of an `ftp -p` data connection through a proxy match real curl, its failure included.

## Context

Measured while doing BL-6083 with a PowerShell loopback proxy that answers the control CONNECT `200`, speaks FTP (`EPSV` `500`, `PASV` `227`) and refuses the data CONNECT `403`: reference curl 8.22.0 writes `* Connecting to example.test port 2000` and, after the refusal line, `* shutting down connection #0`; Curl writes `* Connecting to 127.0.0.1 port 2000` (the proxy's address) and no `shutting down` line. Exit codes and error lines already match.

## Acceptance criteria

- [x] For the measured exchange, with and without `-f`, Curl's `*` lines from `Connect data stream passively` to the end match reference curl.
- [x] A focused unit test pins them, `dotnet build` is clean and the fast tests pass.

## Notes

- Measured reference curl 8.22.0 against a throwaway PowerShell proxy at `localhost` (`-sS -v -p -x http://localhost:P ftp://example.test/x`: `PASV` data CONNECT refused with and without `-f`, `EPSV` then `PASV` refused, `--no-ftp-skip-pasv-ip`, and both tunnels opened). Curl's lines from `Connect data stream passively` now match in every case, port numbers aside. The one other difference, `sendrecv_dl: we are done` on success, is a debug-build trace line ADR-0782 excludes.
- Three fixes: through a proxy `Connecting to` names the URL's host, and `Established 2nd connection to` names the proxy's host; a failed last data dial writes `shutting down connection #0`. Reference curl writes that last line without a proxy too (measured with `Record-CurlExchange.ps1 -Ftp`, `PASV` naming port 1), so direct connections write it as well. Matching curl is no new decision, so no ADR.
- `touches` widened with `FtpProtocolHandlerDataConnectFailureTests.cs`, two of whose tests expected the failure to be the last line; no other task in Doing names it.
- `Measure-CodeQuality.ps1 -Library Curl.Original.Protocol.Ftp.UnitLibrary`: 0 failing members. `dotnet build` clean, fast tests green.

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing. Criteria at claim: 1961e71e 89b25f9e.
- 2026-11-27: Doing -> Done. ftp -p data connections through a proxy write curl's -v lines, failure included.
