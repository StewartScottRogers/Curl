---
id: BL-6083
title: Exit 22 under -f when a proxy refuses the CONNECT of an ftp -p tunnel
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-6049]
touches: [Curl.Original.Protocol.Ftp.UnitLibrary/FtpSession.cs, Curl.Original.Protocol.Ftp.UnitLibrary/FtpProtocolHandler.cs, Curl.Original.Protocol.Ftp.UnitLibrary/FtpTransferMessages.cs, Curl.Original.Protocol.Ftp.UnitLibrary/CLAUDE.md, Curl.Original.Protocol.Ftp.UnitTests/FtpProtocolHandlerProxyConnectFailTests.cs]
requirement: none
created: 2026-11-27
completed: 2026-11-27
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

- [x] For the measured command line, the `*` lines after the reply head, the error line and the exit code match reference curl.
- [x] Without `-f` the exit code stays 7 with `CONNECT tunnel failed, response 403`.
- [x] A focused unit test in `Curl.Original.Protocol.Ftp.UnitTests` pins both, `dotnet build` is clean and the fast tests pass.

## Notes

- `touches` widened with `FtpTransferMessages.cs` (the two exit 22 texts belong with the other messages) and the library's `CLAUDE.md`; the one other task in Doing (BL-6081) names neither.
- The FTP handler now sets `ConnectTarget.FailsOnHttpErrorStatus` on the control and data targets under `-f` (not `--fail-with-body`) and maps a refused CONNECT (400 or above) to exit 22, as HTTP does (ADR-1465, ADR-1467). No new decision, so no ADR.
- Reference curl writes `* closing connection #0` after any control connect that fails (refused TCP and unresolved host measured too), so the handler now writes it for every failed control connect.
- Data tunnel, measured with a throwaway PowerShell proxy: a refused `EPSV` data CONNECT falls back to `PASV` (exit 13 when that fails too), `-f` or not; a refused `PASV` data CONNECT ends exit 22 under `-f`, exit 7 without. Curl matches both exit codes and error lines.
- All control-connection cases (`403`, `407`, `407` with `-U`, `--fail-with-body`, no `-f`) match reference curl line for line.
- `Measure-CodeQuality.ps1 -Library Curl.Original.Protocol.Ftp.UnitLibrary`: 0 failing members. `dotnet build` clean, fast tests green.
- Filed: BL-6084, BL-6085, BL-6086 (other protocols answer exit 7 under `-f`), BL-6087 (ldap ignores the proxy), BL-6088 (data connection `-v` lines through a proxy). `smb` could not be compared: the reference build has it disabled.

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing. Criteria at claim: f42e3136 88a33e86 a8c89593.
- 2026-11-27: Doing -> Done. curl -f -p -x <proxy> ftp:// ends exit 22 on a refused CONNECT, with curl's -v lines.
