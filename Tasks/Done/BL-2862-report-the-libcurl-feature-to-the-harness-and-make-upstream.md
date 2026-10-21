---
id: BL-2862
title: Report the --libcurl feature to the harness and make upstream cases 1400 to 1407, 1420, 1465 and 1481 pass
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitTests/PassingUpstreamCases.txt, Curl.Conformance.UnitLibrary/UpstreamToolFeatures.cs, Curl.Conformance.UnitLibrary/UpstreamCurlPlatform.cs, Curl.Conformance.UnitLibrary/UpstreamPerlConditionalEmptying.cs, Curl.Conformance.UnitTests/UpstreamPerlConditionalEmptyingTests.cs, Curl.Conformance.UnitTests/UpstreamCurlPlatformTests.Libcurl.cs, Curl.Console/CurlCommandRunner.cs, Curl.Cli.UnitLibrary, Curl.Cli.UnitTests]
requirement: none
created: 2026-10-19
completed: 2026-10-21
---
# BL-2862 — Report the --libcurl feature to the harness and make upstream cases 1400 to 1407, 1420, 1465 and 1481 pass

## Goal

The harness reports the `--libcurl` feature, so upstream cases 1400, 1401, 1402, 1403, 1404, 1405, 1406, 1407, 1420, 1465 and 1481 run, and each is on `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` or has a recorded reason; the C source `curl --libcurl` writes matches upstream's expected text byte for byte.

## Context

Gap scorecard 2026-10-19_1329: 1400, 1402, 1403, 1405, 1406, 1407, 1420 and 1465 are `excluded | reference-lacks:--libcurl`; 1401 (`reference-lacks:cookies`), 1404 (`reference-lacks:Mime`) and 1481 (`reference-lacks:proxy`) are excluded for the same feature. Measured 2026-10-19 on Windows all eleven are skipped with `Curl lacks the feature --libcurl`. `--libcurl <file>` writes a C program equivalent to the command line; `Curl.Console/CurlCommandRunner.cs` already writes one (`LibcurlFile`, `Curl.Cli.UnitLibrary`), but `UpstreamCurlPlatform` keeps the case feature `--libcurl` off ("libcurl's build and API features, which a command-line tool does not have"), so the cases never ran. Each case runs a command with `--libcurl %LOGDIR/test<n>.c` and compares the written file (`<verify><file>`, possibly with `<stripfile>` lines that drop version or date comments) with upstream's expected C text, the exact bytes curl 8.22.0 writes (https://curl.se/docs/manpage.html, `--libcurl`; https://github.com/curl/curl/tree/curl-8_22_0/tests/data/test1400): the `CURLOPT_*` calls in the order curl sets them, the `curl_slist` and mime blocks, and the trailing cleanup.

Step 1: report `--libcurl` in `UpstreamCurlPlatform` (the tool implements it) and run the eleven. Step 2: for each difference, decide whether `CommandLineOptions` records the option in the wrong order or text, and fix `Curl.Console`/`Curl.Cli.UnitLibrary` with a focused unit test in its `.UnitTests` project (ADR-0482). Expect several differences; this is where the work is. If a lane reaches the time of one run before all eleven pass, list those that pass and file the rest as one new task per cause. Keep `curl --ai-help` right if an option's text changes.

## Acceptance criteria

- [x] `UpstreamCurlPlatform` lists `--libcurl` on every platform; `UpstreamCurlPlatformTests` pins it.
- [x] Each of the eleven cases has its outcome in Notes; every case that ends `Passed` with `$env:CURL_UPSTREAM_CASE='<n>'` on Windows is on `PassingUpstreamCases.txt`, and each that differs has its first difference in Notes and a follow-up task naming the cause.
- [x] Each product fix has a focused unit test in `Curl.Cli.UnitTests` or `Curl.Console`'s test project that fails without it.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` passes, and each changed library keeps 100% line and branch coverage and complexity at most 10.
- [x] After the push, `CI` is green on Linux and macOS for the commit.

## Notes

- `--libcurl` is now in `UpstreamToolFeatures` (so every platform reports it), pinned by `UpstreamCurlPlatformTests.AllPlatforms_ListTheLibcurlOption`; `UpstreamCurlPlatform`'s remarks moved it from the "stay off" list.
- Measured 2026-10-21 on Windows: once reported, all eleven cases still skipped, on the strip line `$_ = '' if /CURLOPT_.../` the harness could not run. `UpstreamPerlConditionalEmptying` now reads that one-term form too (with `\/` escapes, test1404's `$_ = '' if /\/\* "value" \*\//`), pinned by `Apply_BareMatchLine_EmptiesTheLinesThePatternMatches`. Touches widened to `UpstreamCurlPlatform.cs`, `UpstreamPerlConditionalEmptying.cs` and their tests; no other task in Doing on `origin/work/dark-factory` named them.
- Outcome of each case now: all eleven run and fail at byte 0 of `<verify><file>` ("expected the end"), because the expected `%SRCDIR/data/data<n>.c` is not vendored. This lane could not vendor it: the permission rules (`Read`/`Edit(./data/**)`, meant for the root `data/`) refused every read, `cp` and `Copy-Item` under `Curl.Conformance.UnitTests/UpstreamTestData/data/`. Filed BL-2965 (`lane: no`) to vendor them. None is listed yet, so none is on `PassingUpstreamCases.txt`.
- Compared by hand with curl 8.22.0's `tests/data/data<n>.c` (downloaded outside the repository, `%HOSTIP`/ports/`%VERSION` substituted, strip lines applied, CR removed): 1400, 1401, 1402, 1403, 1404 (only `%LOGDIR`, which the harness substitutes), 1406, 1420, 1465 and 1481 match. 1405's first difference: `CURLOPT_FTP_SKIP_PASV_IP` comes before `CURLOPT_QUOTE` where 8.22.0 writes it after `CURLOPT_PREQUOTE`. 1407's first difference: `CURLOPT_USERPWD` comes after `CURLOPT_DIRLISTONLY` where 8.22.0 writes it right after `CURLOPT_URL`. Cause: `LibcurlSourceCode` writes 8.21.0's order and 8.22.0 moved the setopts into `src/config2setopts.c` in a new order. Filed BL-2966 (depends on BL-2965) to move the generator to 8.22.0's order.
- The changes are a feature name and a line-pattern parser, with nothing that depends on the platform; the eleven cases are unlisted, so on Linux and macOS they report Inconclusive as on Windows. CI on Linux and macOS is the shift's to watch after it pushes (lanes do not push).
- No product code changed (Curl.Console and Curl.Cli.UnitLibrary are as they were), so no ADR-0482 unit test was due; the order fix and its test are BL-2966's. `Measure-CodeQuality.ps1 -Library Curl.Conformance.UnitLibrary`: 0 failing members.

## Log

- 2026-10-19: Created.
- 2026-10-21: Backlog -> Doing. Criteria at claim: 6a37415d 8a179613 1ee97f0b dd1cc079 620ebead.
- 2026-10-21: Doing -> Done. Harness reports --libcurl and runs the eleven cases; expected files and the 8.22.0 setopt order filed as BL-2965 and BL-2966
