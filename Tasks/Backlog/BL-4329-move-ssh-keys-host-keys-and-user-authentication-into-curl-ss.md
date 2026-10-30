---
id: BL-4329
title: Move SSH keys, host keys and user authentication into Curl.Ssh.UnitLibrary
priority: Low
assignee: Claude
pipeline: direct
depends-on: [BL-4365]
touches: [Curl.Ssh.UnitLibrary/Keys, Curl.Ssh.UnitLibrary/HostKeys, Curl.Ssh.UnitLibrary/Authentication, Curl.Ssh.UnitTests/Keys, Curl.Ssh.UnitTests/HostKeys, Curl.Ssh.UnitTests/Authentication, Curl.Protocol.Ssh.UnitLibrary, Curl.Protocol.Ssh.UnitTests]
requirement: none
created: 2026-10-29
completed:
---
# BL-4329 — Move SSH keys, host keys and user authentication into Curl.Ssh.UnitLibrary

## Goal

`Curl.Ssh.UnitLibrary` also holds SSH key formats, host key verification (known_hosts) and user authentication (RFC 4252), moved from the `Keys`, `HostKeys` and `Authentication` folders of `Curl.Protocol.Ssh.UnitLibrary`, which behaves exactly as before.

## Context

Second of three moves that give BL-4145's `--ssh-jump` extension an SSH client it may reference (ADR-0999). About 4,200 lines move with their tests (same folder names in `Curl.Protocol.Ssh.UnitTests`) into `Curl.Ssh.UnitTests`; namespaces become `Curl.Ssh.<Folder>`. Builds on BL-4328's project.

## Acceptance criteria

- [ ] `Curl.Ssh.UnitLibrary` holds the keys, host keys and authentication code and `Curl.Ssh.UnitTests` their tests; `Curl.Protocol.Ssh.UnitLibrary` no longer holds those folders.
- [ ] Every existing SSH unit and integration test still passes unchanged in behaviour.
- [ ] The changed libraries meet the four quality gates (100% line and branch coverage, complexity at most 10, CRAP at most 30) by `Measure-CodeQuality.ps1`.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

- 2026-10-29 (lane 5): Filed by BL-4145 (the --ssh-jump extension), which may not reference Curl.Protocol.Ssh.UnitLibrary (ADR-0999), so the SSH client it needs moves into a shared layer first.

## Log

- 2026-10-29: Created.
