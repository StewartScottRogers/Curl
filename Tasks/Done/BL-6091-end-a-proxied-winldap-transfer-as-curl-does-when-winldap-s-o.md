---
id: BL-6091
title: End a proxied WinLDAP transfer as curl does when WinLDAP's own direct connection fails
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-6087]
touches: [Curl.Original.Protocol.Ldap.UnitLibrary/LdapProtocolHandler.cs, Curl.Original.Protocol.Ldap.UnitTests/LdapProtocolHandlerProxyConnectFailTests.cs, Curl.Original.Protocol.Ldap.UnitLibrary/CLAUDE.md, Record-CurlExchange.ps1]
requirement: none
created: 2026-11-27
completed: 2026-11-28
---
# BL-6091 — End a proxied WinLDAP transfer as curl does when WinLDAP's own direct connection fails

## Goal

When a proxy accepts the CONNECT of a Windows-build `ldap` transfer and WinLDAP's own direct connection to the host fails, Curl ends as curl 8.22.0 does: exit 38 with the WinLDAP `-v` lines.

## Context

Measured during BL-6087 (`Record-CurlExchange.ps1 -sS -v -x http://127.0.0.1:P <url>`, proxy reply `HTTP/1.1 200 OK`). After the CONNECT lines curl writes `LDAP local: LDAP Vendor = ...`, `LDAP local: <url>`, `LDAP local: trying to establish cleartext connection`, `LDAP local: bind via ldap_win_bind Unavailable`, `shutting down connection #0` and exits 38 for `ldap://example.test/` (host does not resolve; the same with `-u u:p`). For `ldap://127.0.0.1:<closed port>/` it ends the same way with `Timeout`, after about 480 s (`-m 60` did not cut it short). Curl instead returns the connector's failure of the second connection (exit 6 or 7) without those lines: see `LdapProtocolHandler.WinLdapConnectFailure`. Also measure `ldaps` with `-u` through a proxy: Curl runs it on its own tunnelled connection.

## Acceptance criteria

- [x] A unit test pins that a proxied WinLDAP `ldap` transfer whose own second connection cannot resolve the host exits 38 with `LDAP local: bind via ldap_win_bind Unavailable` and the `*` lines above, and one whose connect is refused ends with the `Timeout` text.
- [x] The behaviour without a proxy is unchanged, `dotnet build` is clean and the fast tests pass.

## Notes

- Measured curl 8.22.0 `ldaps://example.test/ -k` through a proxy that handshakes TLS in the tunnel, with and without `-u`: curl's handshake completes, then `bind via ldap_win_bind Server Down`, exit 38, no byte in the tunnel. So a proxied WinLDAP transfer now always searches on its own direct connection, `ldaps -u` included, and curl's own handshake obeys `-k` (ADR-1470).
- Curl ends a refused direct connect with `Timeout` at once; curl takes about 480 s. No output depends on the wait.
- A connect failure other than an unresolved host (any plaintext one) ends `Timeout`.
- Touches widened: the library's CLAUDE.md, and Record-CurlExchange.ps1, which gained a `-Script` step `tls` to take the ldaps measurement. No task in Doing named either.
- Quality: Measure-CodeQuality.ps1 for the LDAP library, 0 failing members. The unit tests choose the dialect and use fakes, so they do not depend on the platform.

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing. Criteria at claim: a116a983 81487ec7.
- 2026-11-28: Doing -> Done. A proxied WinLDAP transfer ends as curl 8.22.0 does when WinLDAP's own connection fails (ADR-1470).
