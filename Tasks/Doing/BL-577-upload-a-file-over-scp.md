---
id: BL-577
title: Upload a file over SCP
priority: High
assignee: Claude
pipeline: protocol
depends-on: [BL-574]
touches: [Curl.Protocol.Ssh.UnitLibrary, Curl.Protocol.Ssh.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-577 — Upload a file over SCP

## Goal

`-T file scp://host/path` runs `scp -t <path>`, sends the `C<mode> <size> <name>` header with curl 8.21.0's mode (from `--create-file-mode`, default as measured), the bytes and the terminating zero byte with acknowledgements, and maps a remote error to curl's exit code and message; `-T -` behaves as curl's does for an unknown size.

## Context

- Conformance audit 2026-09-28, row 35. Builds on BL-574's exec channel.
- **BCL first.** Anything the BCL lacks is hand-built in its own library (standing rule, root `CLAUDE.md`, "Decisions", 2026-09-28): never a package, never a task blocked for a missing primitive.
- Measure with the reference curl against a local OpenSSH server through `Record-CurlExchange.ps1 -NoServer`: a new file, an existing file, a missing directory, a read-only target, `--create-file-mode 0600`, and `-T -`.

## Acceptance criteria

- [ ] Measured first as above; stderr, exit code and the resulting remote file (size and mode) copied into Notes, with the exec command curl sent.
- [ ] `Curl.Protocol.Ssh.UnitTests` pin the header, data and acknowledgements and the outcome for each case against the in-memory peer.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Protocol.Ssh.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
