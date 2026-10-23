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
completed: 2026-10-23
---
# BL-3400 — Measure upstream case 1211 (FTP PORT and 425 on download and timeout) against curl 8.22.0: the QUIT Curl sends

## Goal

Upstream case 1211 either passes through Curl and is on the ratchet, or a measurement against the reference curl 8.22.0 shows Curl already sends the same commands as curl and that is recorded.

## Context

Stewart (2026-10-23) wants every test passing. Case 1211 (`ftp://HOST:PORT/1211 -P -`, active mode, 425 on download, then timeout) is disabled upstream as unreliable (ADR-0737). On Windows Curl sends a `QUIT` the case does not expect. Measure first (standing rule): run the same command against a loopback FTP server that answers `425` to `RETR` with the reference curl 8.22.0 (`/ucrt64/bin/curl`, ADR-0018), record the commands each sends (extend `Record-CurlExchange.ps1` for an FTP exchange only if needed), and compare. Read the case's `<protocol>` part and `<errorcode>` for what is expected. Library: `Curl.Protocol.Ftp.UnitLibrary`.

## Acceptance criteria

- [x] A Notes entry gives the control-connection commands the reference curl 8.22.0 and Curl each send in this exchange, and the exit code each returns.
- [x] If they differ, Curl is fixed to send curl's commands, with a focused unit test in `Curl.Protocol.Ftp.UnitTests` that fails without the fix (ADR-0482).
- [x] If curl and Curl agree, a new ADR (decided by Claude under Stewart's delegation, listed in the Decisions README.md) amending ADR-0737 states that 1211 stays off the ratchet because the case itself is unreliable or stale against curl 8.22.0, with the measured commands; otherwise case 1211 ends `Passed` with `$env:CURL_UPSTREAM_CASE='1211'` on Windows and is on `PassingUpstreamCases.txt` after ten passing runs.
- [x] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Measured with `Record-CurlExchange.ps1 -Ftp -FtpIdleMilliseconds 120000 -FtpReply "RETR=150 Opening data connection\r\n425 Can't open data connection"` and `ftp://127.0.0.1:<port>/1211 -P - -v` (the recorder needed no extension; `-P -` gets `200` to `EPRT` and no dial-back, as `NODATACONN425` does):
  - Reference curl 8.22.0 (`/ucrt64/bin/curl`, Schannel, x86_64-w64-mingw32): `USER anonymous`, `PASS ftp@example.com`, `PWD`, `EPRT |1|127.0.0.1|<port>|`, `TYPE I`, `SIZE 1211`, `RETR 1211`, `QUIT`; exit 10, `curl: (10) FTP: The server failed to connect to data port`.
  - Curl (`dotnet run --project Curl.Console --no-build` as the recorder's `-Curl`): the same eight commands, `QUIT` included; exit 10, the same error line.
  - Case runner (`$env:CURL_UPSTREAM_CASE='1211'`): Curl fails only at `QUIT` on line 8 of `<protocol>`. The case expects no `QUIT` and exit 28, and curl 8.22.0 does neither.
- They agree, so no code change and no new unit test (criterion 2 does not apply). `FtpProtocolHandlerActiveModeTests.ExecuteAsync_NegativeReplyBehindThe150_QuitsWithExit10WithoutWaiting` already pins the behaviour. ADR-0802 amends ADR-0737: 1211 stays off the ratchet because the case is stale against curl 8.22.0.
- `dotnet build Curl.slnx -warnaserror` is clean; fast tests green (46 test assemblies passed, 0 failed). No production code changed, so no quality run.

## Log

- 2026-10-23: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: 8481adb5 817ebe6e 3331bee8 c5f8cef6.
- 2026-10-23: Doing -> Done. Measured case 1211 against curl 8.22.0: both send QUIT and exit 10; ADR-0802 keeps the stale case off the ratchet
