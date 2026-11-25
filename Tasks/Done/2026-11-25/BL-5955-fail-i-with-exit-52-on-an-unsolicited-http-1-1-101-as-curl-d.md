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
completed: 2026-11-25
---
# BL-5955 — Fail -I with exit 52 on an unsolicited HTTP/1.1 101 as curl does

## Goal

`curl -v -I` answered with an unsolicited `HTTP/1.1 101` ends as curl does: exit 52, `Empty reply from server`, connection shut down.

## Context

Measured in BL-5948, `Record-CurlExchange.ps1 -Response 'HTTP/1.1 101 Switching Protocols\r\nUpgrade: x\r\n\r\nno\n' -HoldOpenMilliseconds 1500`, `-sS -v -I`: the reference Windows curl writes `{ [3 bytes data]`, `* Empty reply from server`, `* shutting down connection #0`, `curl: (52) Empty reply from server`. Curl exits 0 and writes `* Connection #0 to host 127.0.0.1:18231 left intact` (ADR-0939 covers the 101 without `-I`; `HttpProtocolHandler.ConnectionReuse.cs`, `EndedAtUnsolicitedSwitchingProtocols`). Measure the same 101 with no bytes after its head before changing the exit code.

## Acceptance criteria

- [x] A unit test shows `-I` on an unsolicited `101` exiting with `CurlExitCode.GotNothing`, the `Empty reply from server` and `shutting down connection #0` lines, and a connection not reused.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Measured 2026-11-25 with `Record-CurlExchange.ps1` against the reference curl 8.22.0 (`-sS -v -I`).
  Every case ends with exit 52, `Empty reply from server`, `shutting down connection #0`:
  - `101` with `no\n` in the head's read: `{ [3 bytes data]` at once, without waiting for the close.
  - `101` with nothing after it: curl waits for the close (4 s under `-HoldOpenMilliseconds 4000`),
    then `{ [0 bytes data]`.
  - `-Script`, head, 700 ms pause, `abc`, pause, `defgh`: one read, `{ [3 bytes data]`, then the failure.
  - `Content-Length: 3` and `HTTP/1.0` with `Connection: keep-alive` end the same way; a second URL
    gets a fresh connection `#1`. `-w` gives code 101, size_header 48, size_download 0, num_connects 1.
- After the change Curl matched the reference's exit code, `-v` lines (debug-build lines aside) and
  stdout in all eight recorded cases.
- The whole change is `HttpResponseBodyReader.ThrowIfBodilessSwitchingProtocolsAsync`;
  `ConnectionReuse.cs` and `ResponseExchange.cs` needed none, since a failed exchange already shuts
  its connection down. `-X HEAD` without `-I` still reads the body to the close, unchanged.
- No ADR: matching the measured curl left nothing to decide. ADR-0939 stays true (it covers a body
  that runs to the close, which `-I` never reads).
- `Measure-CodeQuality.ps1 -Library Curl.Original.Protocol.Http.UnitLibrary`: 0 failing members.

## Log

- 2026-11-25: Created.
- 2026-11-25: Backlog -> Doing. Criteria at claim: 89c36d8e 3d836d5e.
- 2026-11-25: Doing -> Done. -I on an unsolicited 101 now exits 52 with curl's data, Empty reply and shutting down lines
