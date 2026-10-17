---
id: BL-2646
title: Resume a POST paused by its read callback when the write callback unpauses it so upstream case 1533 passes
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2539]
touches: [Curl.Engine.UnitLibrary, Curl.Engine.UnitTests, Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-17
completed:
---
# BL-2646 — Resume a POST paused by its read callback when the write callback unpauses it so upstream case 1533 passes

## Goal

A 3-byte POST whose read callback answers `CURL_READFUNC_PAUSE` until the response's first bytes arrive, and whose write callback then calls `curl_easy_pause(CURLPAUSE_CONT)`, finishes, and with `CURLOPT_KEEP_SENDING_ON_ERROR` the connection is reused, so upstream case 1533 passes (exit 41, `TEST_ERR_SUCCESS`).

## Context

Found by BL-2539 (port `Lib1533` with its `PausedPoster` in `Curl.Conformance.LibTest.UnitLibrary`). Through the case runner on Windows test1533 ends with `curl did not finish within 20 seconds` and no stderr was kept. The server answers each POST early, before the body (see `Curl.Conformance.UnitTests/UpstreamTestData/test1533.curltest`): the engine must keep receiving while the upload is paused, deliver the response to the write callback, and resume sending when the callback unpauses it from inside the transfer. Expected stderr lines are `<description>: expected: N connections; actual: N connections` for the four requests (1, 1, 1, 0 new connections). The engine is `Curl.Engine.UnitLibrary`.

## Acceptance criteria

- [ ] A unit test in `Curl.Engine.UnitTests` reproduces the stall and passes once it is fixed.
- [ ] Upstream case 1533 passes through the runner on Windows and is added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` (it prints no path, OS error text or certificate).
- [ ] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.
- [ ] `Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary` reports 0 failing members.

## Notes

## Log

- 2026-10-17: Created.
