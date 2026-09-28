---
id: BL-570
title: List an SFTP directory as curl prints it
priority: High
assignee: Claude
pipeline: protocol
depends-on: [BL-569]
touches: [Curl.Protocol.Ssh.UnitLibrary, Curl.Protocol.Ssh.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-570 — List an SFTP directory as curl prints it

## Goal

`sftp://host/dir/` (a path ending in `/`) lists the directory with `OPENDIR`/`READDIR` and writes each entry exactly as curl 8.21.0 does (the long `ls -l`-style line the server's `longname` gives, or names only with `-l`/`--list-only`).

## Context

- Conformance audit 2026-09-28, row 35. Builds on BL-569.
- **BCL first.** Anything the BCL lacks is hand-built in its own library (standing rule, root `CLAUDE.md`, "Decisions", 2026-09-28): never a package, never a task blocked for a missing primitive.
- Measure with the reference curl against a local OpenSSH server through `Record-CurlExchange.ps1 -NoServer`: a directory with files, a subdirectory and a symbolic link, with and without `-l`, an empty directory, and a missing directory.

## Acceptance criteria

- [ ] Measured first as above; stdout bytes, stderr and exit code copied into Notes.
- [ ] `Curl.Protocol.Ssh.UnitTests` pin the output bytes for each case from canned `READDIR` replies.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Protocol.Ssh.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-28: Created.
