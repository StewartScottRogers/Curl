---
id: BL-4439
title: Rename curl's own protocol projects to Curl.Original.Protocol.<Name>
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Protocol.Dict.UnitLibrary, Curl.Protocol.Dict.UnitTests, Curl.Protocol.File.UnitLibrary, Curl.Protocol.File.UnitTests, Curl.Protocol.Ftp.UnitLibrary, Curl.Protocol.Ftp.UnitTests, Curl.Protocol.Gopher.UnitLibrary, Curl.Protocol.Gopher.UnitTests, Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests, Curl.Protocol.Imap.UnitLibrary, Curl.Protocol.Imap.UnitTests, Curl.Protocol.Ldap.UnitLibrary, Curl.Protocol.Ldap.UnitTests, Curl.Protocol.Mqtt.UnitLibrary, Curl.Protocol.Mqtt.UnitTests, Curl.Protocol.Pop3.UnitLibrary, Curl.Protocol.Pop3.UnitTests, Curl.Protocol.Rtsp.UnitLibrary, Curl.Protocol.Rtsp.UnitTests, Curl.Protocol.Smb.UnitLibrary, Curl.Protocol.Smb.UnitTests, Curl.Protocol.Smtp.UnitLibrary, Curl.Protocol.Smtp.UnitTests, Curl.Protocol.Ssh.IntegrationTests, Curl.Protocol.Ssh.UnitLibrary, Curl.Protocol.Ssh.UnitTests, Curl.Protocol.Telnet.UnitLibrary, Curl.Protocol.Telnet.UnitTests, Curl.Protocol.Tftp.UnitLibrary, Curl.Protocol.Tftp.UnitTests, Curl.Protocol.Ws.UnitLibrary, Curl.Protocol.Ws.UnitTests, Curl.Protocol.Abstractions.UnitTests/ProtocolIsolationTests.cs, Directory.Build.props, Curl.slnx]
requirement: none
lane: no
created: 2026-10-31
completed:
---
# BL-4439 — Rename curl's own protocol projects to Curl.Original.Protocol.<Name>

## Goal

Curl's own protocol projects (the protocols upstream curl has) are named `Curl.Original.Protocol.<Name>.*` and sit under `/Original/Protocols/<Name>/` in `Curl.slnx`; `Curl.Protocol.Abstractions` keeps its name, since extensions build on it too.

## Context

Stewart, 2026-10-31: rename `Curl.Protocol.*` to `Curl.Original.Protocol.*`, keeping `Curl.Protocol.Abstractions`, with the `/Original/Protocols/` solution folder. Renamed: Dict, File, Ftp, Gopher, Http, Imap, Ldap, Mqtt, Pop3, Rtsp, Smb, Smtp, Ssh, Telnet, Tftp, Ws (33 projects). Two checks find protocols by name and must follow: `Directory.Build.props`'s extension isolation (`StartsWith('Curl.Protocol.')`) and `ProtocolIsolationTests.ProtocolLibraries` (`Curl.Protocol.*.UnitLibrary`). The guarded audit and gap files that name a protocol project (`Audit/Tools/Fuzz/Fuzz.cs`, `Invoke-MutationTest.ps1`, `Audit/Instructions`, `Gap/Instructions`) change through the `audit` and `gap` branches. Follows BL-4438 and ADR-1174.

## Acceptance criteria

- [ ] Each of the 33 projects is `Curl.Original.Protocol.<Name>.*` in folder, csproj, assembly and namespace; `Curl.Protocol.Abstractions` is unchanged.
- [ ] `Curl.slnx` holds them under `/Original/Protocols/<Name>/`.
- [ ] `Directory.Build.props` and `ProtocolIsolationTests` find protocols by the new name, and still fail an extension or protocol that references a protocol library.
- [ ] No live file outside the audit and gap paths names an old project; `Tasks/Done` and findings keep history.
- [ ] An ADR records the naming, and `CLAUDE.md`, `LaneRules.md` and the glossary state it.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-10-31: Created.
- 2026-10-31: Backlog -> Doing. Criteria at claim: be3fb4c0 963f259f 0256f0cd dabd8fca 21b67014 3d836d5e.
