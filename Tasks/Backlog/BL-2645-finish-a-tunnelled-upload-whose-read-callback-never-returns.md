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
completed:
---
# BL-2645 — Finish a tunnelled upload whose read callback never returns 0 at CURLOPT_INFILESIZE so upstream cases 1525 to 1527 pass

## Goal

A verbose PUT through an HTTP proxy with `CURLOPT_HTTPPROXYTUNNEL`, `CURLOPT_INFILESIZE` 13 and a read callback that answers 13 bytes on every call finishes once 13 bytes are sent, as upstream's does, so upstream cases 1525, 1526 and 1527 pass.

## Context

Found by BL-2539 (port `LibProxyTunnelUpload` in `Curl.Conformance.LibTest.UnitLibrary`, programs lib1525 to lib1527, with `CURLOPT_PROXYHEADER` and `CURLOPT_HEADEROPT` unified or separate, and lib1527 sending `Expect: 100-continue`). Through the case runner on Windows all three cases end with `curl did not finish within 20 seconds` and no stderr was kept, so the first step is to find where the transfer stalls: the CONNECT through the proxy, the upload reading past `CURLOPT_INFILESIZE` (upstream stops reading at the size), or the `Expect: 100-continue` wait. `LibProxyTunnelUpload.HelloCloudReader.Read` is upstream's `t1525_read_cb` byte for byte. The engine is `Curl.Engine.UnitLibrary`; the cases' expected protocol and proxy bytes are in `Curl.Conformance.UnitTests/UpstreamTestData/test1525.curltest` to `test1527.curltest`.

## Acceptance criteria

- [ ] A unit test in `Curl.Engine.UnitTests` reproduces the stall the cases show and passes once it is fixed.
- [ ] Upstream cases 1525, 1526 and 1527 pass through the runner on Windows and are added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` (none prints a path, OS error text or certificate); a case still failing is filed as its own task with its differing lines.
- [ ] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.
- [ ] `Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary` reports 0 failing members.

## Notes

## Log

- 2026-10-17: Created.
