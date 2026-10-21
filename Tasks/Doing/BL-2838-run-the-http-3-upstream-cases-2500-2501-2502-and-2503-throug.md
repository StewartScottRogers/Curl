---
id: BL-2838
title: Run the HTTP/3 upstream cases 2500, 2501, 2502 and 2503 through the HTTP/3 stand-in
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitTests/PassingUpstreamCases.txt, Curl.Conformance.UnitTests/UpstreamConformanceTests.Http3Cases.cs, Curl.Conformance.UnitLibrary/UpstreamPlatformFeatureTable.cs]
requirement: none
created: 2026-10-19
completed:
---
# BL-2838 — Run the HTTP/3 upstream cases 2500, 2501, 2502 and 2503 through the HTTP/3 stand-in

## Goal

Upstream cases 2500, 2501, 2502 and 2503, which need the `http/3` feature, run through the harness's HTTP/3 stand-in (`UpstreamHttp3Server`) and are on `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` or have a recorded reason in an ADR.

## Context

Gap scorecard 2026-10-19_1329: 2500 and 2503 are `excluded | reference-lacks:http/3`, 2501 (HTTP/3 POST, `Debug,http,http/3`) is `excluded | debug-build-only`, 2502 (`lib2502`, libtest) is `excluded | libcurl-api`. Measured 2026-10-19 on Windows, 2500, 2502 and 2503 are skipped with `Curl lacks the feature http/3` and 2501 with `Curl lacks the feature Debug`.

`UpstreamCurlPlatform` lists `http/3` on no platform: ADR-0540 has Curl refuse `--http3` and `--http3-only` as every reference build does (none has a QUIC library), so upstream's own `runtests.pl` skips the cases for the reference too. ADR-0498 and ADR-0502 describe the HTTP/3 stand-in (`Curl.Conformance.Http3Server.UnitLibrary`, datagrams on port 9019, "http3 cases run off Windows only"), and `UpstreamCaseScreening` already takes `emulatesHttp3`. Curl implements QUIC and HTTP/3 by hand (`Curl.Quic.UnitLibrary`, `Curl.Http3.UnitLibrary`); the root CLAUDE.md says nothing is left out because it is hard.

The task: (1) read ADR-0498, ADR-0502 and ADR-0540 and decide in a new ADR ("Decided by Claude under Stewart's delegation") how `http/3` is reported to a case: either as a harness stand-in for the four cases (the harness reports it while the stand-in server is wired, leaving `curl --version` as the platform's curl prints it) or by keeping it off with the reason; (2) if reported, wire cases 2500 to 2503 and run them on Linux and macOS in CI (the stand-in runs off Windows only, so they are `Inconclusive` on Windows). 2501 also needs `Debug`; read what it needs from `tests/data/test2501` at tag `curl-8_22_0` and use the ADR-0687 style stand-in.

Lanes only run Windows, so the pass on Linux is judged by the `CI` workflow.

## Acceptance criteria

- [ ] A new ADR states for `http/3` whether the harness reports it to cases 2500, 2501, 2502 and 2503 and why, consistent with ADR-0540.
- [ ] If reported: a unit test in `UpstreamCurlPlatformTests` pins that only a case naming the stand-in server gets `http/3`, and cases 2500, 2501, 2502 and 2503 are on `PassingUpstreamCases.txt`, `Inconclusive` on Windows and `Passed` in `CI` on Linux and macOS.
- [ ] If not reported: each case's reason is in the ADR and in Notes, and no case is left unexplained.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` passes, and each changed library keeps 100% line and branch coverage.

## Notes

## Log

- 2026-10-19: Created.
- 2026-10-21: Backlog -> Doing. Criteria at claim: 9bc15d2e 55a9b3c6 9830c497 7bd8d990.
