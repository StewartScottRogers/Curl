---
id: BL-587
title: Read an LDAP URL and send its search request, failing with exit 39
priority: Normal
assignee: Claude
pipeline: protocol
depends-on: [BL-586]
touches: [Curl.Protocol.Ldap.UnitLibrary, Curl.Protocol.Ldap.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-587 — Read an LDAP URL and send its search request, failing with exit 39

## Goal

`ldap://host/<dn>?<attrs>?<scope>?<filter>?<extensions>` is read as curl 8.21.0 reads it (RFC 4516, percent-decoding, defaults `base` scope and `(objectClass=*)`), the filter string is encoded per RFC 4515, the `SearchRequest` is sent, and a non-success `SearchResultDone` fails with exit 39 (`CURLE_LDAP_SEARCH_FAILED`) and curl's message; a URL curl rejects is refused with its exit code.

## Context

- Conformance audit 2026-09-28, row 37. Builds on BL-586. URL model: BL-585's ADR.
- Measure with `Record-CurlExchange.ps1 -Script`: `ldap://h/dc=example`, `?cn,mail?sub?(uid=a)`, a filter with `&`, `|`, `!`, `*` and an escaped `\2a`, a bad scope, a bad filter, and a search answered `noSuchObject` (32); record `request.bin`, stderr and exit code.

## Acceptance criteria

- [ ] Measured first as above; request bytes, stderr and exit code copied into Notes.
- [ ] `Curl.Protocol.Ldap.UnitTests` pin the search request bytes for each URL and the outcome for each failure.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Protocol.Ldap.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
