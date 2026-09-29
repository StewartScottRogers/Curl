---
id: BL-571
title: Upload a file over SFTP with -T, --ftp-create-dirs, -C and --append
priority: High
assignee: Claude
pipeline: protocol
depends-on: [BL-569]
touches: [Curl.Protocol.Ssh.UnitLibrary, Curl.Protocol.Ssh.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-571 — Upload a file over SFTP with -T, --ftp-create-dirs, -C and --append

## Goal

`-T file sftp://host/path` uploads with `OPEN` (create, truncate, curl's permission bits from `--create-file-mode`)/`WRITE`/`CLOSE`; `--ftp-create-dirs` creates missing directories (with `--create-dirs` mode), `-C <n>`/`-C -` resume at the offset or the remote size, and `-a` appends, each as curl 8.21.0 does, with failures mapped to curl's exit codes.

## Context

- Conformance audit 2026-09-28, row 35. Builds on BL-569. The existing context members (`Upload`, `ResumeFrom`, `FtpCreateDirectories`, `CreateFileMode`) are reused; `-a`/`--append` parsing is the FTP ASCII-and-append task (row 24) and may land later: if it has not, leave append to that task and note it here.
- **BCL first.** Anything the BCL lacks is hand-built in its own library (standing rule, root `CLAUDE.md`, "Decisions", 2026-09-28): never a package, never a task blocked for a missing primitive.
- Measure with the reference curl against a local OpenSSH server through `Record-CurlExchange.ps1 -NoServer`: a new file, an existing file, a missing directory with and without `--ftp-create-dirs`, `-C -` against a shorter remote file, `-T -` from standard input, and a read-only target.

## Acceptance criteria

- [ ] Measured first as above; stderr, exit code and the resulting remote file (size and mode) copied into Notes.
- [ ] `Curl.Protocol.Ssh.UnitTests` pin the SFTP requests (flags, attributes, offsets) and outcome for each case against the in-memory peer; `%{size_upload}` and upload progress match the measured values.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Protocol.Ssh.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
