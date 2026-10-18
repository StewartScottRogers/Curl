---
id: BL-2587
title: Curl.Engine: write the headers of a transfer that fails on multiple Location headers (test580)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary, Curl.Engine.UnitTests, Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests, Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-17
completed: 2026-10-17
---
# BL-2587 — Curl.Engine: write the headers of a transfer that fails on multiple Location headers (test580)

## Goal

A Libcurl transfer with `CURLOPT_HEADER` that fails on a response with several `Location:` headers writes the response's header lines to the write callback before failing with `CURLE_WEIRD_SERVER_REPLY` (8), as libcurl 8.22.0 does.

## Context

Found by BL-2527 running test580 through the ported `lib507` (multi interface): the program's stdout is compared with `<reply><data>` and is empty, expected `HTTP/1.1 302 eat this!` first. Check what libcurl writes before the error and what exit code Curl gives.

## Acceptance criteria

- [x] test580 passes through the runner and is added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`.
- [x] A focused unit test in the fixed library's own `.UnitTests` project pins the fix (ADR-0482).
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- 2026-10-17 (lane 2), measured before any edit: through `CURL_UPSTREAM_CASE=580` Curl's stdout is
  empty where `<reply><datacheck>` expects the 302 head up to `Connection: close`. The exit code is
  not the first difference. Cause: the fix belongs in `Curl.Protocol.Http.UnitLibrary`, not
  `Curl.Engine`. `HttpResponseHeadBuilder.KeepLocation` throws `HttpTransferException(8,
  "Multiple Location headers")` from `AddLine` while the head is still being read.
  `HttpProtocolHandler` writes a head to `context.HeaderOutput` only once it is whole
  (`WriteHeadersAsync(context.HeaderOutput, exchange.Head.HeadBytes, ...)`, around line 1268).
  `OutcomeOfFailure` then reports only the held `-v` lines, so no header byte reaches the write
  callback. libcurl 8.22.0's `http_rw_hd` writes each header line with `Curl_client_write` after
  `http_header` accepts it. The lines before the second Location are therefore written, and the
  rejected line is not.
- Planned fix: on the Multiple-Location failure, write the head bytes committed so far
  (`headText`, which already excludes the rejected line) to `context.HeaderOutput` before
  failing. Pin it with a focused test in `Curl.Protocol.Http.UnitTests` (a 302 with two differing
  Location headers writes the status line and the earlier headers and returns 8). Then add 580 to
  the ratchet; its output does not depend on the platform.
  Leave the other mid-head failures (header without colon, NUL byte) to a task of their own.
- Touches widened to `Curl.Protocol.Http.UnitLibrary`, `Curl.Protocol.Http.UnitTests` and
  `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`, which the fix and the first acceptance
  criterion need. Requeued because BL-2571 (Doing) touches `Curl.Protocol.Http.UnitLibrary` and
  BL-2528 (Doing) touches `PassingUpstreamCases.txt`.

- 2026-10-17 (lane 2), done: `HttpResponseHeadBuilder.KeepLocation` now attaches the head bytes committed
  before the refused line (`HttpTransferException.HeadBytesBeforeFailure`), and
  `HttpProtocolHandler.ReadHeadAsync` writes them to the header output before it rethrows. The
  write sits after the catch, not in it: an `await` in a catch left a hidden branch the coverage
  gate counts. test580 passes and is on the ratchet. The other mid-head failures (NUL byte, header
  without colon) keep writing nothing, as planned; they have no task yet because no upstream case
  shows a difference.

## Log

- 2026-10-17: Created.
- 2026-10-17: Backlog -> Doing. Criteria at claim: 49be4119 904b57ec 3d836d5e.
- 2026-10-17: Doing -> Backlog. touches Curl.Protocol.Http.UnitLibrary, held by BL-2571, and Curl.Conformance.UnitTests/PassingUpstreamCases.txt, held by BL-2528
- 2026-10-17: Backlog -> Doing. Criteria at claim: 49be4119 904b57ec 3d836d5e.
- 2026-10-17: Doing -> Done. test580 writes the head before the second Location and passes on the ratchet
