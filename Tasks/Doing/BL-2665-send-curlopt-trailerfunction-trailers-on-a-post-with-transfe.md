---
id: BL-2665
title: Send CURLOPT_TRAILERFUNCTION trailers on a POST with Transfer-Encoding: chunked (case 1598)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Libcurl, Libcurl.UnitTests, Curl.Engine.UnitLibrary, Curl.Engine.UnitTests, Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests, Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-17
completed:
---
# BL-2665 — Send CURLOPT_TRAILERFUNCTION trailers on a POST with Transfer-Encoding: chunked (case 1598)

## Goal

A POST whose `CURLOPT_HTTPHEADER` holds `Transfer-Encoding: chunked` sends its body chunked and the lines `CURLOPT_TRAILERFUNCTION` adds in the closing chunk, so upstream case 1598 (lib1598) passes.

## Context

Found by BL-2543, which ported lib1598. The runner reports `<verify><protocol> differs at byte 226 (line 11): expected "my-super-awesome-trailer: trail1\r\n", got "\r\n"`: the trailers are sent for a chunked upload (case 1591 passes) but not for a `CURLOPT_POSTFIELDS` body made chunked by the header.

## Acceptance criteria

- [ ] Upstream case 1598 passes through the runner and is added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project covers the fix (ADR-0482).
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-10-17: Created.
- 2026-10-18: Backlog -> Doing. Criteria at claim: d706bc46 48be2d2b 3d836d5e.
