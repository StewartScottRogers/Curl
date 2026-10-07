---
id: BL-973
title: Close the SFTP channel after a failed open or quote command, as curl does
priority: Normal
assignee: Claude
pipeline: protocol
depends-on: [BL-572]
touches: [Curl.Protocol.Ssh.UnitLibrary, Curl.Protocol.Ssh.UnitTests, Documentation/Planning/Decisions]
requirement: none
created: 2026-09-29
completed: 2026-09-30
---
# BL-973 — Close the SFTP channel after a failed open or quote command, as curl does

## Goal

When an SFTP download's `OPEN`, a listing's `OPENDIR`, an upload's `OPEN` or a `-Q` command before the transfer fails, Curl sends the channel's `EOF` and `CLOSE` before `DISCONNECT`, as curl 8.21.0 does.

## Context

- Measured 2026-09-29 in BL-572 (ADR-0247, "Consequences"). OpenSSH's `sshd -d` log shows `channel 0: rcvd eof`, `send eof`, `send close` and `rcvd close` before `Received disconnect ... 11: Shutdown`, both for `sftp://.../missing` (exit 78) and for `-Q "rm /missing"` (exit 21). `Curl.Protocol.Ssh.UnitLibrary` throws before `SftpSession.FinishIgnoringFailureAsync`, which leaves the channel open (ADR-0220's decision, made before this was measured).
- The setup is BL-572's: OpenSSH 10.2 in WSL on port 2233 with `sftp-server -l DEBUG3` and `LogLevel DEBUG3` (`~/bl572`), and the reference curl through `Record-CurlExchange.ps1 -NoServer`.
- **BCL first.** Anything the BCL lacks is hand-built in its own library (standing rule, root `CLAUDE.md`, "Decisions", 2026-09-28).

## Acceptance criteria

- [x] Measured first: the teardown after a failed `OPEN`, `OPENDIR`, upload `OPEN`, `REALPATH` and `-Q` command is copied into Notes.
- [x] `Curl.Protocol.Ssh.UnitTests` pin `CHANNEL_EOF` and `CHANNEL_CLOSE` before `DISCONNECT` for each measured case, through `InMemorySshServer`'s events.
- [x] ADR-0220 or a new ADR records the change; `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Protocol.Ssh.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- **touches:** added `Documentation/Planning/Decisions` for ADR-0274 and the notes on
  ADR-0220 and ADR-0247; no task in `Doing` names it.
- **Measured 2026-09-30:** curl 8.21.0 (Git for Windows' mingw build, Schannel, libssh2
  1.11.1) against OpenSSH 10.2 in WSL (`~/bl572`, `sshd -ddd`, `sftp-server -l DEBUG3`).
  A failed `REALPATH .` came from a subsystem wrapper (`~/bl572/wrapgone.sh`) that starts
  `sftp-server` in a directory removed beneath it.

  | Case | SFTP requests | Exit / stderr | sshd teardown |
  | --- | --- | --- | --- |
  | `sftp://.../files/missing` | `realpath "."`, `open ... flags READ` -> No such file | 78 `Could not open remote file for reading: No such file or directory` | `rcvd eof`, `send eof`, `send close`, `rcvd close`, `Received disconnect ... 11: Shutdown` |
  | `sftp://.../nodir/` | `realpath "."`, `opendir` -> No such file | 78 `Could not open directory for reading: No such file or directory` | same |
  | `-T f sftp://.../nodir/x.txt` | `realpath "."`, `open ... flags WRITE,CREATE,TRUNCATE` -> No such file | 78 `Upload failed: No such file or directory (2/-31)` | same |
  | `-Q "rm /missing"` on a download, listing and upload | `realpath "."`, `remove name "/missing"` -> No such file | 21 `rm "/missing" failed: No such file or directory` | same |
  | `REALPATH .` refused, download and listing | `realpath "."` -> No such file | 78 `Remote file not found` | same |

- **Change:** `SftpSession.CloseChannelOnFailureAsync` wraps each download, listing and
  upload from `REALPATH` on; an `SshTransferException` closes the channel (`EOF`, wait for
  `CLOSE`, `CLOSE`, a broken connection ignored) before it is passed on. No handle was
  open in any measured case, so none is closed. The fake's new `RefusedPaths` answers
  status 2 for a named path, which is how the tests fail `REALPATH`, an upload `OPEN` and
  a quote command.
- **Tests:** `SshProtocolHandlerTests.FailedSftpTeardown.cs`, 9 cases, each pinning the
  requests, `channel eof`, `channel close`, `disconnect 11 Shutdown` and curl's exit and
  message.

## Log

- 2026-09-29: Created.
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Done. A failed SFTP REALPATH, OPEN, OPENDIR, upload OPEN or -Q command closes the channel with EOF and CLOSE before DISCONNECT, as curl 8.21.0 does (ADR-0274)
