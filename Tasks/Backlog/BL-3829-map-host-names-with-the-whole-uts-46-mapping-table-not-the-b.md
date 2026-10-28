---
id: BL-3829
title: Map host names with the whole UTS#46 mapping table, not the BL-3548 subset
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Abstractions.UnitLibrary/CurlUrlHostNameMapping.cs, Curl.Protocol.Abstractions.UnitTests/CurlUrlHostNameMappingTests.cs]
requirement: none
created: 2026-10-26
completed:
---
# BL-3829 — Map host names with the whole UTS#46 mapping table, not the BL-3548 subset

## Goal

`CurlUrlHostNameMapping.Map` maps every character as UTS #46 (non-transitional, Unicode 15.1, as libidn2 2.3.8 in curl 8.22.0) does, so no host name that is not ASCII reaches a different punycode host from curl's.

## Context

BL-3548 (CGR-019, ADR-0895) added `Curl.Protocol.Abstractions.UnitLibrary/CurlUrlHostNameMapping.cs` because Curl runs with invariant globalization, where `IdnMapping` and `string.Normalize` map nothing. It covers the characters mapped to nothing, `char.ToLowerInvariant`, `İ`, full-width ASCII, the ligatures U+FB00 to U+FB06 and canonical composition of lower-case letters in U+00C0 to U+024F, U+0370 to U+04FF and U+1E00 to U+1FFF. Still unmapped: other compatibility mappings (`ſ` to `s`, `ℌ`, `①`, `Ⅻ`, `㎏`, half-width katakana, Arabic presentation forms and the rest of `IdnaMappingTable.txt`), case mappings `ToLowerInvariant` lacks, composition outside those ranges (Hangul jamo, Indic nuktas), canonical reordering of several combining marks, and characters outside the BMP. Generate the table from `IdnaMappingTable.txt` with a C# file-based app (no Python), kept compact, and keep the library at 100% line and branch coverage. Linux is where these hosts can be passed to curl (Windows curl reads an ANSI argv); measure there or with `-K` in a UTF-8 config file.

## Acceptance criteria

- [ ] For a sample of at least 30 hosts drawn from each mapping class above, Curl's `Host` header equals curl 8.22.0's (measured, recorded in Notes).
- [ ] Focused tests in `CurlUrlHostNameMappingTests.cs` pin each mapping class.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Filed by BL-3548's run; validity differences (disallowed characters, ZWJ context rules) are BL-3588's, not this task's.

## Log

- 2026-10-26: Created.
