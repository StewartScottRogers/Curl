---
id: BL-576
title: Register the SSH handler for scp and sftp in Curl.Console
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-562, BL-569, BL-574]
touches: [Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-576 — Register the SSH handler for scp and sftp in Curl.Console

## Goal

`curl sftp://...` and `curl scp://...` run end to end through `Curl.Console` with the SSH handler and the TCP connector, the SSH options (BL-562) and `-u`, `--key`, `--pass`, `-Q`, `-T`, `-r`, `-C` mapped into the context, where today they fail as an unsupported protocol.

## Context

- Conformance audit 2026-09-28, row 35. Handler: BL-563 to BL-574; options: BL-561, BL-562.
- Register in `Curl.Console/CurlComposition.cs`; map options in `TransferContextFactory.cs`; dispatch in `Curl.Core.UnitLibrary/ProtocolDispatcher.cs` must accept `scp`/`sftp` with default port 22; add them to the `-V` protocol list as ADR-0021 requires if that list is built here.

## Acceptance criteria

- [ ] `Curl.Console.UnitTests` run an `sftp://` download and an `scp://` download through a fake connector backed by the in-memory SSH peer from `Curl.Protocol.Ssh.UnitTests` (or an equivalent test double), pinning stdout, stderr and exit code as measured by BL-569 and BL-574.
- [ ] Each SSH option reaches the context unchanged, with tests.
- [ ] `curl -V` lists `scp` and `sftp`, with a test.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Console` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
