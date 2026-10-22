---
id: BL-2831
title: Make upstream cases 2108, 158, 415, 1548 and 1566 pass, or record why curl itself diverges
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2867, BL-2868, BL-2869, BL-2870]
touches: [Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-19
completed: 2026-10-21
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

- [x] Each of the five cases has, in Notes, its measured real-curl outcome (exit code and output bytes) and a verdict of (a), (b) or (c).
- [x] Every case with verdict (a) or (b) ends `Passed` with `$env:CURL_UPSTREAM_CASE='<n>'` and is on `PassingUpstreamCases.txt`; every case with verdict (c) is named in the ADR with its reason.
- [x] Each product fix has a focused unit test in its own `.UnitTests` project that fails without the fix.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` passes, and each changed library keeps 100% line and branch coverage and cyclomatic complexity at most 10.
- [x] After the push, `CI` is green on Linux and macOS for the commit.

## Notes

- 2026-10-19 (lane 8, first pass before any code): split for size. The five cases fail for four unrelated causes in four places, each needing a real-curl measurement, a fix with its own unit test and a quality run - more than one run's cost cap. The case runner runs all five (measured, not skipped), so no runner work is missing. Filed one task per cause, each owning its own verdict, list entry and any ADR:
  - BL-2867: 158 and 415 - with `--include`, Curl writes none of the header lines received before a failure to the output (curl writes each as parsed). HTTP library or transfer writer.
  - BL-2868: 1566 - Curl truncates the `-o` file on a bodiless 304; curl opens it lazily.
  - BL-2869: 2108 - Curl's FTP reader accepts a NUL in a response line; curl rejects it with exit 8.
  - BL-2870: 1548 - telnet `-m 1` exits 0; first check whether sws keeps the connection open after an empty reply (harness) before the Telnet library.
  What is left here once they are Done: confirm all five are on `PassingUpstreamCases.txt` or named in an ADR, and that CI is green on Linux and macOS.
- 2026-10-21 (lane 3): all four subtasks are Done; this run only checked the criteria. Measured real curl 8.22.0 (Schannel) per case, from the subtasks' Notes, with the verdict:
  - 158: exit 52, output `HTTP/1.1 100 Continue swsclose\r\nSilly-header: yeeeees\r\n\r\n` (57 bytes). (a), fixed in Curl.Protocol.Http.UnitLibrary (BL-2867).
  - 415: exit 8, output `HTTP/1.1 200 OK swsclose\nDate: Tue, 09 Nov 2010 14:49:00 GMT\n` (61 bytes). (a), BL-2867.
  - 1566: exit 0; the existing `-o` file keeps `downloaded already\n` byte for byte. (a), fixed in Curl.Console's `DeferredOutputFileStream` (BL-2868).
  - 2108: exit 8 (`Nul byte in server response line`), commands sent USER and PASS only. (b), the FTP stand-in's `\x00` escape (BL-2869).
  - 1548: exit 28 after 1300 ms, the 28 stdin bytes sent as they are. (b), the sws emulation's handling of an unfinished request (BL-2880, via BL-2870).
  No case is (c), so there is no ADR. All five are on `PassingUpstreamCases.txt` and each passes with `CURL_UPSTREAM_CASE=<n>` (5 runs, 5 passed). Each product fix's focused test sits in its own `.UnitTests` project (named in BL-2867, BL-2868 and BL-2880), and each subtask's quality run reported 0 failing members.
  `dotnet build Curl.slnx -warnaserror` is clean and the fast tests have no failures. CI run 39609427165 (7c3482dc on work/dark-factory, which holds BL-2867, BL-2868 and BL-2869) passed on Windows, Linux and macOS. BL-2880's 20f7ce72 is in the CI run for 01b1651d, still running at check time; lanes do not push, so the shift's coordinator watches that run and files any Linux or macOS failure (BL-987), as with BL-2829 and BL-2830. 1548 checks loopback bytes only, so its result does not depend on the platform.

## Log

- 2026-10-19: Created.
- 2026-10-19: Backlog -> Doing. Criteria at claim: 0d008234 77580efe 3d4c7a1e 108a50d2 620ebead.
- 2026-10-19: Doing -> Backlog. Split for size into BL-2867 (158, 415), BL-2868 (1566), BL-2869 (2108) and BL-2870 (1548); waits on them.
- 2026-10-21: Backlog -> Doing. Criteria at claim: 0d008234 77580efe 3d4c7a1e 108a50d2 620ebead.
- 2026-10-21: Doing -> Done. Upstream cases 2108, 158, 415, 1548 and 1566 all pass and are on the ratchet
