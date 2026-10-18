---
id: BL-2626
title: Make upstream cases 653 and 654 pass: a MIME post follows changes made after CURLOPT_MIMEPOST and curl_mime_free
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Libcurl, Libcurl.UnitTests, Curl.Console, Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-17
completed: 2026-10-17
---
# BL-2626 — Make upstream cases 653 and 654 pass: a MIME post follows changes made after CURLOPT_MIMEPOST and curl_mime_free

## Goal

Upstream cases 653 (part data changed between two performs) and 654 (curl_mime_free after curl_easy_duphandle unbinds the original handle) pass through the runner.

## Context

Found by BL-2534; the ports in `Curl.Conformance.LibTest.UnitLibrary` are unit tested and follow their C sources. Differences: 653 `<verify><protocol> differs at byte 350 (line 15): expected "Content-Length: 179\r\n", got "Content-Length: 162\r\n"` (the second post resends the first data); 654 `differs at byte 55 (line 4): expected "Content-Length: 0\r\n", got "Transfer-Encoding: chunked\r\n"` (the freed structure is still posted by the original handle). Libcurl takes a snapshot of the MIME structure at setopt time. Run each with `CURL_UPSTREAM_CASE=<n>` and the manual test `NamedUpstreamCase_RunThroughCurl_PassesAsAListedCaseMust`; in the first pass find the engine or Libcurl project at fault and add it to `touches` before any edit.

## Acceptance criteria

- [x] Each named case that passes through the runner on Windows is added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`; one that still fails is filed with the board script with its differing lines.
- [x] Any product fix has a focused unit test in the fixed library's own `.UnitTests` project (ADR-0482).
- [x] No added case's output depends on the platform.
- [x] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.

## Notes

- First pass: the fault is in `Libcurl` (`CURLOPT_MIMEPOST` stored a snapshot at setopt), so `Libcurl` and `Libcurl.UnitTests` were added to `touches`; no task in Doing on `origin/work/dark-factory` named them (only BL-2706, IMAP).
- Decision (ADR-0675): `CurlEasy` remembers the bound `CurlMime` and writes its body as it is now into the options at each perform and `DupHandle`; once freed, the options carry an empty `HttpPost` form, which the engine already posts as `Content-Length: 0` with no `Content-Type` (BL-2599), libcurl's unbound `MIMEKIND_NONE` part. `CURLOPT_MIMEPOST`/`HTTPPOST` NULL and `curl_easy_reset` unbind it. The multi path is not refreshed.
- `Curl.Console` added to `touches`: `CurlCommandRunner.SetUpToolEasy` freed its `-F` form (`using`) before the perform, which under the new semantics posted an empty body (`CurlCommandRunnerEasyTransferTests.RunAsync_OneOptionOfTheHttpAndProtocolGroupsAndAForm_...` failed); it now keeps the form, as curl's tool frees it at cleanup. No Doing task named `Curl.Console`.
- 653 passes and is listed (plain HTTP, fixed data: no platform dependence). 654's first request now matches; the duplicate's chunked body still differs (`differs at byte 257 (line 13): expected "1c1\r\n", got "c2\r\n"`), filed as BL-2708 (likely the same chunk framing as BL-2624).
- Tests: 5 new `CurlEasyTests` (part changed after setopt, freed after setopt, duplicate keeps its copy, NULL MIMEPOST/HTTPPOST unbinds, reset unbinds). Fast suite green; `Measure-CodeQuality.ps1 -Library Libcurl,Curl.Console`: 0 failing members.

## Log

- 2026-10-17: Created.
- 2026-10-17: Backlog -> Doing. Criteria at claim: 92cbe6ef 4963d26c 21e5ddeb 4272a6b9.
- 2026-10-17: Doing -> Done. Upstream case 653 passes and is listed: a MIME post follows part changes and curl_mime_free (ADR-0675); 654's chunk framing is BL-2708
