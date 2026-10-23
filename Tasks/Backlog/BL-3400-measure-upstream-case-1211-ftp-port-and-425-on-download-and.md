---
id: BL-3400
title: Measure upstream case 1211 (FTP PORT and 425 on download and timeout) against curl 8.22.0: the QUIT Curl sends
priority: High
assignee: Claude
pipeline: protocol
depends-on: []
touches: [Curl.Protocol.Ftp.UnitLibrary, Curl.Protocol.Ftp.UnitTests, Curl.Conformance.UnitTests/PassingUpstreamCases.txt, Documentation/Planning/Decisions/README.md]
requirement: none
created: 2026-10-23
completed:
---
# BL-3400 — Measure upstream case 1211 (FTP PORT and 425 on download and timeout) against curl 8.22.0: the QUIT Curl sends

## Goal

Upstream case 1211 either passes through Curl and is on the ratchet, or a measurement against the reference curl 8.22.0 shows Curl already sends the same commands as curl and that is recorded.

## Context

Stewart (2026-10-23) wants every test passing. Case 1211 (`ftp://HOST:PORT/1211 -P -`, active mode, 425 on download, then timeout) is disabled upstream as unreliable (ADR-0737). On Windows Curl sends a `QUIT` the case does not expect. Measure first (standing rule): run the same command against a loopback FTP server that answers `425` to `RETR` with the reference curl 8.22.0 (`/ucrt64/bin/curl`, ADR-0018), record the commands each sends (extend `Record-CurlExchange.ps1` for an FTP exchange only if needed), and compare. Read the case's `<protocol>` part and `<errorcode>` for what is expected. Library: `Curl.Protocol.Ftp.UnitLibrary`.

## Acceptance criteria

- [ ] A Notes entry gives the control-connection commands the reference curl 8.22.0 and Curl each send in this exchange, and the exit code each returns.
- [ ] If they differ, Curl is fixed to send curl's commands, with a focused unit test in `Curl.Protocol.Ftp.UnitTests` that fails without the fix (ADR-0482).
- [ ] If curl and Curl agree, a new ADR (decided by Claude under Stewart's delegation, listed in the Decisions README.md) amending ADR-0737 states that 1211 stays off the ratchet because the case itself is unreliable or stale against curl 8.22.0, with the measured commands; otherwise case 1211 ends `Passed` with `$env:CURL_UPSTREAM_CASE='1211'` on Windows and is on `PassingUpstreamCases.txt` after ten passing runs.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

## Log

- 2026-10-23: Created.
