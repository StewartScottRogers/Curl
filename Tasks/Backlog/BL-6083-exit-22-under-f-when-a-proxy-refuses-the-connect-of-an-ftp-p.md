---
id: BL-6083
title: Exit 22 under -f when a proxy refuses the CONNECT of an ftp -p tunnel
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-6049]
touches: [Curl.Original.Protocol.Ftp.UnitLibrary/FtpSession.cs, Curl.Original.Protocol.Ftp.UnitLibrary/FtpProtocolHandler.cs, Curl.Original.Protocol.Ftp.UnitTests/FtpProtocolHandlerProxyConnectFailTests.cs]
requirement: none
created: 2026-11-27
completed:
---
# BL-6083 — Exit 22 under -f when a proxy refuses the CONNECT of an ftp -p tunnel

## Goal

`curl -f -p -x <proxy> ftp://...` through a proxy that refuses the CONNECT ends as real curl does: exit 22.

## Context

Found while finishing BL-6049. Measured with `Record-CurlExchange.ps1` (`-sS -v -f -p -x http://127.0.0.1:P
ftp://example.test/x`, reply `403`, `Content-Length: 0`): reference curl 8.22.0 writes
`* The requested URL returned error: 403`, `* closing connection #0`,
`curl: (22) The requested URL returned error: 403`; Curl writes `* CONNECT tunnel failed, response 403` and
`curl: (7) CONNECT tunnel failed, response 403`. The FTP handler does not set
`ConnectTarget.FailsOnHttpErrorStatus` (ADR-1467) and does not turn the failed connect into exit 22; see
`HttpProtocolHandler.FailedConnectResult` (STD-125, STD-227, ADR-1465) for HTTP's rule, the `407` text included.
Other protocols that tunnel through a proxy were not measured: measure them and file what differs.

## Acceptance criteria

- [ ] For the measured command line, the `*` lines after the reply head, the error line and the exit code match reference curl.
- [ ] Without `-f` the exit code stays 7 with `CONNECT tunnel failed, response 403`.
- [ ] A focused unit test in `Curl.Original.Protocol.Ftp.UnitTests` pins both, `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-27: Created.
