---
id: BL-5955
title: Fail -I with exit 52 on an unsolicited HTTP/1.1 101 as curl does
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Original.Protocol.Http.UnitLibrary/HttpProtocolHandler.ConnectionReuse.cs, Curl.Original.Protocol.Http.UnitLibrary/HttpProtocolHandler.ResponseExchange.cs, Curl.Original.Protocol.Http.UnitLibrary/HttpResponseBodyReader.cs, Curl.Original.Protocol.Http.UnitTests/HeadUnsolicitedSwitchingProtocolsTests.cs]
requirement: none
created: 2026-11-25
completed:
---
# BL-5955 — Fail -I with exit 52 on an unsolicited HTTP/1.1 101 as curl does

## Goal

`curl -v -I` answered with an unsolicited `HTTP/1.1 101` ends as curl does: exit 52, `Empty reply from server`, connection shut down.

## Context

Measured in BL-5948, `Record-CurlExchange.ps1 -Response 'HTTP/1.1 101 Switching Protocols\r\nUpgrade: x\r\n\r\nno\n' -HoldOpenMilliseconds 1500`, `-sS -v -I`: the reference Windows curl writes `{ [3 bytes data]`, `* Empty reply from server`, `* shutting down connection #0`, `curl: (52) Empty reply from server`. Curl exits 0 and writes `* Connection #0 to host 127.0.0.1:18231 left intact` (ADR-0939 covers the 101 without `-I`; `HttpProtocolHandler.ConnectionReuse.cs`, `EndedAtUnsolicitedSwitchingProtocols`). Measure the same 101 with no bytes after its head before changing the exit code.

## Acceptance criteria

- [ ] A unit test shows `-I` on an unsolicited `101` exiting with `CurlExitCode.GotNothing`, the `Empty reply from server` and `shutting down connection #0` lines, and a connection not reused.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-25: Created.
- 2026-11-25: Backlog -> Doing. Criteria at claim: 89c36d8e 3d836d5e.
