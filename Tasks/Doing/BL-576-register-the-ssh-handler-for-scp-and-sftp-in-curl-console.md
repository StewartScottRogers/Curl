---
id: BL-576
title: Register the SSH handler for scp and sftp in Curl.Console
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-562, BL-569, BL-574, BL-952]
touches: [Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-576 — Register the SSH handler for scp and sftp in Curl.Console

## Goal

`curl sftp://...` and `curl scp://...` run end to end through `Curl.Console` with the SSH handler and the TCP connector, the SSH options (BL-562) and `-u`, `--key`, `--pass`, `-Q`, `-T`, `-r`, `-C` mapped into the context, where today they fail as an unsupported protocol.

## Context

- Conformance audit 2026-09-28, row 35. Handler: BL-563 to BL-574; options: BL-561, BL-562.
- Register in `Curl.Console/CurlComposition.cs`; map options in `TransferContextFactory.cs`; dispatch in `Curl.Core.UnitLibrary/ProtocolDispatcher.cs` must accept `scp`/`sftp` with default port 22; add them to the `-V` protocol list as ADR-0021 requires if that list is built here.

## Acceptance criteria

- [ ] `Curl.Console.UnitTests` run an `sftp://` download and an `scp://` download through a fake connector backed by the in-memory SSH peer from `Curl.Protocol.Ssh.UnitTests` (or an equivalent test double), pinning stdout, stderr and exit code as measured by BL-569 and BL-574.
- [ ] Each SSH option reaches the context unchanged, with tests.
- [ ] `curl -V` lists `scp` and `sftp`, with a test.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Console` reports 100% line and branch coverage and no failing member.

## Notes

- 2026-09-29 (lane 4): there is no SSH handler to register yet. `Curl.Protocol.Ssh.UnitLibrary`
  has the transport, host key check, authentication, `SftpFileDownload` and `ScpFileDownload`,
  but no `IProtocolHandler` composing them, and every other protocol keeps its handler in its
  own library. Writing it in `Curl.Console` would put protocol logic in the executable, so it
  is filed as BL-952 (touches the SSH library and its tests, which BL-568 in `Doing` holds)
  and this task now depends on it. BL-952 also exposes the in-memory SSH peer this task's
  `Curl.Console.UnitTests` need. No code was changed by this run.
- From BL-568 (ADR-0230): `publickey` runs only when `SshUserAuthentication` is given an
  `SshUserKeySource` built from the transfer's `SshOptions`, the file system,
  `Environment.GetEnvironmentVariable` and ADR-0022's credential encoding; without one it
  is skipped.

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Backlog. Waiting on BL-952: no SshProtocolHandler exists in Curl.Protocol.Ssh.UnitLibrary to register
- 2026-09-29: Backlog -> Doing.
