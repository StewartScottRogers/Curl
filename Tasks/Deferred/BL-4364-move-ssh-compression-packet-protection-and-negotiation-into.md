---
id: BL-4364
title: Move SSH compression, packet protection and negotiation into Curl.Ssh.UnitLibrary
priority: Low
assignee: Claude
pipeline: direct
depends-on: [BL-4320, BL-4375, BL-4376]
touches: [Curl.Ssh.UnitLibrary, Curl.Ssh.UnitTests, Curl.Protocol.Ssh.UnitLibrary, Curl.Protocol.Ssh.UnitTests/Compression, Curl.Protocol.Ssh.UnitTests/PacketProtection, Curl.Protocol.Ssh.UnitTests/Negotiation, Curl.Protocol.Ssh.UnitTests/GlobalUsings/Curl.Protocol.Ssh.Compression.cs, Curl.Protocol.Ssh.UnitTests/GlobalUsings/Curl.Protocol.Ssh.PacketProtection.cs, Curl.Protocol.Ssh.UnitTests/GlobalUsings/Curl.Protocol.Ssh.Negotiation.cs, Curl.Protocol.Ssh.UnitTests/Fakes, Curl.Conformance.SshServer.UnitLibrary, Curl.Conformance.SshServer.UnitTests, Curl.Console/CurlComposition.cs, Curl.Engine.UnitLibrary/EngineComposition.PlatformBuilds.cs, Curl.Engine.UnitTests/EngineCompositionPlatformLibraryTests.cs]
requirement: none
created: 2026-10-30
completed:
---
# BL-4364 — Move SSH compression, packet protection and negotiation into Curl.Ssh.UnitLibrary

## Goal

The Compression, PacketProtection and Negotiation folders of Curl.Protocol.Ssh.UnitLibrary and their tests live in Curl.Ssh.UnitLibrary / Curl.Ssh.UnitTests under namespaces Curl.Ssh.<Folder>.

## Context

Part 3 of BL-4328. Move the three folders with their tests (same folder names) from the protocol library and its unit tests. Update the using lines in Curl.Conformance.SshServer.UnitLibrary and UnitTests, Curl.Console/CurlComposition.cs (Curl.Protocol.Ssh.Negotiation becomes Curl.Ssh.Negotiation), EngineComposition.PlatformBuilds.cs and EngineCompositionPlatformLibraryTests.cs. If a folder still needs a type that has not moved (a verifier, the transport log), move that type here too and say so in Notes.

## Acceptance criteria

- [ ] Compression, PacketProtection and Negotiation and their tests exist only in `Curl.Ssh.UnitLibrary` / `Curl.Ssh.UnitTests`, under namespaces `Curl.Ssh.Compression`, `Curl.Ssh.PacketProtection` and `Curl.Ssh.Negotiation`.
- [ ] `Curl.Protocol.Ssh.UnitLibrary`, the conformance SSH server, Curl.Console and Curl.Engine build and behave as before.
- [ ] The changed libraries meet the four quality gates (100% line and branch coverage, complexity at most 10, CRAP at most 30) by `Measure-CodeQuality.ps1`.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

- 2026-10-30 (BL-4367): `touches` narrowed from the whole Curl.Protocol.Ssh.UnitTests to the files and folders this task changes there. That project now imports each moving namespace once, in `GlobalUsings/Curl.Protocol.Ssh.<Folder>.cs`, so moving a folder changes that one file (to `global using Curl.Ssh.<Folder>;`) rather than the using lines of every test that names its types; root types moved to Curl.Ssh get their own new `GlobalUsings/Curl.Ssh.cs`.

- 2026-10-30: Split from BL-4328 (over its cost cap); see its Notes for the dependency closure measured. Each moved folder's namespaces become `Curl.Ssh.<Folder>`; every moved type is `internal`, so `Curl.Ssh.UnitLibrary` needs `InternalsVisibleTo` for the consumers listed in BL-4328's Notes.

## Log

- 2026-10-30: Created.
- 2026-10-30: Backlog -> Deferred. Its work is already in BL-4362's stashed change (the whole BL-4328 move, ADR-1162); look again once BL-4362 is Done, when nothing should be left
