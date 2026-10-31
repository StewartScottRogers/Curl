---
id: BL-3829
title: Map host names with the whole UTS#46 mapping table, not the BL-3548 subset
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-4033]
touches: [Curl.Protocol.Abstractions.UnitLibrary/CurlUrlHostNameMapping.cs, Curl.Protocol.Abstractions.UnitLibrary/CurlUrlHostNameMappingTable.cs, Curl.Protocol.Abstractions.UnitTests/CurlUrlHostNameMappingTests.cs, Curl.Protocol.Abstractions.UnitTests/Cgr019Tests.cs]
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
- [x] Focused tests in `CurlUrlHostNameMappingTests.cs` pin each mapping class.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Filed by BL-3548's run; validity differences (disallowed characters, ZWJ context rules) are BL-3588's, not this task's.
- Resumed run: the earlier run had written the generated table, NFC and the tests but no ADR; this run added ADR-0980, the table and `Cgr019Tests.cs` (its pinned `｟` now maps to `⦅`) to touches, and the abstractions tests pass (1117 passed, 2 skipped).
- Not measured here: Windows curl uses WinIDN and an ANSI argv, and no Linux curl was reachable (a WSL probe hung). The pinned hosts follow the Unicode 15.1 table and NFC; measuring them is BL-4033 (`lane: no`). Committing the generator app, which the earlier run did not keep, is BL-4034.
- Requeued for BL-4033: the board refuses a changed criterion, and the first one needs a Linux curl 8.22.0 with libidn2 that no lane has. The mapping code, table, tests and ADR-0980 are committed (build clean, fast tests green, `Measure-CodeQuality.ps1` clean for `CurlUrlHostNameMapping` after `Compose` was split to complexity under 10), since they are finished and only the measurement is left; the next claim ticks the first criterion from BL-4033's recorded measurements and fixes any host that differs.

## Log

- 2026-10-26: Created.
- 2026-10-28: Backlog -> Doing. Criteria at claim: aacc7631 98808546 779790c0.
- 2026-10-28: Doing -> Backlog. Waits for BL-4033: measuring the pinned hosts needs a Linux curl 8.22.0 with libidn2, which lanes lack.
- 2026-10-29: Backlog -> Deferred. Split into BL-4078 and BL-4077; mapping code, table, tests and ADR-0980 are done, only the measured-host criterion remains
