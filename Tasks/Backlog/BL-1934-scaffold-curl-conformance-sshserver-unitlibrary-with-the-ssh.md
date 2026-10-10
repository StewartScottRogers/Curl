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
completed:
---
# BL-1934 — Scaffold Curl.Conformance.SshServer.UnitLibrary with the SSH identification exchange and plain packet framing

## Goal

A new `Curl.Conformance.SshServer.UnitLibrary` holds an in-memory SSH server `IConnector` whose connections exchange identification strings with Curl's SSH client and frame unencrypted packets, ready for key exchange (BL-1935).

## Context

Part 1 of BL-1899 (upstream's 42 skipped SCP/SFTP cases). Read ADR-0456 first: the server lives in its own library, references `Curl.Protocol.Ssh.UnitLibrary` and `Curl.Cryptography.UnitLibrary`, and reuses the client's wire code (`Transport/SshPacketReader.cs`, `SshPacketWriter.cs`, `SshWireReader.cs`, `SshWireWriter.cs`, `SshIdentificationExchange.cs`) through `InternalsVisibleTo`. A working but ungated server-side session already exists in test code: `Curl.Protocol.Ssh.UnitTests\Fakes\InMemorySshServer.cs` and `InMemorySshServerSession.cs`; port its transport part, do not reference test code. Use the `new-project` skill for the scaffold (flat layout, alphabetical in `Curl.slnx`, `.UnitTests` directly after). BCL only, platform-neutral, no socket.

## Acceptance criteria

- [ ] `Curl.Conformance.SshServer.UnitLibrary` and `Curl.Conformance.SshServer.UnitTests` exist, are listed in `Curl.slnx`, and the library references `Curl.Protocol.Ssh.UnitLibrary`, whose csproj grants it `InternalsVisibleTo`.
- [ ] The server sends `SSH-2.0-OpenSSH_9.7` (or upstream sshd's identification if measured otherwise) and reads the client's identification; a unit test runs Curl's `SshIdentificationExchange` against it in memory and both sides see the other's string.
- [ ] The server reads and writes plain (unencrypted) packets with the reused `SshPacketReader`/`SshPacketWriter`; a unit test sends the client's `KEXINIT` and reads it back on the server.
- [ ] The library has a `CLAUDE.md` describing the transport so far and ADR-0456.
- [ ] 100% line and branch coverage, complexity at most 10; `dotnet build -warnaserror` clean and `dotnet test --filter "TestCategory!=Integration"` green.

## Notes

## Log

- 2026-10-09: Created.
