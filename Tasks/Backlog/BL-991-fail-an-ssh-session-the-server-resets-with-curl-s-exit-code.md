---
id: BL-991
title: Fail an SSH session the server resets with curl's exit code instead of an unhandled IOException
priority: Normal
assignee: Claude
pipeline: protocol
depends-on: []
touches: [Curl.Protocol.Ssh.UnitLibrary, Curl.Protocol.Ssh.UnitTests]
requirement: none
created: 2026-09-29
completed:
---
# BL-991 — Fail an SSH session the server resets with curl's exit code instead of an unhandled IOException

## Goal

An `sftp://` or `scp://` transfer whose server resets or aborts the TCP connection during the SSH handshake (or later) ends with the exit code and message the reference curl gives, not with an unhandled `System.IO.IOException` and its stack trace.

## Context

- Found in BL-575 (2026-09-29): an OpenSSH 10 `sshd` in WSL whose pre-auth child failed to start (exit 127, `libwrap.so.0` missing) reset each connection straight after accept. `curl.exe -v --compressed-ssh -k --key <key> sftp://127.0.0.1:2275/...` printed `* SSH: user '...'`, then `Unhandled exception. System.IO.IOException: Unable to read data from the transport connection: An established connection was aborted by the software in your host machine.` (inner `SocketException` 10053) out of `CurlCommandRunner.TransferAllGroupsAsync`.
- `SshTransport.NegotiateAlgorithmsAsync` maps only `InvalidDataException` and `EndOfStreamException`; `SshIdentificationExchange` and the later reads let `IOException` through. `SshUserAuthentication.TryExchangeAsync` already catches `IOException`.
- Measure first: run the reference curl (Windows mingw build, `Record-CurlExchange.ps1` finds it) against a listener that accepts and resets (extend `Record-CurlExchange.ps1` with a reset-on-accept mode if it has none) and record exit code and stderr; libssh2 probably reports `Failure establishing ssh session: -13, Failed getting banner` (exit 2), but pin what is measured.

## Acceptance criteria

- [ ] The reference curl's exit code and stderr for a connection reset before the banner, and after it, are measured and copied into Notes.
- [ ] `Curl.Protocol.Ssh.UnitTests` has a test per measured case where the fake `IConnection` throws `IOException` from a read, pinning the exit code and message.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Protocol.Ssh.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-29: Created.
