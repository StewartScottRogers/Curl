---
id: BL-4363
title: Split SshDiagnosticLog into a transport log and protocol-only lines
priority: Low
assignee: Claude
pipeline: direct
depends-on: [BL-4320]
touches: [Curl.Protocol.Ssh.UnitLibrary/SshDiagnosticLog.cs, Curl.Protocol.Ssh.UnitLibrary/SshDiagnosticLog.Authentication.cs, Curl.Protocol.Ssh.UnitLibrary/SshDiagnosticLog.Channel.cs, Curl.Protocol.Ssh.UnitLibrary/SshDiagnosticLog.HostKeys.cs, Curl.Protocol.Ssh.UnitLibrary/SshDiagnosticLog.Transfer.cs, Curl.Protocol.Ssh.UnitLibrary/SshDiagnosticLog.Transport.cs, Curl.Protocol.Ssh.UnitTests/SshDiagnosticLogTests.cs, Curl.Protocol.Ssh.UnitTests/SshProtocolHandlerTests.DiagnosticLog.cs, Curl.Protocol.Ssh.UnitTests/Fakes/RecordingDiagnosticLog.cs]
requirement: none
created: 2026-10-30
completed: 2026-10-30
---
# BL-4363 — Split SshDiagnosticLog into a transport log and protocol-only lines

## Goal

SshDiagnosticLog is split into a transport log in the protocol library's current location, free of SFTP and known_hosts types, plus protocol-only lines that write through it.

## Context

Part 2 of BL-4328. SshDiagnosticLog (265 lines) mixes transport lines with SFTP lines (SftpRequestSent, SftpAnswerRead, using Sftp.SftpPacketType) and known_hosts lines (HostKeyAccepted(SshOptions, KnownHostsFile?)). SshTransport must take only a transport log so it can later move to Curl.Ssh; do the split inside Curl.Protocol.Ssh.UnitLibrary first, with no behaviour change and byte-identical verbose output.

## Acceptance criteria

- [x] A transport log type holds every line SshTransport and the other transport-layer folders write, and references no Sftp, KnownHosts or SshOptions type.
- [x] The SFTP and known_hosts lines live in a separate protocol-only type that writes through the transport log; verbose output is byte-identical to before.
- [x] Existing SSH unit tests pass unchanged in behaviour, and the split has focused unit tests.
- [x] The changed libraries meet the four quality gates (100% line and branch coverage, complexity at most 10, CRAP at most 30) by `Measure-CodeQuality.ps1`.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- 2026-10-30 (interactive): done by BL-4366 and BL-4362's parts: `Curl.Ssh.Transport.SshTransportLog` holds the transport lines and names no Sftp, KnownHosts or SshOptions type; `SshDiagnosticLog` in Curl.Protocol.Ssh holds the SFTP and known_hosts lines and writes through it; BL-4376 measured 0 failing gates; build clean and fast tests green at 2b5b4064e.

- 2026-10-30 (BL-4367): `touches` narrowed from the whole Curl.Protocol.Ssh.UnitTests to the files and folders this task changes there. That project now imports each moving namespace once, in `GlobalUsings/Curl.Protocol.Ssh.<Folder>.cs`, so moving a folder changes that one file (to `global using Curl.Ssh.<Folder>;`) rather than the using lines of every test that names its types; root types moved to Curl.Ssh get their own new `GlobalUsings/Curl.Ssh.cs`.

- 2026-10-30: Split from BL-4328 (over its cost cap); see its Notes for the dependency closure measured. Each moved folder's namespaces become `Curl.Ssh.<Folder>`; every moved type is `internal`, so `Curl.Ssh.UnitLibrary` needs `InternalsVisibleTo` for the consumers listed in BL-4328's Notes.
- Needs no new project, so it can run beside BL-4362.

## Log

- 2026-10-30: Created.
- 2026-10-30: Backlog -> Deferred. Its work is already in BL-4362's stashed change (the whole BL-4328 move, ADR-1162); look again once BL-4362 is Done, when nothing should be left
- 2026-10-30: Deferred -> Backlog. BL-4362's parts are Done; closing
- 2026-10-30: Backlog -> Doing. Interactive close Criteria at claim: d8426bc7 3e53227a aff2f4a8 e35aff77 3d836d5e.
- 2026-10-30: Doing -> Done. Work landed with BL-4362's parts; criteria checked interactively
