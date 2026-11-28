---
id: BL-6087
title: Tunnel ldap through the -p proxy and exit 22 under -f when it refuses the CONNECT
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-6083]
touches: [Curl.Original.Protocol.Ldap.UnitLibrary/LdapProtocolHandler.cs, Curl.Original.Protocol.Ldap.UnitLibrary/CLAUDE.md, Curl.Original.Protocol.Ldap.UnitTests/LdapProtocolHandlerProxyConnectFailTests.cs]
requirement: none
created: 2026-11-27
completed: 2026-11-27
---
# BL-6087 — Tunnel ldap through the -p proxy and exit 22 under -f when it refuses the CONNECT

## Goal

`curl -p -x <proxy> ldap://example.test/` asks the proxy for a CONNECT tunnel and, when it is refused, ends as real curl does: exit 22 under `-f`.

## Context

Measured while doing BL-6083 with `Record-CurlExchange.ps1` (`-sS -v -p -f -x http://127.0.0.1:P <url>`, reply `403`, `Content-Length: 0`): reference curl 8.22.0 writes `* The requested URL returned error: 403`, `* closing connection #0`, `curl: (22) The requested URL returned error: 403`; Curl never reaches the proxy: it writes `* Could not resolve host: example.test` and `curl: (6) Could not resolve host: example.test`. Find where the proxy is dropped (the handler, or the connector the console wires for ldap; widen `touches` if so). BL-6083 did this for FTP: set `ConnectTarget.FailsOnHttpErrorStatus` and map the failed connect (see `FtpProtocolHandler.FailedConnectResult`, the `407` text included).

## Acceptance criteria

- [x] For the measured command line, with and without `-f`, the `*` lines after the reply head, the error line and the exit code match reference curl.
- [x] A focused unit test pins both, `dotnet build` is clean and the fast tests pass.

## Notes

- Cause: `LdapProtocolHandler.TargetOf` never set `ConnectTarget.Proxy`; the console's connector needed no change. The fix sets `Proxy` and `FailsOnHttpErrorStatus`, writes `closing connection #N` when curl's own connect fails, and maps a refused CONNECT under `-f` to exit 22, as BL-6083 did for FTP.
- WinLDAP's own second connection sets no proxy: reference curl, after a `200`, binds without the proxy (WinLDAP knows nothing of it). No ADR: this only matches measured curl.
- `closing connection #N` is written for every failed connect of curl's own, proxied or not; reference curl 8.22.0 writes it for a refused direct `ldap://` connect too (measured).
- Measured with `Record-CurlExchange.ps1`: `-p` without and with `-f` against a `403`, and `-f -U u:p` against a `407`; Curl's lines from the reply head, error line and exit code (7, 22, 22) match.
- `Curl.Original.Protocol.Ldap.UnitLibrary/CLAUDE.md` added to `touches` for its proxy paragraph; no task in Doing names it.
- Quality: `Measure-CodeQuality.ps1 -Library Curl.Original.Protocol.Ldap.UnitLibrary` reports 0 failing members.
- Filed BL-6091: the accepted-tunnel case where WinLDAP's direct connection fails.

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing. Criteria at claim: 3390b2f5 6ff2b1f3.
- 2026-11-27: Doing -> Done. ldap now tunnels through the proxy; a refused CONNECT ends as curl 8.22.0 does, exit 22 under -f
