---
id: BL-5284
title: Open and leave idle curl's own connection before WinLDAP's on ldap://
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Original.Protocol.Ldap.UnitLibrary/LdapProtocolHandler.cs, Curl.Original.Protocol.Ldap.UnitTests/LdapIdleCurlConnectionTests.cs]
requirement: none
created: 2026-11-16
completed:
---
# BL-5284 — Open and leave idle curl's own connection before WinLDAP's on ldap://

## Goal

On an `ldap://` URL with a simple or anonymous bind, the port opens curl's own connection to the host and leaves it idle while the LDAP exchange runs on a second connection, as upstream curl 8.21.0's `WinLDAP` backend does; or the port records that it keeps one connection.

## Context

Filed from Surl's Phase 7 measurement of the port (Stewart approved filing on the port's board, 2026-11-16): Surl `Documentation/Conformance/CurlPort/Port-Board-Handoff.md`, entry 6 (github.com/StewartScottRogers/Surl). The port was measured at commit `b42c319ba940f66452b2b6f3e9910d46825b08fe` (port `curl.exe` SHA-256 `F28D1417807AA862BA2A6D5B0BF0CF1200673BE45B959B1A78E1E4B476EF8FA7`), on Windows x64, 2026-11-05, beside upstream curl 8.21.0, the oracle: where the two differ against the same Surl server, the port is the one in the wrong.

- **Finding:** Surl `Documentation/Conformance/CurlPort/Ldap.md`, F1.
- **Surl tests (12):** every `CurlPortBesideUpstreamCurlOverLdapTests.WinLdap_` case with a simple or anonymous bind, e.g. `WinLdap_Search_BaseWithSimpleBind_Exits0WithTheBaseEntry` (all listed in `Ldap.md`); Surl commit `00351846bd295957d066acd7ac6a9faa68c78e84`.
- **Command line, e.g.:** `curl -sS -m 20 -u alice:secret ldap://127.0.0.1:<port>/dc=example,dc=com`.
- **Upstream curl 8.21.0 (correct):** reference build, SHA-256 `0E773709C3A44DB47B88B71351D902027682ED87C3BD3821009E454BACCA8778`: two TCP connections; connection 0 carries 0 bytes, connection 1 the bind, search and unbind (112 bytes from `30 84 00 00 00 1B 02 01 01 60 84 ...`). The server notes two exchanges.
- **Port (defect):** one connection carrying exactly upstream's connection 1 bytes. First differing request byte: connection 0, offset 0 (`0x30` from the port; upstream's connection ends there). Exit codes, output and standard error agree.
- **What decides:** curl 8.21.0 `lib/ldap.c`: `ldap_do()` passes the host and port to `ldap_init()` / `ldap_sslinit()`, so `WinLDAP` opens its own connection and curl's stays idle.
- **Where to start:** `Curl.Original.Protocol.Ldap.UnitLibrary/LdapProtocolHandler.cs`. BL-3763 (CGR-234) already added curl's extra connection for the anonymous Negotiate (logon) bind; this is the simple-bind and `-u` path.

## Acceptance criteria

- [x] On `ldap://` the port opens curl's own connection, sends nothing on it, and runs the LDAP exchange on a second connection, shown by a port test counting connections; or a port ADR records that the port keeps one connection.

## Notes

- Decision in ADR-1280. Also edited Curl.Console.UnitTests/CurlCompositionLdapTests.cs (target count 2) and LdapProtocolHandlerTests.Search.cs; both are test-only. Ldap quality gate: 100/100, 0 failing. Platform-neutral tests. Ldaps with -u left at one connection (BL-5285).

## Log

- 2026-11-16: Created.
- 2026-11-16: Backlog -> Doing. Criteria at claim: c377b6dc.
