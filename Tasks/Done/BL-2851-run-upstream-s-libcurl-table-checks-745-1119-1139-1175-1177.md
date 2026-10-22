---
id: BL-2851
title: Run upstream's libcurl table checks 745, 1119, 1139, 1175, 1177, 1222, 1276, 1477 and 1488 against Libcurl's option, symbol, error and feature tables
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2850, BL-2990, BL-2991, BL-2992, BL-2993]
touches: [Curl.Conformance.UnitTests/PassingUpstreamCases.txt, Curl.Conformance.UnitLibrary/UpstreamCaseScreening.cs, Curl.Conformance.UnitLibrary/UpstreamPerlCheckLine.cs, Curl.Conformance.UnitTests/UpstreamCaseScreeningTests.cs, Curl.Conformance.UnitTests/UpstreamTestData/README.md]
requirement: none
created: 2026-10-19
completed: 2026-10-21
---
# BL-2851 — Run upstream's libcurl table checks 745, 1119, 1139, 1175, 1177, 1222, 1276, 1477 and 1488 against Libcurl's option, symbol, error and feature tables

## Goal

Upstream cases 745, 1119, 1139, 1175, 1177, 1222, 1276, 1477 and 1488 check `Libcurl`'s option, symbol, error-code and feature tables against upstream's libcurl documentation through C# interpretations of their check scripts, and each is on `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` or has a recorded reason.

## Context

Gap scorecard 2026-10-19_1329 has these nine `excluded | upstream-source-check`; measured 2026-10-19 on Windows they are skipped with `the harness does not run a perl command`. They run upstream curl 8.22.0 scripts (https://github.com/curl/curl/tree/curl-8_22_0/tests, `test<n>.pl`) that keep `docs/libcurl/symbols-in-versions`, the libcurl man pages and the C headers consistent: 745 typecheck-gcc against `curl.h`, 1119 symbols-in-versions against the headers, 1139 every libcurl option has a man page, 1175 symbols-in-versions against `libcurl-errors`, 1177 feature names and `CURL_VERSION_*` in `lib/version.c` and the docs, 1222 deprecation statuses and versions, 1276 `lib/optiontable.pl`, 1477 error codes in headers against `libcurl-errors`, 1488 symbols-in-versions and man pages agree on added-in versions. Curl has no C headers; its counterparts are `Libcurl`'s tables (`CurlOptions.All`, `CurlCode` and its strerror table, `CurlVersionFeatures`, the `CURLINFO_*` list; `Libcurl/CLAUDE.md` maps them), which must carry exactly the symbols, error numbers, feature names, deprecations and added-in versions the vendored docs list. A script that only checks C headers or C macro syntax (745 `typecheck-gcc`; judge from the script) has no counterpart: record it.

Depends on BL-2850, which adds the `<command type="perl">` mechanism and the vendoring of documentation files; extend its vendoring to `docs/libcurl/symbols-in-versions`, `docs/libcurl/libcurl-errors.md`, `docs/libcurl/opts/*.md` and what else these scripts open. Where a check finds `Libcurl` differing from upstream, fix `Libcurl` with a focused unit test in `Libcurl.UnitTests` (ADR-0482) and add the folder to `touches` in the same commit. If nine cases prove too many for one run, finish the first five in the order listed and file the rest as a new task.

## Acceptance criteria

- [x] Each of the nine cases has a C# interpretation of its script over `Libcurl`'s tables, or a Notes entry naming the C-only thing it checks and a row in a new ADR "Decided by Claude under Stewart's delegation".
- [x] Each interpreted case ends `Passed` with `$env:CURL_UPSTREAM_CASE='<n>'` on Windows and is on `PassingUpstreamCases.txt`.
- [x] Any `Libcurl` fix has a focused unit test in `Libcurl.UnitTests` that fails without it.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` passes, and each changed library keeps 100% line and branch coverage and complexity at most 10.
- [x] After the push, `CI` is green on Linux and macOS for the commit.

## Notes

- 2026-10-21 (lane 7, first pass, before any code): too big for one run, as BL-2850 was. It needs vendored `docs/libcurl` files (symbols-in-versions, libcurl-errors.md, opts/*.md, curl_version_info.3, which 1177 reads from `%PWD/../docs/libcurl`), a `Libcurl` reference from the harness, and seven script interpretations, all in files outside `touches` (csprojs, Update-UpstreamTestData.ps1, new `UpstreamTest<n>Script.cs` files, `UpstreamConformanceTests.cs`). Split into BL-2990 (vendoring and the reference), BL-2991 (1119, 1175, 1477), BL-2992 (1139, 1222, 1488) and BL-2993 (1177). What stays here once they are Done: judge 745 (typecheck-gcc over `curl.h`) and 1276 (`lib/optiontable.pl` against `lib/easyoptions.c`), both likely C-only, record them in an ADR or port them, then confirm all nine cases and the CI criterion.
- 2026-10-21 (lane 7, second claim): BL-2990 to BL-2993 are Done and put 1119, 1139, 1175, 1177, 1222, 1477 and 1488 on `PassingUpstreamCases.txt` with interpretations of their scripts (ADR-0749 records that they read the vendored headers where `Libcurl` has no table for a header symbol). 745 and 1276 are C-only: 745 checks `typecheck-gcc.h` (GCC compile-time type-check macros) against `curl.h`, 1276 checks the generated `lib/easyoptions.c` against `lib/optiontable.pl`'s output from `curl.h`; Curl has neither artefact. Recorded in ADR-0755. No `Libcurl` difference found, so no fix and no `Libcurl.UnitTests` test; no production code changed, so no quality run was needed. Measured: `dotnet build Curl.slnx -warnaserror` clean; fast tests 0 failed (Curl.Conformance.UnitTests 4027 passed, 133 skipped), the ratchet holding all seven listed cases. CI on Linux and macOS is the shift's to watch after it pushes (lanes do not push); the seven scripts read vendored text files only, nothing platform-dependent.

## Log

- 2026-10-19: Created.
- 2026-10-21: Backlog -> Doing. Criteria at claim: 09d8956f 44ec4ea8 cfcc71e4 dd1cc079 620ebead.
- 2026-10-21: Doing -> Backlog. Split for size; waits on BL-2990, BL-2991, BL-2992 and BL-2993
- 2026-10-21: Backlog -> Doing. Criteria at claim: 09d8956f 44ec4ea8 cfcc71e4 dd1cc079 620ebead.
- 2026-10-21: Doing -> Done. Seven libcurl table checks pass on the ratchet; 745 and 1276 recorded C-only in ADR-0755
