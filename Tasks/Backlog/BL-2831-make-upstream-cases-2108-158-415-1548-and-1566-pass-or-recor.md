---
id: BL-2831
title: Make upstream cases 2108, 158, 415, 1548 and 1566 pass, or record why curl itself diverges
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-19
completed:
---
# BL-2831 — Make upstream cases 2108, 158, 415, 1548 and 1566 pass, or record why curl itself diverges

## Goal

Each of upstream cases 2108, 158, 415, 1548 and 1566 is either on `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` or has a recorded reason, and where Curl is wrong it is fixed.

## Context

The gap office (scorecard 2026-10-19_1329) measures 158, 415, 1548 and 1566 `match | reference-diverges` (the case failed in process but the real reference curl answers the same way as Curl) and 2108 `match`. Measured 2026-10-19 on Windows, all five fail:

- 158 (multipart formpost with only a 100 reply) and 415 (HTTP response with control code then negative Content-Length): the `--output` file against `<reply><data>` differs at byte 0, expected `HTTP/1.1 100 Continue swsclose` / `HTTP/1.1 200 OK swsclose`, got an empty file.
- 1548 (TELNET read stdin without upload file, `-m 1`): expected exit code 28, got 0.
- 1566 (`--etag-compare` that gets a 304 back should not overwrite the file): the `--output` file differs at byte 0, expected `downloaded already`.
- 2108 (FTP rejects a nul byte in a server response line): `<verify><protocol>` has an extra `PWD` where the case expects the end (the FTP handler carried on after the NUL).

For each case, measure real curl first (`Record-CurlExchange.ps1`; the Schannel build on Windows, OpenSSL on Linux, root CLAUDE.md "Decisions") and compare with upstream's expectation in `tests/data/test<n>` of curl 8.22.0. "reference-diverges" means the reference curl does not meet the case's expectation on the gap runner either; upstream may rely on a runtests.pl behaviour (`swsclose` handling, `-m` timing, a test server quirk) the harness lacks. Outcomes allowed per case: (a) Curl is wrong - fix it, with a focused unit test in the fixed library's own `.UnitTests` project (ADR-0482), and list the case; (b) the harness is wrong - fix the harness and list the case; (c) the case cannot pass in a platform-neutral harness - record the reason in Notes and in a single ADR "Decided by Claude under Stewart's delegation" naming the case.

Any fix's folder must be added to this task's `touches` in the same commit (likely `Curl.Protocol.Ftp.UnitLibrary`, `Curl.Protocol.Telnet.UnitLibrary`, `Curl.Protocol.Http.UnitLibrary` and their `.UnitTests`).

## Acceptance criteria

- [ ] Each of the five cases has, in Notes, its measured real-curl outcome (exit code and output bytes) and a verdict of (a), (b) or (c).
- [ ] Every case with verdict (a) or (b) ends `Passed` with `$env:CURL_UPSTREAM_CASE='<n>'` and is on `PassingUpstreamCases.txt`; every case with verdict (c) is named in the ADR with its reason.
- [ ] Each product fix has a focused unit test in its own `.UnitTests` project that fails without the fix.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` passes, and each changed library keeps 100% line and branch coverage and cyclomatic complexity at most 10.
- [ ] After the push, `CI` is green on Linux and macOS for the commit.

## Notes

## Log

- 2026-10-19: Created.
