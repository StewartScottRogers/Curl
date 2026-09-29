---
id: BL-588
title: Write LDAP search results in curl's output format
priority: Normal
assignee: Claude
pipeline: protocol
depends-on: [BL-587]
touches: [Curl.Protocol.Ldap.UnitLibrary, Curl.Protocol.Ldap.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-588 — Write LDAP search results in curl's output format

## Goal

Each `SearchResultEntry` is written exactly as the platform's curl 8.21.0 build writes it (the `DN:` line, one tab-indented `attr: value` line per value, base64 for binary values, the blank line between entries), and referrals and `SearchResultDone` are handled as curl handles them.

## Context

- Conformance audit 2026-09-28, row 37. Builds on BL-587. Output format and platform split: BL-585's ADR.
- Measure with `Record-CurlExchange.ps1 -Script`: two entries, a multi-valued attribute, a binary value (bytes outside printable ASCII), an entry with no attributes, and a referral; record stdout bytes exactly.

## Acceptance criteria

- [ ] Measured first as above; stdout bytes copied into Notes.
- [ ] `Curl.Protocol.Ldap.UnitTests` pin the output bytes for each case; any platform difference the ADR records is pinned with `OSCondition`.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Protocol.Ldap.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
