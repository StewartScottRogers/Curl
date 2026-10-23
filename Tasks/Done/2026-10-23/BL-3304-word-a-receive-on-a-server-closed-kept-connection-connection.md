---
id: BL-3304
title: Word a receive on a server-closed kept connection 'Connection was aborted' as the Schannel build does
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/TlsFailureMessages.cs, Curl.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs, Curl.Networking.UnitTests, Curl.Protocol.Http.UnitTests, Curl.Networking.UnitLibrary/StreamConnection.cs, Curl.Networking.UnitLibrary/CLAUDE.md]
requirement: none
created: 2026-10-23
completed: 2026-10-23
---
# BL-3304 — Word a receive on a server-closed kept connection 'Connection was aborted' as the Schannel build does

## Goal

When a kept connection the server has closed is reused, Curl on Windows prints the same Recv failure reason as curl 8.22.0 Schannel ("Connection was aborted").

## Context

Found by BL-3282. Record-CurlExchange.ps1 -Connections 3 with a 401 carrying Negotiate, NTLM, Digest and Basic challenges and Content-Length 1, args -sv --anyauth -u u:p: after "Request completely sent off" on the reused connection real curl prints "* Recv failure: Connection was aborted" then "Connection died, retrying a fresh connect"; Curl prints "* Recv failure: Connection was reset". The differential cases proxy-tunnel and proxy-connect-fail (CONNECT answered 200 Content-Length 0 then closed) showed the same in BL-3233's run, though a recorder run of proxy-tunnel gave "reset" from both. Find which WSA error each side sees (Winsock 10053 vs 10054) and why: likely real curl's send hits the RST first so its recv reports WSAECONNABORTED. Keep Linux and macOS on strerror.

## Acceptance criteria

- [x] Curl's -sv --anyauth exchange above prints "Recv failure: Connection was aborted" on Windows as real curl does, pinned by a focused unit test in the fixed library's .UnitTests project.
- [x] The fast tests pass and Measure-CodeQuality.ps1 reports 0 failing members for each changed library.

## Notes

- Cause: both sides see WSAECONNABORTED (10053); `StreamConnection` rewrote it to reset on Windows (ADR-0419). Measured with a C# file-based app: after the peer's FIN, a write answered with RST makes every receive fail with 10053 (sync, ReceiveAsync, NetworkStream.ReadAsync, polled or not), so ADR-0419's timing window does not apply once a FIN arrived.
- Fix (ADR-0796, decided by Claude under Stewart's delegation): the pre-write check is now `HasUnreadBytesOrEndBeforeWrite` - unread bytes, or readable with a one-byte peek returning 0 (FIN); a failing peek (reset) is not an end, so ADR-0419's rewrite stays for a bare RST. Linux and macOS unchanged.
- Touches widened to `Curl.Networking.UnitLibrary/StreamConnection.cs` (where the rewrite lives) and its `CLAUDE.md` (names the renamed hook); no Doing task on origin/work/dark-factory named either. TlsFailureMessages.cs and HttpProtocolHandler.cs needed no change.
- Measured with Record-CurlExchange.ps1 -Connections 3 (the task's 401 exchange): real curl 8.22.0 and Curl now both print `* Recv failure: Connection was aborted` then `Connection died, retrying a fresh connect (retry count: 1)`. proxy-tunnel (CONNECT 200 Content-Length 0, closed): real curl races (one run reset, one aborted; BL-3233's run saw aborted); Curl now prints aborted.
- Pinned by `StreamConnectionTests.ReadAsync_WhenTheServerClosedTheKeptConnectionBeforeTheWriteAndTheReadIsAborted_FailsWithTheAbort`. Fast tests green; Measure-CodeQuality.ps1 -Library Curl.Networking.UnitLibrary: 0 failing members. Platform-neutral: the rewrite is Windows-only and the test sets it explicitly.

## Log

- 2026-10-23: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: f84b09c6 69edf767.
- 2026-10-23: Doing -> Done. A reused kept connection the server closed prints 'Recv failure: Connection was aborted' on Windows as curl 8.22.0 Schannel does
