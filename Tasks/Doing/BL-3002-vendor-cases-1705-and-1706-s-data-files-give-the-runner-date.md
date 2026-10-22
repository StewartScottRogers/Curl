---
id: BL-3002
title: Vendor cases 1705 and 1706's data files, give the runner %DATE and %VERNUM, and put both cases on the ratchet
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-3018]
touches: [Curl.Conformance.UnitTests/UpstreamTestData/Update-UpstreamTestData.ps1, Curl.Conformance.UnitTests/UpstreamTestData/data, Curl.Conformance.UnitTests/UpstreamTestDataTests.cs, Curl.Conformance.UnitLibrary/UpstreamCaseRunner.cs, Curl.Conformance.UnitTests/UpstreamCaseRunnerTests.cs, Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-21
completed:
---
# BL-3002 — Vendor cases 1705 and 1706's data files, give the runner %DATE and %VERNUM, and put both cases on the ratchet

## Goal

Upstream cases 1705 and 1706 ("managen makes man page" / "ASCII page") run through BL-2881's `managen` interpretation and end `Passed`, each on `PassingUpstreamCases.txt`.

## Context

Split from BL-2881 (2026-10-21), which builds the generator (`Curl.Cli.UnitLibrary/ManualPages`) and registers `managen` (`UpstreamManagenScript`). Three things still stop the cases running, all outside BL-2881's `touches`:

1. The case data is not vendored: `tests/data/data1705-1.md` to `-4.md`, `data1705-stdout.1`, `data1706-1.md` to `-4.md` and `data1706-stdout.txt` at `curl-8_22_0`. Add them to `Update-UpstreamTestData.ps1`'s one-by-one data list (as data1461.txt is) and to `UpstreamTestData/data`.
2. `%DATE` and `%VERNUM` have no value in `UpstreamCaseRunner` (listed in `UpstreamTestVariableSubstitution` but never set), so the cases skip. runtests.pl at `curl-8_22_0` sets `SOURCE_DATE_EPOCH` to its start time and `%DATE` to `strftime "%Y-%m-%d", gmtime` of it, and `%VERNUM` to the leading digits and dots of curl's version (`CurlVersion`, 8.22.0). managen prints `.TH curl 1 "<date>" "curl <version>"` with the date from `SOURCE_DATE_EPOCH` (UTC) and the version from `-I <include>/curl/curlver.h`'s `LIBCURL_VERSION` digits; the vendored `UpstreamTestData/include/curl/curlver.h` says `8.22.0-DEV`, so 8.22.0. The runner must give the script the same date it gives `%DATE` (for example `SOURCE_DATE_EPOCH` in the invocation's `EnvironmentVariables`, which `UpstreamManagenScript` reads).
3. The command names `-I %SRCDIR/../include`: check the path resolves to the vendored `UpstreamTestData/include` (BL-2881's script reads it through the path given; if `%SRCDIR/..` is not `UpstreamTestData`, map it there).

## Acceptance criteria

- [ ] `$env:CURL_UPSTREAM_CASE='1705'` and `'1706'` each end `Passed` on Windows, and both are on `PassingUpstreamCases.txt`.
- [ ] `UpstreamCaseRunner` gives `%DATE` and `%VERNUM` values, with unit tests.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

## Log

- 2026-10-21: Created.
- 2026-10-21: Backlog -> Doing. Criteria at claim: 294eb782 69030254 c5f8cef6.
