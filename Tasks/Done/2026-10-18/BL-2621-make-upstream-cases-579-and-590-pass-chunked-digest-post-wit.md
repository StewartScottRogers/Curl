---
id: BL-2621
title: Make upstream cases 579 and 590 pass: chunked digest POST with a read callback, and proxy NTLM
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitTests/PassingUpstreamCases.txt, Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-10-17
completed: 2026-10-18
---
# BL-2621 — Make upstream cases 579 and 590 pass: chunked digest POST with a read callback, and proxy NTLM

## Goal

Upstream cases 579 (lib579, chunked POST from a read callback with Digest authentication and a progress callback) and 590 (lib590, proxy offering Negotiate and NTLM, only NTLM allowed) pass through the runner.

## Context

Found by BL-2533; the ports `Lib579` and `Lib590` are unit tested and match their C sources. Differences: 579 `the program's stdout against <reply><data> differs at byte 0 (line 1): expected "HTTP/1.1 100 Continue\r\n", got the end` (the transfer writes nothing); 590 `<verify><protocol> differs at byte 130 (line 6): expected "GET http://test.remote.example.com/path/590 HTTP/1.1\r\n", got the end` (the NTLM follow-up request is never sent). Run each with `CURL_UPSTREAM_CASE=<n>` and the manual test `NamedUpstreamCase_RunThroughCurl_PassesAsAListedCaseMust`; in the first pass find the engine or Libcurl project at fault and add it to `touches` before any edit (split into two tasks if the causes are in different projects).

## Acceptance criteria

- [x] Each named case that passes through the runner on Windows is added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`; one that still fails is filed with the board script with its differing lines.
- [x] Any product fix has a focused unit test in the fixed library's own `.UnitTests` project (ADR-0482).
- [x] No added case's output depends on the platform.
- [x] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.

## Notes

- 2026-10-18: 590 already passed at claim (fixed by earlier engine work); only added to the list.
- 579: the perform answered `GotNothing` (`Empty reply from server`). The Digest probe
  (`HttpRequestFraming.AsAuthProbe`) dropped chunking, so it sent the lib's
  `Transfer-Encoding: chunked` header with `Content-Length: 0` and no last chunk, and sws waited for
  a chunk that never came. libcurl 8.22.0 keeps `upload_chunky` for the zero-length negotiation post
  and sends `0\r\n\r\n` (the 5 bytes in the case's `Progress: end UL 5/0`). Fix: the probe stays
  chunked when an `-H` value asked for it (`IsChunked && !ChunksForUnknownLength`); curl's own
  chunking of an unknown-length body is still dropped, the probe's length being 0. Focused test
  `ExecuteAsync_DigestProbeOfAChunkedBody_SendsOnlyTheLastChunk`.
- `touches` widened to `Curl.Protocol.Http.UnitLibrary` and `Curl.Protocol.Http.UnitTests`: the fix is
  there; no task in Doing on `origin/work/dark-factory` named either.
- Platform: 579 compares stdout and a progress file of counts; 590 uses the hand-built NTLM on every
  platform. Neither has paths or OS text.
- Measure-CodeQuality.ps1 -Library Curl.Protocol.Http.UnitLibrary: 0 failing members.

## Log

- 2026-10-17: Created.
- 2026-10-18: Backlog -> Doing. Criteria at claim: 92cbe6ef 4963d26c 21e5ddeb 4272a6b9.
- 2026-10-18: Doing -> Done. Upstream cases 579 (chunked Digest POST from a read callback) and 590 (proxy NTLM) pass and are on the ratchet
