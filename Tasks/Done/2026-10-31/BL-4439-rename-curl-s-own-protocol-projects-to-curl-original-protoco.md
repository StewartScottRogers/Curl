---
id: BL-4439
title: Rename curl's own protocol projects to Curl.Original.Protocol.<Name>
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Original.Protocol.Dict.UnitLibrary, Curl.Original.Protocol.Dict.UnitTests, Curl.Original.Protocol.File.UnitLibrary, Curl.Original.Protocol.File.UnitTests, Curl.Original.Protocol.Ftp.UnitLibrary, Curl.Original.Protocol.Ftp.UnitTests, Curl.Original.Protocol.Gopher.UnitLibrary, Curl.Original.Protocol.Gopher.UnitTests, Curl.Original.Protocol.Http.UnitLibrary, Curl.Original.Protocol.Http.UnitTests, Curl.Original.Protocol.Imap.UnitLibrary, Curl.Original.Protocol.Imap.UnitTests, Curl.Original.Protocol.Ldap.UnitLibrary, Curl.Original.Protocol.Ldap.UnitTests, Curl.Original.Protocol.Mqtt.UnitLibrary, Curl.Original.Protocol.Mqtt.UnitTests, Curl.Original.Protocol.Pop3.UnitLibrary, Curl.Original.Protocol.Pop3.UnitTests, Curl.Original.Protocol.Rtsp.UnitLibrary, Curl.Original.Protocol.Rtsp.UnitTests, Curl.Original.Protocol.Smb.UnitLibrary, Curl.Original.Protocol.Smb.UnitTests, Curl.Original.Protocol.Smtp.UnitLibrary, Curl.Original.Protocol.Smtp.UnitTests, Curl.Original.Protocol.Ssh.IntegrationTests, Curl.Original.Protocol.Ssh.UnitLibrary, Curl.Original.Protocol.Ssh.UnitTests, Curl.Original.Protocol.Telnet.UnitLibrary, Curl.Original.Protocol.Telnet.UnitTests, Curl.Original.Protocol.Tftp.UnitLibrary, Curl.Original.Protocol.Tftp.UnitTests, Curl.Original.Protocol.Ws.UnitLibrary, Curl.Original.Protocol.Ws.UnitTests, Curl.Protocol.Abstractions.UnitTests/ProtocolIsolationTests.cs, Directory.Build.props, Curl.slnx]
requirement: none
lane: no
created: 2026-10-31
completed: 2026-10-31
---
# BL-4439 — Rename curl's own protocol projects to Curl.Original.Protocol.<Name>

## Goal

Curl's own protocol projects (the protocols upstream curl has) are named `Curl.Original.Protocol.<Name>.*` and sit under `/Original/Protocols/<Name>/` in `Curl.slnx`; `Curl.Protocol.Abstractions` keeps its name, since extensions build on it too.

## Context

Stewart, 2026-10-31: rename `Curl.Protocol.*` to `Curl.Original.Protocol.*`, keeping `Curl.Protocol.Abstractions`, with the `/Original/Protocols/` solution folder. Renamed: Dict, File, Ftp, Gopher, Http, Imap, Ldap, Mqtt, Pop3, Rtsp, Smb, Smtp, Ssh, Telnet, Tftp, Ws (33 projects). Two checks find protocols by name and must follow: `Directory.Build.props`'s extension isolation (`StartsWith('Curl.Protocol.')`) and `ProtocolIsolationTests.ProtocolLibraries` (`Curl.Protocol.*.UnitLibrary`). The guarded audit and gap files that name a protocol project (`Audit/Tools/Fuzz/Fuzz.cs`, `Invoke-MutationTest.ps1`, `Audit/Instructions`, `Gap/Instructions`) change through the `audit` and `gap` branches. Follows BL-4438 and ADR-1174.

## Acceptance criteria

- [x] Each of the 33 projects is `Curl.Original.Protocol.<Name>.*` in folder, csproj, assembly and namespace; `Curl.Protocol.Abstractions` is unchanged.
- [x] `Curl.slnx` holds them under `/Original/Protocols/<Name>/`.
- [x] `Directory.Build.props` and `ProtocolIsolationTests` find protocols by the new name, and still fail an extension or protocol that references a protocol library.
- [x] No live file outside the audit and gap paths names an old project; `Tasks/Done` and findings keep history.
- [x] An ADR records the naming, and `CLAUDE.md`, `LaneRules.md` and the glossary state it.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- 2026-10-31 (interactive): 33 folders moved, about 1,560 files changed. Build clean; fast tests 114 of 114 projects pass, ProtocolIsolationTests among them (it asserts Http is found). A Har extension given a reference to Curl.Original.Protocol.Http failed the build with VerifyExtensionIsolation's ADR-0999 error. Inside the new namespace partly qualified `Abstractions.X` crefs no longer resolved, so 9 files spell out `Curl.Protocol.Abstractions`. `Curl.slnx` also gains the parent folders `/Original/`, `/Original/Protocols/` and BL-4438's missing `/Extensions/Protocol/`. The guarded audit and gap files follow on their own branches.

## Log

- 2026-10-31: Created.
- 2026-10-31: Backlog -> Doing. Criteria at claim: be3fb4c0 963f259f 0256f0cd dabd8fca 21b67014 3d836d5e.
- 2026-10-31: Doing -> Done. curl's own protocols renamed to Curl.Original.Protocol under /Original/Protocols/ (ADR-1176); build clean, 114 of 114 test projects pass, isolation checks follow the new name
