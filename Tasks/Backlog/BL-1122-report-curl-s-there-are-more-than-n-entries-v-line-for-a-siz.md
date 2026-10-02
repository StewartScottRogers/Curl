---
id: BL-1122
title: Report curl's There are more than N entries -v line for a size-limited LDAP search
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Protocol.Ldap.UnitLibrary, Curl.Protocol.Ldap.UnitTests]
requirement: none
created: 2026-10-01
completed:
---
# BL-1122 — Report curl's There are more than N entries -v line for a size-limited LDAP search

## Goal

An LDAP search answered with result code 4 (`sizeLimitExceeded`) still succeeds, as it does now, and also reports curl 8.21.0's `-v` info line `There are more than N entries`, N being the number of entries received, after those entries are written; today the line is missing for both dialects.

## Context

- curl 8.21.0, both LDAP back ends print it:
  - `lib/ldap.c` (the WinLDAP build, Windows), line 624 at https://github.com/curl/curl/blob/curl-8_21_0/lib/ldap.c: `ldap_search_s` returning `LDAP_SIZELIMIT_EXCEEDED` is not an error (line 440), the entries are written, and at `quit:` `if(rc == LDAP_SIZELIMIT_EXCEEDED) infof(data, "There are more than %d entries", num);`, `num` counting the entries written.
  - `lib/openldap.c` (the OpenLDAP build, Linux and macOS), line 1134 at https://github.com/curl/curl/blob/curl-8_21_0/lib/openldap.c: on the `LDAP_RES_SEARCH_RESULT` with code `LDAP_SIZELIMIT_EXCEEDED`, `infof(data, "There are more than %d entries", lr->nument);` and then the same handling as success; `lr->nument` counts the `LDAP_RES_SEARCH_ENTRY` messages.
- Curl today: `Curl.Protocol.Ldap.UnitLibrary/LdapSearchReply.cs` `IsSuccess` already accepts result codes 0 and 4, and `LdapSearch.OutcomeAsync` writes the held entries and succeeds, but nothing reports the line. `Curl.Protocol.Ldap.UnitTests/LdapProtocolHandlerTests.Search.cs` `ExecuteAsync_SearchAnsweredSizeLimitExceeded_Succeeds` checks only the result, for both `LdapDialect` values. Report through the transfer's `ITransferEvents`, keeping the text in `LdapVerboseLines`.

## Acceptance criteria

- [ ] New tests in `Curl.Protocol.Ldap.UnitTests` for both `LdapDialect.WinLdap` and `LdapDialect.OpenLdap`: a search answered with two entries and a `sizeLimitExceeded` done reports `There are more than 2 entries` after the entries are written; with no entries, `There are more than 0 entries`.
- [ ] A test pins that a search done with result code 0 reports no such line.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes; `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Protocol.Ldap.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-10-01: Created.
