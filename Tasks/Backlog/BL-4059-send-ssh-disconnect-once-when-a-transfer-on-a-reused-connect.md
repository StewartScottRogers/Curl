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
completed:
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

- [ ] `SshKeptSession.ShutDownAsync` sends `DISCONNECT` at most once, however many times it is called, or the handler leaves the shutdown of a reused connection to the pool.
- [ ] A test in `Curl.Protocol.Ssh.UnitTests` runs two SFTP transfers to one host over the in-memory SSH server, the second failing. It checks that the server receives exactly one `SSH_MSG_DISCONNECT`.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-10-29: Created.
