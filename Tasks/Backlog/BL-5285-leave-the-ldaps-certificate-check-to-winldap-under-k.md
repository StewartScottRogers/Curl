---
id: BL-5285
title: Leave the ldaps certificate check to WinLDAP under -k
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Original.Protocol.Ldap.UnitLibrary/LdapProtocolHandler.cs, Curl.Original.Protocol.Ldap.UnitTests/LdapsInsecureWinLdapTests.cs]
requirement: none
created: 2026-11-16
completed:
---
# BL-5285 — Leave the ldaps certificate check to WinLDAP under -k

## Goal

On Windows, `-k` does not make an `ldaps://` connection to a server with an untrusted certificate succeed: the port exits 38 with `Server Down`, as upstream curl 8.21.0's `WinLDAP` backend does.

## Context

Filed from Surl's Phase 7 measurement of the port (Stewart approved filing on the port's board, 2026-11-16): Surl `Documentation/Conformance/CurlPort/Port-Board-Handoff.md`, entry 8 (github.com/StewartScottRogers/Surl). The port was measured at commit `b42c319ba940f66452b2b6f3e9910d46825b08fe` (port `curl.exe` SHA-256 `F28D1417807AA862BA2A6D5B0BF0CF1200673BE45B959B1A78E1E4B476EF8FA7`), on Windows x64, 2026-11-05, beside upstream curl 8.21.0, the oracle: where the two differ against the same Surl server, the port is the one in the wrong.

- **Finding:** Surl `Documentation/Conformance/CurlPort/Ldap.md`, F3.
- **Surl test:** `CurlPortBesideUpstreamCurlOverLdapTests.WinLdap_Ldaps_Insecure_Exits38ServerDownBecauseWinLdapChecksTheCertificateItself`, Surl commit `00351846bd295957d066acd7ac6a9faa68c78e84`.
- **Command line:** `curl -sS -m 20 -k -u alice:secret ldaps://127.0.0.1:<port>/dc=example,dc=com` against `surl --user alice:secret --self-signed`.
- **Upstream curl 8.21.0 (correct):** reference build, SHA-256 `0E773709C3A44DB47B88B71351D902027682ED87C3BD3821009E454BACCA8778`: exit 38, nothing written, `curl: (38) LDAP local: bind via ldap_win_bind Server Down`.
- **Port (defect):** exit 0, writes `DN: dc=example,dc=com`.
- **What decides:** curl 8.21.0 `lib/ldap.c` hands `ldaps` to `WinLDAP`'s own TLS (`ldap_sslinit()`, `LDAP_OPT_SSL`); `-k` turns off curl's own verification but is not passed to `WinLDAP`, which checks the certificate against the Windows store and fails with `LDAP_SERVER_DOWN`.
- **Where to start:** `Curl.Original.Protocol.Ldap.UnitLibrary/LdapProtocolHandler.cs`. BL-3818, BL-4044 and BL-4055 (CGR-289) made the logon-bind `ldaps` path open WinLDAP's second connection trusting only the system store; this is the `-u` simple-bind path under `-k`.

## Acceptance criteria

- [ ] On Windows, `-k` with `ldaps://` to a self-signed server exits 38 with `curl: (38) LDAP local: bind via ldap_win_bind Server Down` and writes nothing, shown by a port test.

## Notes

Shares `LdapProtocolHandler.cs` with BL-5284, so the two run one after the other.

## Log

- 2026-11-16: Created.
