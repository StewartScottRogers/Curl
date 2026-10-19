---
id: BL-2862
title: Report the --libcurl feature to the harness and make upstream cases 1400 to 1407, 1420, 1465 and 1481 pass
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitTests/PassingUpstreamCases.txt, Curl.Conformance.UnitLibrary/UpstreamCurlPlatform.cs, Curl.Conformance.UnitTests/UpstreamCurlPlatformTests.cs, Curl.Console/CurlCommandRunner.cs, Curl.Cli.UnitLibrary, Curl.Cli.UnitTests]
requirement: none
created: 2026-10-19
completed:
---
# BL-2862 — Report the --libcurl feature to the harness and make upstream cases 1400 to 1407, 1420, 1465 and 1481 pass

## Goal

The harness reports the `--libcurl` feature, so upstream cases 1400, 1401, 1402, 1403, 1404, 1405, 1406, 1407, 1420, 1465 and 1481 run, and each is on `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` or has a recorded reason; the C source `curl --libcurl` writes matches upstream's expected text byte for byte.

## Context

Gap scorecard 2026-10-19_1329: 1400, 1402, 1403, 1405, 1406, 1407, 1420 and 1465 are `excluded | reference-lacks:--libcurl`; 1401 (`reference-lacks:cookies`), 1404 (`reference-lacks:Mime`) and 1481 (`reference-lacks:proxy`) are excluded for the same feature. Measured 2026-10-19 on Windows all eleven are skipped with `Curl lacks the feature --libcurl`. `--libcurl <file>` writes a C program equivalent to the command line; `Curl.Console/CurlCommandRunner.cs` already writes one (`LibcurlFile`, `Curl.Cli.UnitLibrary`), but `UpstreamCurlPlatform` keeps the case feature `--libcurl` off ("libcurl's build and API features, which a command-line tool does not have"), so the cases never ran. Each case runs a command with `--libcurl %LOGDIR/test<n>.c` and compares the written file (`<verify><file>`, possibly with `<stripfile>` lines that drop version or date comments) with upstream's expected C text, the exact bytes curl 8.22.0 writes (https://curl.se/docs/manpage.html, `--libcurl`; https://github.com/curl/curl/tree/curl-8_22_0/tests/data/test1400): the `CURLOPT_*` calls in the order curl sets them, the `curl_slist` and mime blocks, and the trailing cleanup.

Step 1: report `--libcurl` in `UpstreamCurlPlatform` (the tool implements it) and run the eleven. Step 2: for each difference, decide whether `CommandLineOptions` records the option in the wrong order or text, and fix `Curl.Console`/`Curl.Cli.UnitLibrary` with a focused unit test in its `.UnitTests` project (ADR-0482). Expect several differences; this is where the work is. If a lane reaches the time of one run before all eleven pass, list those that pass and file the rest as one new task per cause. Keep `curl --ai-help` right if an option's text changes.

## Acceptance criteria

- [ ] `UpstreamCurlPlatform` lists `--libcurl` on every platform; `UpstreamCurlPlatformTests` pins it.
- [ ] Each of the eleven cases has its outcome in Notes; every case that ends `Passed` with `$env:CURL_UPSTREAM_CASE='<n>'` on Windows is on `PassingUpstreamCases.txt`, and each that differs has its first difference in Notes and a follow-up task naming the cause.
- [ ] Each product fix has a focused unit test in `Curl.Cli.UnitTests` or `Curl.Console`'s test project that fails without it.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` passes, and each changed library keeps 100% line and branch coverage and complexity at most 10.
- [ ] After the push, `CI` is green on Linux and macOS for the commit.

## Notes

## Log

- 2026-10-19: Created.
