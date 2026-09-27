---
id: BL-359
title: Match curl when -L follows a 307 or 308 with a -F body whose file cannot seek
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-298]
touches: [Curl.Core.UnitLibrary, Curl.Core.UnitTests]
requirement: none
created: 2026-09-27
completed:
---
# BL-359 — Match curl when -L follows a 307 or 308 with a -F body whose file cannot seek

## Goal

`curl -L -F f=@<pipe or device> <url>` answered with a 307 or 308 does on the second request what curl 8.21.0 does: the same bytes, or the same exit code and message if curl refuses to rewind.

## Context

- Found in BL-298 (2026-09-27). `ConcatenatedReadStream` (`Curl.Core.UnitLibrary/Multipart`) seeks only when every segment seeks, and `RedirectFollower` rewinds a `StreamBody` only then. A file part streamed from a non-seekable stream (a named pipe, a device) leaves the body non-seekable, so the next hop sends whatever is left of it, chunked - unmeasured against curl.
- curl may fail such a hop with exit 65 (`CURLE_SEND_FAIL_REWIND`, "necessary data rewind wasn't possible"); measure first with `Record-CurlExchange.ps1 -Connections 2` (307 then 200) and a file part read from a pipe, and record the command and bytes in Notes.

## Acceptance criteria

- [ ] With an injected non-seekable file stream, `-L -F f=@<path>` against a 307 then a 200 produces curl 8.21.0's measured second request, or its measured exit code and stderr, pinned in a `RedirectFollowerTests` or `CurlCommandRunnerFormTests` case.
- [ ] `dotnet build -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for every library touched.

## Notes

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
