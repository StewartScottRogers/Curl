---
id: BL-969
title: Close the SFTP channel after a failed open or quote command, as curl does
priority: Normal
assignee: Claude
pipeline: protocol
depends-on: [BL-572]
touches: [Curl.Protocol.Ssh.UnitLibrary, Curl.Protocol.Ssh.UnitTests]
requirement: none
created: 2026-09-29
completed:
---
# BL-969 — Close the SFTP channel after a failed open or quote command, as curl does

## Goal

When an SFTP download's `OPEN`, a listing's `OPENDIR`, an upload's `OPEN` or a `-Q` command before the transfer fails, Curl sends the channel's `EOF` and `CLOSE` before `DISCONNECT`, as curl 8.21.0 does.

## Context

- Measured 2026-09-29 in BL-572 (ADR-0247, "Consequences"). OpenSSH's `sshd -d` log shows `channel 0: rcvd eof`, `send eof`, `send close` and `rcvd close` before `Received disconnect ... 11: Shutdown`, both for `sftp://.../missing` (exit 78) and for `-Q "rm /missing"` (exit 21). `Curl.Protocol.Ssh.UnitLibrary` throws before `SftpSession.FinishIgnoringFailureAsync`, which leaves the channel open (ADR-0220's decision, made before this was measured).
- The setup is BL-572's: OpenSSH 10.2 in WSL on port 2233 with `sftp-server -l DEBUG3` and `LogLevel DEBUG3` (`~/bl572`), and the reference curl through `Record-CurlExchange.ps1 -NoServer`.
- **BCL first.** Anything the BCL lacks is hand-built in its own library (standing rule, root `CLAUDE.md`, "Decisions", 2026-09-28).

## Acceptance criteria

- [ ] Measured first: the teardown after a failed `OPEN`, `OPENDIR`, upload `OPEN`, `REALPATH` and `-Q` command is copied into Notes.
- [ ] `Curl.Protocol.Ssh.UnitTests` pin `CHANNEL_EOF` and `CHANNEL_CLOSE` before `DISCONNECT` for each measured case, through `InMemorySshServer`'s events.
- [ ] ADR-0220 or a new ADR records the change; `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Protocol.Ssh.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-29: Created.
