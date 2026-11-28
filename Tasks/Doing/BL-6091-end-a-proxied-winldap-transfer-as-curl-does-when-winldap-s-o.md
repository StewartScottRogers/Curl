---
id: BL-6091
title: End a proxied WinLDAP transfer as curl does when WinLDAP's own direct connection fails
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-6087]
touches: [Curl.Original.Protocol.Ldap.UnitLibrary/LdapProtocolHandler.cs, Curl.Original.Protocol.Ldap.UnitTests/LdapProtocolHandlerProxyConnectFailTests.cs]
requirement: none
created: 2026-11-27
completed:
---
# BL-6091 — End a proxied WinLDAP transfer as curl does when WinLDAP's own direct connection fails

## Goal

When a proxy accepts the CONNECT of a Windows-build `ldap` transfer and WinLDAP's own direct connection to the host fails, Curl ends as curl 8.22.0 does: exit 38 with the WinLDAP `-v` lines.

## Context

Measured during BL-6087 (`Record-CurlExchange.ps1 -sS -v -x http://127.0.0.1:P <url>`, proxy reply `HTTP/1.1 200 OK`). After the CONNECT lines curl writes `LDAP local: LDAP Vendor = ...`, `LDAP local: <url>`, `LDAP local: trying to establish cleartext connection`, `LDAP local: bind via ldap_win_bind Unavailable`, `shutting down connection #0` and exits 38 for `ldap://example.test/` (host does not resolve; the same with `-u u:p`). For `ldap://127.0.0.1:<closed port>/` it ends the same way with `Timeout`, after about 480 s (`-m 60` did not cut it short). Curl instead returns the connector's failure of the second connection (exit 6 or 7) without those lines: see `LdapProtocolHandler.WinLdapConnectFailure`. Also measure `ldaps` with `-u` through a proxy: Curl runs it on its own tunnelled connection.

## Acceptance criteria

- [ ] A unit test pins that a proxied WinLDAP `ldap` transfer whose own second connection cannot resolve the host exits 38 with `LDAP local: bind via ldap_win_bind Unavailable` and the `*` lines above, and one whose connect is refused ends with the `Timeout` text.
- [ ] The behaviour without a proxy is unchanged, `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing. Criteria at claim: a116a983 81487ec7.
