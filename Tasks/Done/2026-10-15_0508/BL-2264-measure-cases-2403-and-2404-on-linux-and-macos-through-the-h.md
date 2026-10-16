---
id: BL-2264
title: Measure cases 2403 and 2404 on Linux and macOS through the %HTTP2TLSPORT stand-in and list or fix them
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2245]
touches: [Curl.Conformance.UnitLibrary, Curl.Conformance.UnitTests, Curl.Conformance.Http2Server.UnitLibrary, Curl.Conformance.Http2Server.UnitTests]
requirement: none
created: 2026-10-14
completed: 2026-10-15
---
# BL-2264 — Measure cases 2403 and 2404 on Linux and macOS through the %HTTP2TLSPORT stand-in and list or fix them

## Goal

test2403 is on PassingUpstreamCases.txt or has a follow-up task per cause, and test2404’s remaining skip reason is recorded.

## Context

BL-2245 (2026-10-14) put nghttpx’s TLS front (ALPN h2, X-Forwarded-Proto: https) on %HTTP2TLSPORT = 9018. On Windows 2403 and 2404 skip for http/2, which Curl’s Schannel-matching build lacks (ADR-0495), and the lane machine has no Linux .NET, so they were never measured. The ratchet reports an unlisted case that passes as Inconclusive (passes and can be listed): read that message for test2403 and test2404 in the Linux and macOS CI runs’ test logs (or run CURL_UPSTREAM_CASE=2403 with the Manual category on Linux). test2404 also names <tool>lib2404</tool> and is expected to skip for it.

## Acceptance criteria

- [x] test2403’s result on Linux and macOS is recorded in Notes; it is in Curl.Conformance.UnitTests/PassingUpstreamCases.txt if it passes, otherwise each distinct cause has a follow-up task (or is fixed here in the HTTP/2 stand-in with a focused unit test).
- [x] test2404’s skip reason on Linux is recorded in Notes, with a follow-up task if it is not the <tool>.
- [x] The fast tests are green.

## Notes

- CI (run 38693073429, commit 05161f029, which holds BL-2245): on ubuntu-latest and macos-latest both rows are reported only as `Skipped` (Inconclusive), with no message in the log. Screening explains it: 2403 names `headers-api` in `<features>`, which `UpstreamCurlPlatform` left off, so it skipped with "Curl lacks the feature headers-api" on Linux and macOS.
- Decision (ADR-0500): Curl writes `%{header_json}` and `%header{name}`, which curl writes only with libcurl's headers API, so the harness now reports `headers-api` on every platform. `Curl.Conformance.UnitLibrary` was added to `touches` for this one-line change in `UpstreamCurlPlatform.CommonFeatures`; no other task in Doing on `origin/work/dark-factory` named it.
- Measured on Windows emulating the Linux build (no Linux .NET here; temporary `UpstreamCurlPlatform.Unix` plus `headers-api` and `parsesAsWindowsBuild: false` in `UpstreamConformanceTests`, as BL-2261 did, then reverted), with the Manual test and `CURL_UPSTREAM_CASE`:
  - test2403: passes. Listed in `PassingUpstreamCases.txt`; on Windows it is `SkippedOnThisPlatform` for `http/2`.
  - test2404: "the harness does not act on <client><tool>" (`lib2404`), the expected reason, so no follow-up task.
  - The other command-line `headers-api` cases: 1670 and 1671 pass (on Windows too) and are listed. 764 and 765 differ on `%header{name:all:sep}` -> BL-2267. 2503 (HTTP/3) differs: no header block before the JSON -> BL-2268.
- Quality: `Measure-CodeQuality.ps1 -Library Curl.Conformance.UnitLibrary`: 100% line, 100% branch, 0 failing members, worst CRAP 10. Fast tests green (Curl.Conformance.UnitTests: 3106 passed, 0 failed).

## Log

- 2026-10-14: Created.
- 2026-10-14: Backlog -> Doing.
- 2026-10-15: Doing -> Done. test2403 passes off Windows and is listed (harness reports headers-api, ADR-0500); test2404 skips for its <tool>; 1670 and 1671 listed; BL-2267 and BL-2268 filed
