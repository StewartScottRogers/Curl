---
id: BL-4363
title: Split SshDiagnosticLog into a transport log and protocol-only lines
priority: Low
assignee: Claude
pipeline: direct
depends-on: [BL-4320]
touches: [Curl.Protocol.Ssh.UnitLibrary/SshDiagnosticLog.cs, Curl.Protocol.Ssh.UnitTests/SshDiagnosticLogTests.cs, Curl.Protocol.Ssh.UnitTests/SshProtocolHandlerTests.DiagnosticLog.cs, Curl.Protocol.Ssh.UnitTests/Fakes/RecordingDiagnosticLog.cs]
requirement: none
created: 2026-10-30
completed:
---
# BL-4363 — Split SshDiagnosticLog into a transport log and protocol-only lines

## Goal

SshDiagnosticLog is split into a transport log in the protocol library's current location, free of SFTP and known_hosts types, plus protocol-only lines that write through it.

## Context

Part 2 of BL-4328. SshDiagnosticLog (265 lines) mixes transport lines with SFTP lines (SftpRequestSent, SftpAnswerRead, using Sftp.SftpPacketType) and known_hosts lines (HostKeyAccepted(SshOptions, KnownHostsFile?)). SshTransport must take only a transport log so it can later move to Curl.Ssh; do the split inside Curl.Protocol.Ssh.UnitLibrary first, with no behaviour change and byte-identical verbose output.

## Acceptance criteria

- [ ] A transport log type holds every line SshTransport and the other transport-layer folders write, and references no Sftp, KnownHosts or SshOptions type.
- [ ] The SFTP and known_hosts lines live in a separate protocol-only type that writes through the transport log; verbose output is byte-identical to before.
- [ ] Existing SSH unit tests pass unchanged in behaviour, and the split has focused unit tests.
- [ ] The changed libraries meet the four quality gates (100% line and branch coverage, complexity at most 10, CRAP at most 30) by `Measure-CodeQuality.ps1`.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

- 2026-10-30 (BL-4367): `touches` narrowed from the whole Curl.Protocol.Ssh.UnitTests to the files and folders this task changes there. That project now imports each moving namespace once, in `GlobalUsings/Curl.Protocol.Ssh.<Folder>.cs`, so moving a folder changes that one file (to `global using Curl.Ssh.<Folder>;`) rather than the using lines of every test that names its types; root types moved to Curl.Ssh get their own new `GlobalUsings/Curl.Ssh.cs`.

- 2026-10-30: Split from BL-4328 (over its cost cap); see its Notes for the dependency closure measured. Each moved folder's namespaces become `Curl.Ssh.<Folder>`; every moved type is `internal`, so `Curl.Ssh.UnitLibrary` needs `InternalsVisibleTo` for the consumers listed in BL-4328's Notes.
- Needs no new project, so it can run beside BL-4362.

## Log

- 2026-10-30: Created.
- 2026-10-30: Backlog -> Deferred. Its work is already in BL-4362's stashed change (the whole BL-4328 move, ADR-1162); look again once BL-4362 is Done, when nothing should be left
