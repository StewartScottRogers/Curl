---
id: BL-4063
title: Report only a --max-time expiry during DNS lookup as Resolving timed out
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Networking.UnitLibrary/TcpConnector.cs, Curl.Networking.UnitTests]
requirement: none
created: 2026-10-29
completed:
---
# BL-4063 — Report only a --max-time expiry during DNS lookup as Resolving timed out

## Goal

A connect cancelled during its DNS lookup for any reason other than the `-m` limit propagates as a cancellation. It does not fail with exit 28 and a `Resolving timed out after N milliseconds` info line.

## Context

Found by the shift-end review of 2026-10-28 (code-reviewer and silent-failure-hunter, ADR-0710), in the diff `354cb7711...c4b167258` (CGR-284).

- `Curl.Networking.UnitLibrary/TcpConnector.cs:1226-1229` wraps every `OperationCanceledException` from `LookUpAsync` as `CanceledWhileResolvingException`.
- `TcpConnector.cs:776` catches it `when (cancellationToken.IsCancellationRequested)`, which is true for any cancellation of the caller's token. It then reports `Resolving timed out` and returns `CurlExitCode.OperationTimedOut`.
- Cancellations that are not timeouts now look like timeouts:
  - the `--http3` race abandoning the TCP attempt once QUIC wins (`HttpProtocolHandler.RaceQuicAgainstTcpAsync`): a false `-v` line;
  - `-Z --fail-early`;
  - a libcurl progress-callback abort;
  - `curl_multi_remove_handle`.
- Before the change these propagated as cancellation. The method's own comment says an earlier cancellation escapes, which is no longer true.

## Acceptance criteria

- [ ] The `Resolving timed out` result is returned only when the `-m` / connect deadline has been reached. Other cancellations still propagate as `OperationCanceledException`.
- [ ] A test in `Curl.Networking.UnitTests` cancels the caller's token during a lookup that has not reached the deadline. It checks that the connect throws `OperationCanceledException` and writes no `Resolving timed out` info line.
- [ ] The existing CGR-284 tests still pass.
- [ ] The comment at the catch says what the code does.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-10-29: Created.
