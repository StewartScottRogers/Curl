---
id: BL-970
title: Treat an sftp:// URL path of /~ as the home directory, as curl does
priority: Normal
assignee: Claude
pipeline: protocol
depends-on: []
touches: [Curl.Protocol.Ssh.UnitLibrary, Curl.Protocol.Ssh.UnitTests]
requirement: none
created: 2026-09-29
completed:
---
# BL-970 — Treat an sftp:// URL path of /~ as the home directory, as curl does

## Goal

`sftp://host/~` resolves to the home directory and a slash, as curl 8.21.0's `Curl_getworkingpath` resolves it, and a home directory that already ends in `/` gains no second slash before the rest of a `/~/` path.

## Context

- Measured 2026-09-29 in BL-572: `curl -D - -Q pwd sftp://localhost:2233/~` printed `257 "/home/stewart_rogers/" is current directory.` and listed the home directory (exit 0). `SftpRemotePath.Resolve` only handles `/~/`, so Curl would try to download a file named `/~`.
- curl's `Curl_getworkingpath` (`lib/vssh/vssh.c` at `curl-8_21_0`): for SFTP, `/~` or a path starting `/~/` becomes the home directory, then the rest from index 2 when the home directory does not end with `/`, or from index 3 when it does; `/~` alone becomes the home directory and `/`. Its `REJECT_ZERO` decoding also refuses a `%00` in the path.
- **BCL first.** Anything the BCL lacks is hand-built in its own library (standing rule, root `CLAUDE.md`, "Decisions", 2026-09-28).

## Acceptance criteria

- [ ] Measured first with the reference curl against OpenSSH (BL-572's setup): `/~`, `/~/`, `/~/x`, and a `%00` in the path; stdout, stderr and exit code copied into Notes.
- [ ] `SftpRemotePathTests` pin each resolution, and `SftpDirectoryListingTests` or `SshProtocolHandlerTests` pin that `/~` lists the home directory.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Protocol.Ssh.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-29: Created.
