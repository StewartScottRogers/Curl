---
id: BL-2629
title: Make upstream case 661 pass: FTP NOCWD and SINGLECWD with absolute and home-relative paths
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-17
completed:
---
# BL-2629 — Make upstream case 661 pass: FTP NOCWD and SINGLECWD with absolute and home-relative paths

## Goal

Upstream case 661 (lib661, CURLOPT_FTP_FILEMETHOD SINGLECWD and NOCWD across fresh, reused and reset handles) passes through the runner.

## Context

Found by BL-2534; the ports in `Curl.Conformance.LibTest.UnitLibrary` are unit tested and follow their C sources. Differences: 661 `<verify><protocol> differs at byte 111 (line 11): expected "QUIT\r\n", got "EPSV\r\n"`: after the first RETR-less failure of the SINGLECWD download of `//folderA/661` the engine goes on to EPSV where curl quits. Run each with `CURL_UPSTREAM_CASE=<n>` and the manual test `NamedUpstreamCase_RunThroughCurl_PassesAsAListedCaseMust`; in the first pass find the engine or Libcurl project at fault and add it to `touches` before any edit.

## Acceptance criteria

- [ ] Each named case that passes through the runner on Windows is added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`; one that still fails is filed with the board script with its differing lines.
- [ ] Any product fix has a focused unit test in the fixed library's own `.UnitTests` project (ADR-0482).
- [ ] No added case's output depends on the platform.
- [ ] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.

## Notes

## Log

- 2026-10-17: Created.
- 2026-10-18: Backlog -> Doing. Criteria at claim: 92cbe6ef 4963d26c 21e5ddeb 4272a6b9.
