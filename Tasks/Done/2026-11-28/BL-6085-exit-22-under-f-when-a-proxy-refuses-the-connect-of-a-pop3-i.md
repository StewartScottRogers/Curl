---
id: BL-6085
title: Exit 22 under -f when a proxy refuses the CONNECT of a pop3, imap or smtp tunnel
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-6083]
touches: [Curl.Original.Protocol.Pop3.UnitLibrary/Pop3ProtocolHandler.cs, Curl.Original.Protocol.Pop3.UnitTests/Pop3ProtocolHandlerProxyConnectFailTests.cs, Curl.Original.Protocol.Imap.UnitLibrary/ImapProtocolHandler.cs, Curl.Original.Protocol.Imap.UnitTests/ImapProtocolHandlerProxyConnectFailTests.cs, Curl.Original.Protocol.Smtp.UnitLibrary/SmtpProtocolHandler.cs, Curl.Original.Protocol.Smtp.UnitTests/SmtpProtocolHandlerProxyConnectFailTests.cs]
requirement: none
created: 2026-11-27
completed: 2026-11-27
---
# BL-6085 — Exit 22 under -f when a proxy refuses the CONNECT of a pop3, imap or smtp tunnel

## Goal

`curl -f -p -x <proxy>` for pop3://example.test/1, imap://example.test/INBOX and smtp://example.test/x through a proxy that refuses the CONNECT ends as real curl does: exit 22.

## Context

Measured while doing BL-6083 with `Record-CurlExchange.ps1` (`-sS -v -p -f -x http://127.0.0.1:P <url>`, reply `403`, `Content-Length: 0`): reference curl 8.22.0 writes `* The requested URL returned error: 403`, `* closing connection #0`, `curl: (22) The requested URL returned error: 403`; Curl writes `* CONNECT tunnel failed, response 403` and `curl: (7) CONNECT tunnel failed, response 403`, with no `closing connection` line. BL-6083 did this for FTP: set `ConnectTarget.FailsOnHttpErrorStatus` and map the failed connect (see `FtpProtocolHandler.FailedConnectResult`, the `407` text included). Measure without `-f` too.

## Acceptance criteria

- [x] For each of the three URLs, the `*` lines after the reply head, the error line and the exit code match reference curl, with and without `-f`.
- [x] A focused unit test in each protocol's `.UnitTests` project pins both, `dotnet build` is clean and the fast tests pass.

## Notes

- Measured reference curl 8.22.0 with `Record-CurlExchange.ps1` for the three URLs: `403` with and without `-f`, `407` with `-f` (with and without `-U`), `--fail-with-body`, and `smtps://`. Built Curl matches every `-v` line from the reply head on, the error line and the exit code in all ten cases.
- Each handler now sets `ConnectTarget.FailsOnHttpErrorStatus` under `-f` and maps a refused CONNECT (400 or above) to exit 22, as FTP and HTTP do (ADR-1465, ADR-1467). No new decision, so no ADR. IMAP's `ConnectOnlyAsync` shares the rule, as FTP's connect-only path does.
- Reference curl also writes `* closing connection #0` after a refused TCP connect and an unresolved host for all three schemes (measured), so the line is written for every failed connect.
- The mapping is repeated in the three handlers because protocol libraries never reference each other.

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing. Criteria at claim: 0a75ad6c 3296538f.
- 2026-11-27: Doing -> Done. curl -f -p -x <proxy> for pop3, imap and smtp ends exit 22 on a refused CONNECT, with curl's -v lines.
