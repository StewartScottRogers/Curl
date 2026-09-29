---
id: BL-830
title: Bind without -u as WinLDAP does: rootDSE read, then the Sicily NTLM bind
priority: Normal
assignee: Claude
pipeline: protocol
depends-on: [BL-586]
touches: [Curl.Protocol.Ldap.UnitLibrary, Curl.Protocol.Ldap.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-830 — Bind without -u as WinLDAP does: rootDSE read, then the Sicily NTLM bind

## Goal

With `LdapDialect.WinLdap` and no `-u`, `LdapProtocolHandler` binds as curl 8.21.0's `ldap_win_bind` does: it reads the rootDSE's `supportedCapabilities` and `supportedSASLMechanisms`, then sends WinLDAP's Sicily NTLM negotiate bind with the logged-on user's credentials (through the BCL's `NegotiateAuthentication`, behind an injected seam so tests need no domain), and fails as WinLDAP fails.

## Context

- ADR-0166, "Bind": the WinLDAP dialect reproduces `ldap_win_bind` without `-u`. BL-586 built the simple bind for both dialects and left the WinLDAP dialect binding anonymously without `-u`.
- Measured in BL-586 (Windows curl 8.21.0, WinLDAP): without `-u` the first message is a SearchRequest, id 1, base `""`, scope base, `neverDerefAliases`, size limit 0, time limit 120 (`02 01 78`), `typesOnly` false, filter `87 0b "objectclass"`, attributes `supportedCapabilities`:
  `30 84 00 00 00 44 02 01 01 63 84 00 00 00 3b 04 00 0a 01 00 0a 01 00 02 01 00 02 01 78 01 01 00 87 0b 6f 62 6a 65 63 74 63 6c 61 73 73 30 84 00 00 00 17 04 15 "supportedCapabilities"`.
  A server that closes then ends with exit 38 `curl: (38) LDAP local: bind via ldap_win_bind Timeout` after about 240 s.
- Measure next with `Record-CurlExchange.ps1 -Script`: answer the rootDSE reads (with and without `supportedSASLMechanisms` naming `GSS-SPNEGO`/`NTLM`), record the Sicily bind messages (`[APPLICATION 0]` with the `sicilyPackageDiscovery`/`sicilyNegotiate`/`sicilyResponse` choices), and each failure.
- `Curl.Authentication.UnitLibrary` holds the NTLM pieces, but a protocol library may reference only Abstractions: the NTLM token source must reach the handler through an interface the composition root supplies.

## Acceptance criteria

- [ ] Measured first as above; request bytes, stderr and exit code copied into Notes.
- [ ] `Curl.Protocol.Ldap.UnitTests` pin the rootDSE SearchRequest bytes, the Sicily bind messages and each failure's exit code and message through a fake connection and a fake token source.
- [ ] The OpenLDAP dialect's anonymous bind is unchanged (its BL-586 tests still pass).
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Protocol.Ldap.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- Filed by BL-586. If the token-source seam needs a new interface in `Curl.Protocol.Abstractions.UnitLibrary`, add that project to `touches` first.

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
