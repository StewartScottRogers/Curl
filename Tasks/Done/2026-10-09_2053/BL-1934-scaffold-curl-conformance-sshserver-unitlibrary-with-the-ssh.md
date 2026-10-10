---
id: BL-1934
title: Scaffold Curl.Conformance.SshServer.UnitLibrary with the SSH identification exchange and plain packet framing
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.SshServer.UnitLibrary, Curl.Conformance.SshServer.UnitTests, Curl.slnx, Curl.Protocol.Ssh.UnitLibrary/Curl.Protocol.Ssh.UnitLibrary.csproj]
requirement: none
created: 2026-10-09
completed: 2026-10-09
---
# BL-1934 — Scaffold Curl.Conformance.SshServer.UnitLibrary with the SSH identification exchange and plain packet framing

## Goal

A new `Curl.Conformance.SshServer.UnitLibrary` holds an in-memory SSH server `IConnector` whose connections exchange identification strings with Curl's SSH client and frame unencrypted packets, ready for key exchange (BL-1935).

## Context

Part 1 of BL-1899 (upstream's 42 skipped SCP/SFTP cases). Read ADR-0456 first: the server lives in its own library, references `Curl.Protocol.Ssh.UnitLibrary` and `Curl.Cryptography.UnitLibrary`, and reuses the client's wire code (`Transport/SshPacketReader.cs`, `SshPacketWriter.cs`, `SshWireReader.cs`, `SshWireWriter.cs`, `SshIdentificationExchange.cs`) through `InternalsVisibleTo`. A working but ungated server-side session already exists in test code: `Curl.Protocol.Ssh.UnitTests\Fakes\InMemorySshServer.cs` and `InMemorySshServerSession.cs`; port its transport part, do not reference test code. Use the `new-project` skill for the scaffold (flat layout, alphabetical in `Curl.slnx`, `.UnitTests` directly after). BCL only, platform-neutral, no socket.

## Acceptance criteria

- [x] `Curl.Conformance.SshServer.UnitLibrary` and `Curl.Conformance.SshServer.UnitTests` exist, are listed in `Curl.slnx`, and the library references `Curl.Protocol.Ssh.UnitLibrary`, whose csproj grants it `InternalsVisibleTo`.
- [x] The server sends `SSH-2.0-OpenSSH_9.7` (or upstream sshd's identification if measured otherwise) and reads the client's identification; a unit test runs Curl's `SshIdentificationExchange` against it in memory and both sides see the other's string.
- [x] The server reads and writes plain (unencrypted) packets with the reused `SshPacketReader`/`SshPacketWriter`; a unit test sends the client's `KEXINIT` and reads it back on the server.
- [x] The library has a `CLAUDE.md` describing the transport so far and ADR-0456.
- [x] 100% line and branch coverage, complexity at most 10; `dotnet build -warnaserror` clean and `dotnet test --filter "TestCategory!=Integration"` green.

## Notes

- Identification: `SSH-2.0-OpenSSH_9.7`, the string the SSH tests' in-memory server already sends; not re-measured against upstream sshd, since libssh2 accepts any `SSH-2.0-` line and no case output prints it.
- The in-memory pipe uses `System.Threading.Channels` (BCL) rather than porting the test fake's chunk queue: fewer lines to hold at 100% coverage.
- Measured with Measure-CodeQuality.ps1 -Library Curl.Conformance.SshServer.UnitLibrary: 100% line, 100% branch, 0 failing members.

## Log

- 2026-10-09: Created.
- 2026-10-09: Backlog -> Doing.
- 2026-10-09: Doing -> Done. SSH server library scaffolded: identification exchange and plain packet framing, 100% coverage
