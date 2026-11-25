---
id: BL-5931
title: Match OpenLDAP after ldap:// StartTLS: the unbind after a failed handshake, no bind without -u, and a non-ExtendedResponse reply
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Original.Protocol.Ldap.UnitLibrary/LdapStartTls.cs, Curl.Original.Protocol.Ldap.UnitLibrary/LdapProtocolHandler.cs, Curl.Original.Protocol.Ldap.UnitTests/LdapStartTls]
requirement: none
created: 2026-11-24
completed:
---
# BL-5931 — Match OpenLDAP after ldap:// StartTLS: the unbind after a failed handshake, no bind without -u, and a non-ExtendedResponse reply

## Goal

On the OpenLDAP dialect, `ldap://` StartTLS sends the bytes and gives the exit codes of curl 8.21.0 in the three cases BL-5908 left out (ADR-1366).

## Context

Found by BL-5908, measuring `C:\UpstreamCurl\openldap-curl-8.21.0-linux-x86_64\curl` under WSL against a scripted `socat` server; source `Curl.Conformance.UnitTests/CurlTestCases/Original/lib/openldap.c` (`oldap_connecting`).

1. After a failed handshake (server sends a TLS alert) curl sends an UnbindRequest `30 05 02 01 02 42 00` in plaintext. `SslStreamTlsProvider` and `HandBuiltTlsProvider` dispose the plaintext connection on failure, so the LDAP handler needs the connection kept open for that write.
2. After StartTLS succeeds with no `-u`, `openldap.c` (state `OLDAP_TLS`) sends no BindRequest and goes straight to the search (read in source, measure first).
3. A reply to StartTLS that is not an ExtendedResponse: measured, a BindResponse with resultCode 52 under `--ssl-reqd` exits 64 `Requested SSL level failed` after an UnbindRequest; the port exits 64 `LDAP local: Can't contact LDAP server` and sends nothing. Measure `--ssl` too.

## Acceptance criteria

- [ ] Each of the three cases sends upstream's bytes and gives its exit code and message, each pinned by a unit test against a test-double LDAP server.
- [ ] The WinLDAP path's StartTLS tests still pass unchanged.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-24: Created.
