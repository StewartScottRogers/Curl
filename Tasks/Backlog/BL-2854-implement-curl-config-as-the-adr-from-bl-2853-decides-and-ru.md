---
id: BL-2854
title: Implement curl-config as the ADR from BL-2853 decides and run upstream cases 1013, 1014, 1022 and 1023
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2853]
touches: [Curl.Conformance.UnitTests/PassingUpstreamCases.txt, Curl.Conformance.UnitLibrary/UpstreamCaseScreening.cs, Curl.Conformance.UnitLibrary/UpstreamCaseRunner.cs, Curl.Conformance.UnitTests/UpstreamCaseScreeningTests.cs]
requirement: none
created: 2026-10-19
completed:
---
# BL-2854 — Implement curl-config as the ADR from BL-2853 decides and run upstream cases 1013, 1014, 1022 and 1023

## Goal

`curl-config` answers its options as the ADR from BL-2853 decides, and upstream cases 1013, 1014, 1022 and 1023 (which compare `curl --version` with `curl-config`) pass through the case runner and are on `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`.

## Context

BL-2853's ADR decides whether Curl ships `curl-config`, its project and each option's answer; follow it, and if it decides to ship nothing, implement the harness stand-in it names. The four cases (gap scorecard 2026-10-19_1329: `unmeasured | harness-unsupported`; skipped today with `test1013.pl compares with ../curl-config, which Curl does not ship`) each run `curl --version`, then a postcheck `%PERL %SRCDIR/libtest/test1013.pl ../curl-config %LOGDIR/stdout<n> <protocols|features>` (1013, 1014) or `test1022.pl ... <version|vernum>` (1022, 1023) in upstream curl 8.22.0 (https://github.com/curl/curl/tree/curl-8_22_0/tests/libtest). Those scripts compare what `curl --version` printed (its `Protocols:`, `Features:`, version and numeric version) with `curl-config`'s answer. `UpstreamCaseScreening.CurlConfigComparison` is today's skip; the postcheck needs a C# interpretation in the manner of `UpstreamTest610Script` (never Perl or Python). Read both scripts for the exact comparison (ordering, which feature names `curl-config` omits).

## Acceptance criteria

- [ ] The executable or stand-in the ADR names exists, in `Curl.slnx` where the layout rules put it, with a `CLAUDE.md` that says what it holds, and answers `--version`, `--vernum`, `--protocols`, `--feature`, `--help` and the other options of the ADR exactly as the ADR lists (unit tests pin each answer, and an unknown option exits as upstream's `curl-config` does: measure the reference).
- [ ] `UpstreamCaseScreening` no longer skips cases 1013, 1014, 1022 and 1023, and a C# interpretation of `test1013.pl` and `test1022.pl` runs their postchecks; tests pin both.
- [ ] Cases 1013, 1014, 1022 and 1023 each end `Passed` with `$env:CURL_UPSTREAM_CASE='<n>'` on Windows and are on `PassingUpstreamCases.txt`.
- [ ] `curl --version` and `curl-config` agree on every platform's feature and protocol lists by a unit test that reads both from `Libcurl`'s `curl_version_info`.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` passes, each changed library keeps 100% line and branch coverage and complexity at most 10, and `dotnet publish` of the new executable still succeeds as native AOT if it is one.
- [ ] After the push, `CI` is green on Linux and macOS for the commit.

## Notes

## Log

- 2026-10-19: Created.
