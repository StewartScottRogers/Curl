---
id: BL-952
title: Compose SshProtocolHandler for scp and sftp in the SSH library
priority: High
assignee: Claude
pipeline: protocol
depends-on: [BL-568]
touches: [Curl.Protocol.Ssh.UnitLibrary, Curl.Protocol.Ssh.UnitTests]
requirement: none
created: 2026-09-29
completed:
---
# BL-952 — Compose SshProtocolHandler for scp and sftp in the SSH library

## Goal

`Curl.Protocol.Ssh.UnitLibrary` has a public `SshProtocolHandler : IProtocolHandler` claiming `scp` and `sftp` (default port 22) that connects through the injected `IConnector`, runs `SshTransport`, checks the host key with `SshHostKeyChecker` from `ITransferContext.Ssh`, authenticates with `SshUserAuthentication` (and BL-568's public key), and runs `SftpFileDownload` or `ScpFileDownload` into the context's output, mapping every `SshTransferException` to its `CurlExitCode` and message.

## Context

- Found by BL-576 on 2026-09-29: every other protocol's `IProtocolHandler` lives in its own library (`TftpProtocolHandler`, `SmbProtocolHandler`, ...), but BL-563 to BL-574 built the SSH pieces without the handler that composes them, so `Curl.Console` has nothing to register. BL-576 registers this handler once it exists.
- Pieces: `Transport/SshTransport.cs`, `HostKeys/SshHostKeyChecker.cs`, `Authentication/SshUserAuthentication.cs`, `Sftp/SftpFileDownload.cs`, `Scp/ScpFileDownload.cs`, `SshConnectionFailure.cs`; options come from `Curl.Protocol.Abstractions.UnitLibrary/SshOptions.cs` (BL-562) and the context's credentials (`-u`, `--key`, `--pass`).
- Failure messages and exit codes are already measured: ADR-0122, ADR-0206, ADR-0212, ADR-0215, ADR-0220, ADR-0225. The handler ends the session after a header failure (ADR-0225).
- `-Q`, `-T`, `-r`, `-C` and directory listing are BL-570 to BL-573 and stay out of this task.

## Acceptance criteria

- [ ] `SshProtocolHandler` exists in `Curl.Protocol.Ssh.UnitLibrary`, claims exactly `scp` and `sftp`, and never constructs a `Socket`.
- [ ] `Curl.Protocol.Ssh.UnitTests` run an `sftp://` and an `scp://` download end to end through the handler against the in-memory SSH peer in `Fakes`, pinning the bytes written and the outcome of BL-569's and BL-574's measured success and failure cases (missing file exit 78, channel open failure exit 79, host key mismatch, authentication failure exit 67).
- [ ] The in-memory peer and a fake connector backed by it can be driven from another test project (public in `Curl.Protocol.Ssh.UnitTests`, or documented as copyable), so BL-576 can run `Curl.Console` against them.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Protocol.Ssh.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-29: Created.
