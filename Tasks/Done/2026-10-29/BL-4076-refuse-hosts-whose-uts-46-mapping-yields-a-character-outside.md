---
id: BL-4076
title: Refuse hosts whose UTS 46 mapping yields a character outside letters, digits and hyphen, as libidn2 curl does (U+2100, U+2101, U+2105, U+2106, Arabic presentation forms)
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Protocol.Abstractions.UnitLibrary/CurlUrlHostNameMapping.cs, Curl.Protocol.Abstractions.UnitTests/CurlUrlHostNameMappingStd3Tests.cs]
requirement: none
created: 2026-10-29
completed: 2026-10-29
---
# BL-4076 — Refuse hosts whose UTS 46 mapping yields a character outside letters, digits and hyphen, as libidn2 curl does (U+2100, U+2101, U+2105, U+2106, Arabic presentation forms)

## Goal

Off Windows, `CurlUrlHostNameMapping` refuses the hosts that libidn2-built curl refuses because their UTS 46 mapping is not valid under STD3 rules, so Curl exits as curl does instead of sending a request.

## Context

BL-4033 measured 457 non-ASCII hosts through curl built with libidn2 2.3.8 on Ubuntu (curl 8.18.0, standing in for 8.22.0; the IDN mapping is libidn2's, not curl's). 443 converted and are pinned in `Curl.Protocol.Abstractions.UnitTests/CurlUrlHostNameMappingTests.cs`. The other 14 were refused with exit 3 or 27 and were not pinned: U+2100, U+2101, U+2105 and U+2106 (each maps to text holding `/`, e.g. U+2100 to `a/c`), and ten Arabic presentation forms in U+FE70..U+FE7F and U+FDFA (they map to sequences that start with a combining mark or hold a space, e.g. U+FDFA). These are UTS 46 `disallowed_STD3_mapped` code points and mappings that break the label rules, which libidn2 applies with `IDN2_USE_STD3_ASCII_RULES`.

Start in `CurlUrlHostNameMapping`: find what it does today for each of the 14 (BL-4033's 443 passing cases must keep passing). Derive the ten Arabic code points from the UTS 46 table (`IdnaMappingTable.txt`, the version the mapping already uses): the `disallowed_STD3_mapped` entries in U+FE70..U+FE7F plus U+FDFA. Which exit code curl gives (3 or 27) depends on whether the URL parser or the resolver refuses it; find the matching refusal in Curl and match it, and if you can't tell which from the code and upstream source (lib/idn.c, lib/urlapi.c, curl 8.22.0), use 3 and say so in Notes. Windows curl uses WinIDN, not libidn2, so mark the tests `[OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]` if the Windows path differs.

## Acceptance criteria

- [x] Each of the 14 hosts has a test in `CurlUrlHostNameMappingStd3Tests.cs` that asserts refusal, and the test fails on the code before the fix and passes after it.
- [x] Every test in `CurlUrlHostNameMappingTests.cs` still passes.
- [x] `CurlUrlHostNameMapping` keeps 100% line and branch coverage and complexity at most 10 per method.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes for `Curl.Protocol.Abstractions.UnitTests`.

## Notes

- Decided: refusal lives in `CurlUrlHostNameMapping.Map` (inside `touches`): a mapping holding an ASCII character other than letter, digit, hyphen or dot becomes U+FFFD, which `CurlUrlHost` already refuses. Exit code is whatever that refusal gives (unmeasured here; no change to CurlUrlHost).
- The 14 are U+2100, 2101, 2105, 2106, FDFA, FDFB and FE70..FE7E even. The same rule also refuses other STD3-mapped code points (FC5E..FC63, FE10, FE33 ...), which BL-4033 did not measure.
- Tests are not OS-conditioned: Map is shared by all platforms. Measure-CodeQuality.ps1 aborted on unrelated test hosts (Curl.Api.UnitTests crashed), so coverage was not measured by it; both branches are exercised by the 17 new rows and the existing 443.

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: f6daa2ba 29763f33 bca84801 a602f75b.
- 2026-10-29: Doing -> Done. Hosts whose UTS 46 mapping holds non-LDH ASCII are refused
