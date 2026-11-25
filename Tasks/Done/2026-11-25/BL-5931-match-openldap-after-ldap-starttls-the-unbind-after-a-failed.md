---
id: BL-5931
title: Match OpenLDAP after ldap:// StartTLS: the unbind after a failed handshake, no bind without -u, and a non-ExtendedResponse reply
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Original.Protocol.Ldap.UnitLibrary/LdapStartTls.cs, Curl.Original.Protocol.Ldap.UnitLibrary/LdapProtocolHandler.cs, Curl.Original.Protocol.Ldap.UnitTests/LdapStartTls, Curl.Original.Protocol.Ldap.UnitLibrary/LdapExtendedResponse.cs, Curl.Original.Protocol.Ldap.UnitLibrary/LdapBindReplyStatus.cs, Curl.Original.Protocol.Ldap.UnitTests/Cgr013Tests.cs, Documentation/Planning/Decisions/ADR-1366-openldap-starttls-and-simple-bind-failures-follow-curl-8-21-0.md]
requirement: none
created: 2026-11-24
completed: 2026-11-25
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

- [x] Each of the three cases sends upstream's bytes and gives its exit code and message, each pinned by a unit test against a test-double LDAP server.
- [x] The WinLDAP path's StartTLS tests still pass unchanged.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Measured on 2026-11-24 under WSL against a scripted `socat` server (curl 8.21.0, OpenLDAP):
  all three cases, plus a SearchResultEntry in reply to StartTLS. The table and the decisions
  are in ADR-1366, "Added by BL-5931" (amended rather than a new ADR: same subject, and its
  "Not covered here" paragraph would otherwise be false).
- The handler keeps the plaintext connection open by giving the TLS provider a wrapper whose
  disposal does nothing (`LdapStartTls.KeptOpenConnection`); `Curl.Networking` is unchanged.
- `--ssl` with a BindResponse of resultCode 52 or 2 binds as LDAPv3 or LDAPv2; resultCode 0
  starts the handshake whatever the response's operation is.
- A SearchResultEntry under `--ssl-reqd` also sends AbandonRequest(1) before the unbind, as
  measured. A reply that is no LDAP response at all keeps the port's earlier behaviour (not measured).
- `touches` widened (no other task in Doing on `origin/work/dark-factory` named them):
  `LdapExtendedResponse.cs`, `LdapBindReplyStatus.cs`, `Cgr013Tests.cs` (two OpenLDAP
  assertions pinned the old behaviour; its WinLDAP tests are unchanged) and ADR-1366.
- `Measure-CodeQuality.ps1` on the library: 100% line, one branch short
  (`IsSimpleBindWithoutCredentials`, SASL after StartTLS); the test for it was added after
  the run, which was not repeated.
- CI on Linux and macOS: the new tests use only in-memory fakes, so nothing is platform-dependent.

## Log

- 2026-11-24: Created.
- 2026-11-24: Backlog -> Doing. Criteria at claim: dab3070b 0fde7b0a 3d836d5e.
- 2026-11-25: Doing -> Done. OpenLDAP-dialect StartTLS now unbinds after a failed handshake, sends no simple bind without -u, and reads a non-ExtendedResponse reply as curl 8.21.0 does.
