---
id: BL-2667
title: Run upstream case 1901: a CURLOPT_POST read-callback body with Transfer-Encoding chunked sends no chunks
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Libcurl, Libcurl.UnitTests, Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-17
completed:
---
# BL-2667 — Run upstream case 1901: a CURLOPT_POST read-callback body with Transfer-Encoding chunked sends no chunks

## Goal

Upstream case 1901 (lib1901, a `CURLOPT_POST` whose body comes from a read callback with a `Transfer-Encoding: chunked` header and a wrong `CURLOPT_POSTFIELDSIZE` of 4) passes through the runner and is on the ratchet.

## Context

Found by BL-2544, which ported lib1901 (`Curl.Conformance.LibTest.UnitLibrary/Lib1901.cs`; its read callback answers `one`, `two`, `three`, `four`, then 0). Measured on Windows: `<verify><protocol> differs at byte 143 (line 9): expected "3\r\n", got the end` - the request line and headers match (`Transfer-Encoding: chunked`, `Content-Type: application/x-www-form-urlencoded`, no `Expect:`) but no chunk is sent. curl 8.22.0 ignores `CURLOPT_POSTFIELDSIZE` once the user's header says chunked and sends each callback answer as its own chunk. See the engine's `ManagedEasyTransfer.ReadCallbackPostBodyOf` (BL-2586), which takes `PostFieldSize` as the `Content-Length` when it is not negative. Before BL-2544's fix of its own port the case threw `ArgumentException` from the callback, which suggests the engine also hands the read callback a buffer of fewer than 4 bytes; check that too.

## Acceptance criteria

- [ ] A `Libcurl` read-callback POST with a user `Transfer-Encoding: chunked` header sends each callback answer as a chunk and the closing `0` chunk whatever `CURLOPT_POSTFIELDSIZE` says, with a focused unit test in the fixed library's own `.UnitTests`.
- [ ] Case 1901 passes through the runner on Windows and is added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-10-17: Created.
