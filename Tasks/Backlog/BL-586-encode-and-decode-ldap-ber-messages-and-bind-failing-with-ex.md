---
id: BL-586
title: Encode and decode LDAP BER messages and bind, failing with exit 38
priority: Normal
assignee: Claude
pipeline: protocol
depends-on: [BL-585, BL-532]
touches: [Curl.Protocol.Ldap.UnitLibrary, Curl.Protocol.Ldap.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-586 — Encode and decode LDAP BER messages and bind, failing with exit 38

## Goal

An `LdapProtocolHandler` connects through `IConnector` (TLS for `ldaps://`), sends the `BindRequest` curl 8.21.0 sends (anonymous, or simple with `-u`), reads the `BindResponse`, and fails a non-success result with exit 38 (`CURLE_LDAP_CANNOT_BIND`) and curl's message; BER messages split across reads are handled.

## Context

- Conformance audit 2026-09-28, row 37. Design: BL-585's ADR (BER approach, platform behaviour). RFC 4511.
- Measure with `Record-CurlExchange.ps1 -Script` (BL-532): anonymous bind, `-u "cn=u,dc=x:secret"`, a bind answered `invalidCredentials` (49), and a server that closes; record `request.bin`, stderr and exit code.

## Acceptance criteria

- [ ] Measured first as above; request bytes, stderr and exit code copied into Notes.
- [ ] `Curl.Protocol.Ldap.UnitTests` pin the bind request bytes and outcome for each case through a fake connection, and round-trip the BER encoder and decoder for each type used.
- [ ] No test needs `TestCategory=Integration`; tests are platform-neutral; any platform difference the ADR records is pinned per platform.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Protocol.Ldap.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-28: Created.
