---
id: BL-2827
title: List the 17 gap-matched upstream cases that run only on Linux and macOS: 308, 310, 313, 469, 969, 1056, 1158, 1186, 1704, 1714, 2034, 2037, 2041, 2090, 3000, 3001, 3016
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-19
completed:
---
# BL-2827 — List the 17 gap-matched upstream cases that run only on Linux and macOS: 308, 310, 313, 469, 969, 1056, 1158, 1186, 1704, 1714, 2034, 2037, 2041, 2090, 3000, 3001, 3016

## Goal

Upstream cases 308, 310, 313, 469, 969, 1056, 1158, 1186, 1704, 1714, 2034, 2037, 2041, 2090, 3000, 3001 and 3016, which the gap office measures as `match` on Linux, are on `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` and are `Inconclusive` (not failed) on Windows.

## Context

The gap office runs this repository's own case runner on Linux and macOS in `gap-platforms.yml`. Its scorecard 2026-10-19_1329 gives these 17 cases `match` on Linux, yet they are off the ratchet. Measured 2026-10-19 on Windows, each is skipped by its `<features>` because only another platform's Curl has them: 308 needs `OpenSSL`; 310, 313, 2034, 2037, 2041, 2090, 3000 and 3001 need `!Schannel`; 469, 969, 1056, 1158, 1186, 1714 and 3016 need `!win32`; 1704 needs `h2c` (Windows' Schannel build has no HTTP/2, BL-2230).

ADR-0488 makes exactly this safe: a listed case whose features only another platform's Curl meets is `Inconclusive` where it cannot run, and a pass or fail on Linux and macOS is judged by the `CI` workflow. `UpstreamCurlPlatform.Unix` and `.MacOS` hold the feature sets; `UpstreamCaseScreening.FindFeatureOfAnotherPlatform` is the test.

The ratchet reads the file as a set, one number per line, and makes no task overlap (ADR-0689). The gap office's headline is the lowest platform's score (ADR-0505), so a macOS-only failure matters too.

## Acceptance criteria

- [ ] Each of the 17 cases was run with `$env:CURL_UPSTREAM_CASE='<n>'` as `Curl.Conformance.UnitTests/CLAUDE.md` ("Debugging one case") describes, and its Windows outcome is in Notes (expected: skipped for the feature named above).
- [ ] Every one of the 17 cases is on `PassingUpstreamCases.txt`, one number per line, in numeric order; a case whose Windows run is a failure rather than the skip named above is not listed and its failure text is in Notes.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test Curl.Conformance.UnitTests --filter "TestCategory!=Integration"` passes, with the added rows `Inconclusive` (feature of another platform), never `Failed`.
- [ ] After the push the `CI` workflow is green on Linux, macOS and Windows for the commit; each case that fails on Linux or macOS is taken off the list again and its failure text is in Notes, and a follow-up task is filed for it.

## Notes

## Log

- 2026-10-19: Created.
- 2026-10-19: Backlog -> Doing. Criteria at claim: fa764056 336c04c9 88890bb1 393b8c68.
- 2026-10-19: Doing -> Backlog. Requeued: left in Doing with no lane and no work when the shift stopped at the weekly token limit.
