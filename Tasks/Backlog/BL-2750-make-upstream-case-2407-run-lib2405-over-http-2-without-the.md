---
id: BL-2750
title: Make upstream case 2407 run lib2405 over HTTP/2 without the 'not a socket' SocketException and hold it on the ratchet
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Conformance.LibTest.UnitLibrary, Curl.Conformance.LibTest.UnitTests, Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-18
completed:
---
# BL-2750 — Make upstream case 2407 run lib2405 over HTTP/2 without the 'not a socket' SocketException and hold it on the ratchet

## Goal

Upstream case 2407 (lib2405 over HTTP/2) passes through Curl and is held on the ratchet.

## Context

Found by BL-2697. Run with the Unix platform's features (the `Platform` line in `UpstreamConformanceTests` set to `UpstreamCurlPlatform.Unix` for one local run), case 2407 fails with `curl threw SocketException: An operation was attempted on something that is not a socket.` This was measured on Windows; check whether Linux and macOS give the same. Port: `Curl.Conformance.LibTest.UnitLibrary/Lib2405.cs` and `Lib2405Run`; the `wakeup` feature is already reported (BL-2661, ADR-0690). Case 2407 is skipped, with run time, on Linux and macOS CI (run 39155312605).

## Acceptance criteria

- [ ] Case 2407 passes when run with the Unix platform's features, and is on `PassingUpstreamCases.txt` without making Windows, Linux or macOS CI red (a focused unit test in `Curl.Conformance.LibTest.UnitTests` pins the fix).
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-10-18: Created.
