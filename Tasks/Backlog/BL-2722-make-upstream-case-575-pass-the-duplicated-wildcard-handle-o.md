---
id: BL-2722
title: Make upstream case 575 pass: the duplicated wildcard handle opens a new FTP connection after QUIT
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Libcurl, Libcurl.UnitTests, Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-18
completed:
---
# BL-2722 — Make upstream case 575 pass: the duplicated wildcard handle opens a new FTP connection after QUIT

## Goal

Upstream case 575 (lib575: FTP wildcard download with `curl_easy_duphandle` and the multi interface) passes through the case runner and is on `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`.

## Context

Found by BL-2618, which emulated ftpserver.pl's `CWD fully_simulated` directories so 574, 1113, 1162 and 1163 pass. 575 now runs and differs in the `Libcurl` engine:
`<verify><protocol> differs at byte 400 (line 38): expected "QUIT\r\n", got "EPSV\r\n"`. After the first handle's two
wildcard performs, libcurl (curl 8.22.0, `tests/libtest/lib575.c`) sends `QUIT` and the duplicated handle, run on a multi
handle, logs in again on a new connection (`USER anonymous`, `PASS`, `PWD`, `CWD fully_simulated`, `CWD UNIX`, `EPSV`,
`TYPE A`, `LIST`, ...). Curl's engine sends the third round's `EPSV` on the first connection instead. Start from lib575's
handle lifecycle (`curl_easy_cleanup` of the original, `curl_easy_duphandle`, `curl_multi_add_handle`) and where the
`Libcurl` engine keeps FTP connections for reuse. Debug with `CURL_UPSTREAM_CASE=575` (see `Curl.Conformance.UnitTests/CLAUDE.md`).

## Acceptance criteria

- [ ] Upstream case 575 passes through the runner on Windows and is added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`.
- [ ] A focused unit test in `Libcurl.UnitTests` pins the connection behaviour the fix changes (ADR-0482).
- [ ] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.

## Notes

## Log

- 2026-10-18: Created.
