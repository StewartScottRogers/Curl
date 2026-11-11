---
id: BL-4951
title: Write an LDAP entry whose last value is empty, as curl does, instead of its DN line alone
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Original.Protocol.Ldap.UnitLibrary/LdapSearchResponse.cs, Curl.Original.Protocol.Ldap.UnitTests/LdapSearchResponseTests.cs]
requirement: none
created: 2026-11-11
completed:
---
# BL-4951 — Write an LDAP entry whose last value is empty, as curl does, instead of its DN line alone

## Goal

An LDAP SearchResultEntry whose only (or last) attribute holds one empty value is written as curl 8.22.0 writes it (`	db: 

` after the DN line), not as the DN line alone.

## Context

Found by BL-4913 with `Record-CurlExchange.ps1 -Script`: bind success, then entry `cn=a` with one attribute `db` holding one empty value (`30 0a 04 02 64 62 31 04 04 00`), then SearchResultDone. curl 8.22.0 (Schannel, WinLDAP) prints `DN: cn=a
	db: 

`; Curl prints `DN: cn=a
`, the "attributes could not be read" path of `LdapEntryFormatter`. The `empty-win` recording in `LdapProtocolHandlerTests.Output.cs`, where the empty value is followed by another attribute, passes, so the reader likely trips on an empty OCTET STRING that ends the entry. Start in `LdapSearchResponse.cs`.

## Acceptance criteria

- [ ] The entry above prints exactly `DN: cn=a
	db: 

` with `-sS -u cn=admin,dc=x:pw`, as curl 8.22.0 does.
- [ ] A focused unit test in `Curl.Original.Protocol.Ldap.UnitTests` pins it.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-11: Created.
