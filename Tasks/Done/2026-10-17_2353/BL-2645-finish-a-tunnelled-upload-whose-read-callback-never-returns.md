---
id: BL-2645
title: Finish a tunnelled upload whose read callback never returns 0 at CURLOPT_INFILESIZE so upstream cases 1525 to 1527 pass
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2539]
touches: [Curl.Engine.UnitLibrary, Curl.Engine.UnitTests, Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-17
completed: 2026-10-17
---
# BL-2645 — Finish a tunnelled upload whose read callback never returns 0 at CURLOPT_INFILESIZE so upstream cases 1525 to 1527 pass

## Goal

A verbose PUT through an HTTP proxy with `CURLOPT_HTTPPROXYTUNNEL`, `CURLOPT_INFILESIZE` 13 and a read callback that answers 13 bytes on every call finishes once 13 bytes are sent, as upstream's does, so upstream cases 1525, 1526 and 1527 pass.

## Context

Found by BL-2539 (port `LibProxyTunnelUpload` in `Curl.Conformance.LibTest.UnitLibrary`, programs lib1525 to lib1527, with `CURLOPT_PROXYHEADER` and `CURLOPT_HEADEROPT` unified or separate, and lib1527 sending `Expect: 100-continue`). Through the case runner on Windows all three cases end with `curl did not finish within 20 seconds` and no stderr was kept, so the first step is to find where the transfer stalls: the CONNECT through the proxy, the upload reading past `CURLOPT_INFILESIZE` (upstream stops reading at the size), or the `Expect: 100-continue` wait. `LibProxyTunnelUpload.HelloCloudReader.Read` is upstream's `t1525_read_cb` byte for byte. The engine is `Curl.Engine.UnitLibrary`; the cases' expected protocol and proxy bytes are in `Curl.Conformance.UnitTests/UpstreamTestData/test1525.curltest` to `test1527.curltest`.

## Acceptance criteria

- [x] A unit test in `Curl.Engine.UnitTests` reproduces the stall the cases show and passes once it is fixed.
- [x] Upstream cases 1525, 1526 and 1527 pass through the runner on Windows and are added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` (none prints a path, OS error text or certificate); a case still failing is filed as its own task with its differing lines.
- [x] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.
- [x] `Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary` reports 0 failing members.

## Notes

- Where it stalled: not the CONNECT or the 100-continue wait. The engine's `CallbackReadStream` could not seek, so
  the HTTP handler sent the upload chunked and read the callback until it returned 0, which `t1525_read_cb` never
  does. `CURLOPT_INFILESIZE` reached only `CURLINFO_CONTENT_LENGTH_UPLOAD`.
- Fix: `CallbackReadStream.DeclaredLength` (set by `ManagedEasyTransfer.DeclaredUploadLengthOf` for `http`/`https`
  with a known size) asks for no more than is left, ends there, and seeks with that `Length`, so the request goes
  with `Content-Length: 13`, as libcurl's `cr_in_read`. Held to HTTP only, as other handlers read `CanSeek` in
  their own ways (ADR-0676).
- Case 1527 then differed in the CONNECT: `CURLOPT_HEADEROPT` was stored and never read. Under `Unified` the
  CONNECT now carries `CURLOPT_HTTPHEADER` (`EngineComposition.ConnectHeadersOf`); the forwarded request gets no
  proxy list. Same library, inside `touches`.
- Upstream cases 1576 and 3034 (also a sized read-callback HTTP upload) pass too and went on the ratchet; 1948 and
  1978 still fail for other reasons (1948's second POST, 1978's AWS SigV4 headers), not this one.
- Measured: fast tests 42 projects green (Curl.Engine.UnitTests 1836 passed, Curl.Conformance.UnitTests 3354
  passed); `Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary` 0 failing members.
- Stall repro test: `ManagedEasyTransferTests.PerformAsync_PutWithInFileSizeAndAReadCallbackThatNeverEnds_...`
  (would hit its 10 s cancel and fail before the fix).

## Log

- 2026-10-17: Created.
- 2026-10-17: Backlog -> Doing. Criteria at claim: 9503dea4 dfad422d 4272a6b9 c4aff572.
- 2026-10-17: Doing -> Done. A sized read-callback HTTP upload stops at CURLOPT_INFILESIZE with Content-Length, and CURLOPT_HEADEROPT unified sends CURLOPT_HTTPHEADER in the CONNECT; upstream 1525-1527, 1576 and 3034 pass
