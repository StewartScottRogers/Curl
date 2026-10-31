---
id: BL-4330
title: Move the SSH connection layer into Curl.Ssh.UnitLibrary and add direct-tcpip channels
priority: Low
assignee: Claude
pipeline: direct
depends-on: [BL-4365, BL-4329]
touches: [Curl.Ssh.UnitLibrary/Connection, Curl.Ssh.UnitLibrary/SshSessionConnection.cs, Curl.Ssh.UnitTests/Connection, Curl.Protocol.Ssh.UnitLibrary, Curl.Protocol.Ssh.UnitTests/Connection, Curl.Protocol.Ssh.UnitTests/GlobalUsings/Curl.Protocol.Ssh.Connection.cs, Curl.Protocol.Ssh.UnitTests/GlobalUsings/Curl.Ssh.Connection.cs, Curl.Protocol.Ssh.UnitTests/Fakes, Curl.Protocol.Ssh.UnitTests/SshDiagnosticLogTests.cs, Curl.Protocol.Ssh.UnitTests/Scp/ScpFileDownloadTests.MaxFileSize.cs, Curl.Ssh.UnitLibrary/CLAUDE.md, Curl.Conformance.SshServer.UnitLibrary/SshServerSessionChannel.cs, Curl.Conformance.SshServer.UnitTests/SshServerConnectorTests.cs, Curl.Conformance.SshServer.UnitTests/SshServerScpProcessTests.cs, Curl.Conformance.SshServer.UnitTests/SshServerSessionChannelTests.cs, Curl.Conformance.SshServer.UnitTests/SshServerSftpProcessTests.cs]
requirement: none
created: 2026-10-29
completed: 2026-10-30
---
# BL-4330 — Move the SSH connection layer into Curl.Ssh.UnitLibrary and add direct-tcpip channels

## Goal

`Curl.Ssh.UnitLibrary` provides an SSH client session (connect over an `IConnection`, key exchange, host key check, user authentication) and the connection protocol (RFC 4254), including opening a `direct-tcpip` channel (RFC 4254 section 7.2) exposed as an `IConnection`, moved from `Connection` and `SshSessionConnection.cs` of `Curl.Protocol.Ssh.UnitLibrary`.

## Context

Third of three moves that give BL-4145's `--ssh-jump` extension an SSH client it may reference (ADR-0999). The jump extension supplies an `IConnector` (BL-4095 hook) that opens a session to the jump host and returns a direct-tcpip channel as the transfer's connection. The session channel SFTP and SCP use stays working; `direct-tcpip` open (originator address and port, target host and port), window adjust, data, EOF and close are the new part, tested against recorded bytes with no network.

## Acceptance criteria

- [x] `Curl.Ssh.UnitLibrary` holds the SSH client session and connection layer, and `Curl.Protocol.Ssh.UnitLibrary` uses it; every existing SSH unit and integration test still passes unchanged in behaviour.
- [x] A `direct-tcpip` channel can be opened through a session and read and written as an `IConnection`, honouring the peer's window and maximum packet size; unit tests against named sections of RFC 4254 (7.2, 5.1, 5.2, 5.3) with fake connections.
- [x] The changed libraries meet the four quality gates (100% line and branch coverage, complexity at most 10, CRAP at most 30) by `Measure-CodeQuality.ps1`.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- 2026-10-30 (lane 1, done): `Connection` and `SshSessionConnection` moved to Curl.Ssh (namespaces `Curl.Ssh.Connection`, `Curl.Ssh`). The shared channel logic is the abstract `SshChannel`; `SshSessionChannel` (session, subsystem, exec; bytes unchanged) and the new `SshDirectTcpipChannel` (RFC 4254 7.2 open, `IConnection`) derive from it. Channel log lines moved from `SshDiagnosticLog` to the new `SshChannelLog` (open line names the channel type, so SFTP/SCP lines are unchanged). Decisions in ADR-1166, including why no separate "client session" wrapper is added: transport, host key check and authentication are already Curl.Ssh types the jump extension composes, and the handler interleaves curl's `-v` and trace lines between them. `SshSessionChannelTests` stays in Curl.Protocol.Ssh.UnitTests/Connection (it needs `SftpServerScript`); the new `SshDirectTcpipChannelTests` (RFC 4254 7.2, 5.1, 5.2, 5.3) is in Curl.Ssh.UnitTests/Connection.
- 2026-10-30 (lane 1): `touches` widened, as no task in Doing on work/dark-factory names them: the conformance SSH server's files that imported `Curl.Protocol.Ssh.Connection`, `SshDiagnosticLogTests.cs` (channel log calls), `ScpFileDownloadTests.MaxFileSize.cs` (a `Connection.`-qualified name), the renamed global using file and Curl.Ssh's `CLAUDE.md`.
- 2026-10-30 (lane 1): Checks run: `dotnet build` clean, the changed files pass `dotnet format --verify-no-changes`, fast tests exit 0 (112 test assemblies passed), Curl.Protocol.Ssh.IntegrationTests 6/6, and `Measure-CodeQuality.ps1 -Library Curl.Ssh.UnitLibrary,Curl.Protocol.Ssh.UnitLibrary` reported 0 failing members. The tests use no paths or OS text, so they do not depend on the platform.

- 2026-10-30 (BL-4367): `touches` narrowed from the whole Curl.Protocol.Ssh.UnitTests to the files and folders this task changes there. That project now imports each moving namespace once, in `GlobalUsings/Curl.Protocol.Ssh.<Folder>.cs`, so moving a folder changes that one file (to `global using Curl.Ssh.<Folder>;`) rather than the using lines of every test that names its types; root types moved to Curl.Ssh get their own new `GlobalUsings/Curl.Ssh.cs`.

- 2026-10-29 (lane 5): Filed by BL-4145 (the --ssh-jump extension), which may not reference Curl.Protocol.Ssh.UnitLibrary (ADR-0999), so the SSH client it needs moves into a shared layer first.

## Log

- 2026-10-29: Created.
- 2026-10-30: Backlog -> Doing. Criteria at claim: 4afd8878 7507d335 e35aff77 3d836d5e.
- 2026-10-30: Doing -> Done. Curl.Ssh.UnitLibrary holds the SSH connection layer; a direct-tcpip channel opens through an authenticated transport and is read and written as an IConnection
