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
completed: 2026-10-19
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

- [x] Upstream case 575 passes through the runner on Windows and is added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`.
- [x] A focused unit test in `Libcurl.UnitTests` pins the connection behaviour the fix changes (ADR-0482).
- [x] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.

## Notes

- 2026-10-19 (lane 2): case 575 already passed when first run through the runner (`CURL_UPSTREAM_CASE=575`): BL-2629 (curl_easy_cleanup closes the handle's own connections, FTP sending QUIT) and BL-2636 landed after this task was filed and fixed it, so no production change was needed. Added 575 to the ratchet and `Libcurl.UnitTests/Easy/CurlEasyDuplicateAfterCleanupTests` (lib575's lifecycle through the real engine over a pooling connector and a scripted connection: the duplicate, performed in a multi handle after the original is cleaned up, opens a second connection). Fast tests: all green; Libcurl.UnitTests 1315 passed.

## Log

- 2026-10-18: Created.
- 2026-10-19: Backlog -> Doing. Criteria at claim: 078dffb6 0bc0233d 4272a6b9.
- 2026-10-19: Doing -> Done. Upstream case 575 passes and is on the ratchet; a Libcurl test pins that a duplicate performed after cleanup opens a new connection
