---
id: BL-569
title: Download a file over SFTP and map SFTP status codes to curl's exit codes
priority: High
assignee: Claude
pipeline: protocol
depends-on: [BL-567]
touches: [Curl.Protocol.Ssh.UnitLibrary, Curl.Protocol.Ssh.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-569 — Download a file over SFTP and map SFTP status codes to curl's exit codes

## Goal

`sftp://host/path` opens a session channel (RFC 4254), starts the `sftp` subsystem, exchanges `SSH_FXP_INIT`/`VERSION` (version 3), resolves `~/` paths as curl 8.21.0 does, and downloads the file with `OPEN`/`READ`/`CLOSE`, writing its bytes and reporting progress, with each `SSH_FX_*` status (no such file, permission denied, failure) mapped to curl's exit code and message (78, 9 and so on as measured).

## Context

- Conformance audit 2026-09-28, row 35. Builds on BL-567 (authenticated transport). Channel windowing and `SSH_MSG_CHANNEL_WINDOW_ADJUST` belong here.
- **BCL only.** No new primitive needed. Anything the BCL lacks is hand-built in its own library (standing rule, root `CLAUDE.md`, "Decisions", 2026-09-28): never a package, never a task blocked for a missing primitive.
- SFTP v3: draft-ietf-secsh-filexfer-02.
- Measure with the reference curl against a local OpenSSH server through `Record-CurlExchange.ps1 -NoServer`: a file, an empty file, a missing file, a file without read permission, `sftp://h/~/file`, a path with `%20`, and `-w '%{size_download}'`.

## Acceptance criteria

- [ ] Measured first as above; stdout bytes, stderr and exit code of each copied into Notes.
- [ ] `Curl.Protocol.Ssh.UnitTests` pin the SFTP requests and the output and outcome for each case against the in-memory peer, including a file larger than one channel window.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Protocol.Ssh.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-28: Created.
