---
id: BL-1207
title: Measure the --trace-config ssh lines for an agent login, a listed symbolic link and -I on a directory
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-1204]
touches: [Curl.Protocol.Ssh.UnitLibrary, Curl.Protocol.Ssh.UnitTests]
requirement: none
created: 2026-10-02
completed:
---
# BL-1207 — Measure the --trace-config ssh lines for an agent login, a listed symbolic link and -I on a directory

## Goal

Under `-v --trace-config ssh` Curl writes the `[SSH]` lines curl 8.21.0 writes for a successful ssh-agent login, a symbolic link in an SFTP listing, and `-I` on an SFTP directory, measured rather than read from curl's source.

## Context

- BL-1204 (ADR-0373) wrote these three from `lib/vssh/libssh2.c` without measuring them: the agent's `SSH_AUTH_AGENT_LIST` and `SSH_AUTH_AGENT` (Windows' `ssh-agent` service was disabled; start it or run Pageant with the RSA key), no `SSH_SFTP_READDIR_LINK` for a link, and `SSH_SFTP_GETINFO -> SSH_SFTP_CLOSE` for `-I`.
- Measure as BL-1204's Notes describe (WSL `sshd` on 2222, `/mingw64/bin/curl` 8.21.0). Code: `SshUserAuthentication.TryAgentAsync`, `SftpDirectoryListing`.

## Acceptance criteria

- [ ] Each of the three paths is measured and its `[SSH]` lines recorded in Notes.
- [ ] `Curl.Protocol.Ssh.UnitTests` pins each against `InMemorySshServer`.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean and the fast tests pass.

## Notes

## Log

- 2026-10-02: Created.
