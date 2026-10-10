---
id: BL-1917
title: Emulate SCP transfers of upstream's test sshd in the case runner
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1954]
touches: [Curl.Conformance.UnitLibrary, Curl.Conformance.UnitTests, Curl.Conformance.SshServer.UnitLibrary, Curl.Conformance.SshServer.UnitTests]
requirement: none
created: 2026-10-09
completed: 2026-10-10
---
# BL-1917 — Emulate SCP transfers of upstream's test sshd in the case runner

## Goal

The SSH stand-in serves SCP downloads and uploads (scp -f and scp -t exec requests), so upstream's SCP cases are measured.

## Context

Harness: Curl.Conformance.UnitLibrary (UpstreamCaseRunner.cs runs a case; UpstreamCaseScreening.cs decides what it skips and why; SwsHttpServerConnector.cs is the in-memory IConnector stand-in for upstream's sws HTTP server). Read Curl.Conformance.UnitLibrary\CLAUDE.md first. The library opens no socket: servers are in-memory IConnector/IDatagramConnector stand-ins, hand-written in the BCL only, platform-neutral, held to 100% line and branch coverage and complexity at most 10. Skip counts are from gap run 2026-10-08_2029. Builds on BL-1916. Implement the scp source and sink protocols (C, D and E control lines with mode, size and name, and the zero-byte acknowledgements) over an exec channel, reading file bytes from <data> parts and recording uploads for <verify><upload>, as upstream's sshd plus scp did. Reference: the upstream curl 8.21.0 release tarball (curl-8_21_0). The cases are vendored in Curl.Conformance.UnitTests\UpstreamTestData (all 2,016 of curl 8.21.0's tests/data files); use them, never a cache under %LOCALAPPDATA%\Curl\gap, which the audit guard refuses a lane. Read tests/server/*.c, tests/*.pl and tests/*.py from the tarball (https://curl.se/download/curl-8.21.0.tar.xz) into an empty scratch folder, never into the repository.

## Acceptance criteria

- [x] At least 8 named SCP cases run through UpstreamCaseRunner and get Passed or a real Curl difference.
- [x] UpstreamCaseScreening no longer returns a skip reason of the form "the harness has no value for %SCP_PWD" for those cases (a screening test in Curl.Conformance.UnitTests pins it); any case still skipped for another reason says that reason.
- [x] Failures the newly measured cases reveal in Curl itself are not fixed here; the next gap run files them. Cases that fail only because of a stand-in fault are fixed in the stand-in.
- [x] Curl.Conformance.UnitLibrary holds 100% line and branch coverage and complexity of at most 10 per method; tests run on Windows, Linux and macOS with no TestCategory=Integration.
- [x] `dotnet build Curl.Conformance.UnitLibrary -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is green.
- [x] Curl.Conformance.UnitLibrary\CLAUDE.md states what the runner now does for this.
- [ ] Interactive check, not a lane gate: `dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- "<tests/data>" <folder without blanks>/raw.json <case numbers>` reports the named cases as measured, not skipped.

## Notes

- 2026-10-10 (interactive): depends-on BL-1916 became BL-1954; BL-1916 was split into BL-1953 and BL-1954 and deferred.

- 2026-10-10 (lane 1): The sshd stand-in lives in Curl.Conformance.SshServer.UnitLibrary (ADR-0456), so the scp process went there; added it and its UnitTests to touches (no task in Doing on origin/work/dark-factory named them).
- Decision: scp reads and writes the real files the URL names under the case's log directory, as upstream's sshd plus scp does, rather than <data> parts: upstream's SCP cases write the served file as <client><file> and verify uploads from %LOGDIR/upload.%TESTNUMBER. The runner, given an SSH server, reads that file as the run's <verify><upload> bytes. Source sends C0644 always (Windows files carry no Unix mode); a leading slash before a Windows drive is dropped from the path.
- The connector runs scp on each channel of a connection in turn (curl reuses the connection for test619/621); SshServerSessionChannel.OpenAsync skips the earlier channel's leftover data/adjust/EOF/CLOSE.
- Measured: 601, 603, 605, 617, 619, 621, 623, 641, 665 and 3022 pass on Windows and are on PassingUpstreamCases.txt. Coverage: Curl.Conformance.UnitLibrary 100/100, Curl.Conformance.SshServer.UnitLibrary 100/100, worst CRAP 10. The interactive Measure-UpstreamCases.cs check is left to an interactive session (not a lane gate).

## Log

- 2026-10-09: Created.
- 2026-10-10: Backlog -> Doing.
- 2026-10-10: Doing -> Done. SSH stand-in runs scp source and sink; 10 upstream SCP cases (601-665, 3022) pass through the case runner
