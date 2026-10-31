---
id: BL-4059
title: Send SSH DISCONNECT once when a transfer on a reused connection fails
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Protocol.Ssh.UnitLibrary/SshKeptSession.cs, Curl.Protocol.Ssh.UnitLibrary/SshProtocolHandler.cs, Curl.Protocol.Ssh.UnitTests]
requirement: none
created: 2026-10-29
completed: 2026-10-29
---
# BL-4059 — Send SSH DISCONNECT once when a transfer on a reused connection fails

## Goal

`sftp://h/a sftp://h/missing` puts exactly one `SSH_MSG_DISCONNECT` on the wire, as curl does.

## Context

Found by the shift-end review of 2026-10-28 (code-reviewer, ADR-0710), in the diff `354cb7711...c4b167258`.

- `Curl.Protocol.Ssh.UnitLibrary/SshProtocolHandler.cs:284-296`: when a transfer fails, `TransferAndKeepOrDisconnectAsync` calls `session.ShutDownAsync`.
- On a reused connection the pool entry's `Session` is still that `SshKeptSession`, and the lease was not marked reusable. So when the connection is disposed, `Curl.Networking.UnitLibrary/PoolEntry.cs:93-95` calls `Session.ShutDownAsync` a second time.
- `SshKeptSession.ShutDownAsync` (line 45) has a guard for the SFTP close, but `DisconnectIgnoringFailureAsync` has none. The second call sends a second encrypted `DISCONNECT`.

## Acceptance criteria

- [x] `SshKeptSession.ShutDownAsync` sends `DISCONNECT` at most once, however many times it is called, or the handler leaves the shutdown of a reused connection to the pool.
- [x] A test in `Curl.Protocol.Ssh.UnitTests` runs two SFTP transfers to one host over the in-memory SSH server, the second failing. It checks that the server receives exactly one `SSH_MSG_DISCONNECT`.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Took the first option: `SshKeptSession.ShutDownAsync` now runs once and returns at once on any later call, so the handler's shutdown after a failed transfer and the pool's shutdown at dispose send one `DISCONNECT` between them. The handler and `PoolEntry` are unchanged.
- The in-memory server stops reading at the first `DISCONNECT`, so a second one never shows up in `server.Events`. The test (`Cgr130Tests.ExecuteAsync_AFailedTransferOnAReusedConnection_SendsOneDisconnect`) also counts what the client writes while the test cache shuts down the connection's session. It found 1 write without the guard and finds 0 with it. Server events show one `disconnect`.
- Measure-CodeQuality.ps1 on Curl.Protocol.Ssh.UnitLibrary: 0 failing members. Full build clean; fast tests green on Windows. The change is platform-neutral (no paths, OS errors or certificates).

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: f666f3d5 f9d7ed7d 3d836d5e.
- 2026-10-29: Doing -> Done. SshKeptSession shuts down once; a failed transfer on a reused SFTP connection sends one DISCONNECT
