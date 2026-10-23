---
id: BL-3401
title: Make upstream case 594 (FTP multi PORT upload, no data connection) finish within 20 seconds, as curl 8.22.0 does
priority: High
assignee: Claude
pipeline: protocol
depends-on: []
touches: [Curl.Protocol.Ftp.UnitLibrary, Curl.Protocol.Ftp.UnitTests, Curl.Conformance.UnitTests/PassingUpstreamCases.txt, Documentation/Planning/Decisions/README.md]
requirement: none
created: 2026-10-23
completed:
---
# BL-3401 — Make upstream case 594 (FTP multi PORT upload, no data connection) finish within 20 seconds, as curl 8.22.0 does

## Goal

Upstream case 594 (`ftp://HOST:PORT/path/594 %FTPTIME2 upload`, multi PORT upload, no data connection and no positive preliminary reply) finishes inside the harness's 20 second limit through Curl, or a measurement shows the reference curl 8.22.0 takes as long and that is recorded.

## Context

Stewart (2026-10-23) wants every test passing. Case 594 is disabled upstream as failing too often to be reliable (ADR-0737); on Windows Curl did not finish within 20 seconds. Read the case for what `%FTPTIME2` limits. Measure first (standing rule): time the reference curl 8.22.0 (`/ucrt64/bin/curl`, ADR-0018) and Curl on the same loopback FTP exchange, and find what Curl waits for (a data-connection accept without the case's time limit, a missing `-m`, a retry loop). Inject `TimeProvider`; never `Thread.Sleep`. Library: `Curl.Protocol.Ftp.UnitLibrary`.

## Acceptance criteria

- [ ] A Notes entry gives the wall-clock time of the reference curl 8.22.0 and of Curl for case 594's exchange, and the exit code each returns.
- [ ] If Curl is slower or wrong, it is fixed with a focused unit test in `Curl.Protocol.Ftp.UnitTests` (using a manual `TimeProvider`) that fails without the fix (ADR-0482).
- [ ] If curl takes as long, a new ADR (decided by Claude under Stewart's delegation, listed in the Decisions README.md) amending ADR-0737 records that 594 stays off the ratchet with the measured times; otherwise case 594 ends `Passed` with `$env:CURL_UPSTREAM_CASE='594'` on Windows and is on `PassingUpstreamCases.txt` after ten passing runs.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

## Log

- 2026-10-23: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: 4d37ec2a e4e018ed 538f29c2 c5f8cef6.
