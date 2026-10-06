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
completed: 2026-10-02
---
# BL-1207 — Measure the --trace-config ssh lines for an agent login, a listed symbolic link and -I on a directory

## Goal

Under `-v --trace-config ssh` Curl writes the `[SSH]` lines curl 8.21.0 writes for a successful ssh-agent login, a symbolic link in an SFTP listing, and `-I` on an SFTP directory, measured rather than read from curl's source.

## Context

- BL-1204 (ADR-0373) wrote these three from `lib/vssh/libssh2.c` without measuring them: the agent's `SSH_AUTH_AGENT_LIST` and `SSH_AUTH_AGENT` (Windows' `ssh-agent` service was disabled; start it or run Pageant with the RSA key), no `SSH_SFTP_READDIR_LINK` for a link, and `SSH_SFTP_GETINFO -> SSH_SFTP_CLOSE` for `-I`.
- Measure as BL-1204's Notes describe (WSL `sshd` on 2222, `/mingw64/bin/curl` 8.21.0). Code: `SshUserAuthentication.TryAgentAsync`, `SftpDirectoryListing`.

## Acceptance criteria

- [x] Each of the three paths is measured and its `[SSH]` lines recorded in Notes.
- [x] `Curl.Protocol.Ssh.UnitTests` pins each against `InMemorySshServer`.
- [x] `dotnet build Curl.slnx -warnaserror` is clean and the fast tests pass.

## Notes

- Measured 2026-10-02 with `/mingw64/bin/curl` 8.21.0 (Schannel, libssh2 1.11.1, WinCNG) against OpenSSH in WSL as
  BL-1204's Notes describe (`sshd -D -e -f` on 2222, user `ftuser`, a fresh RSA PEM key). Each line is `* [SSH] ` and
  the text, `block=1`/`pollset` removed (ADR-0372).
- Agent: Windows' `ssh-agent` service is disabled and `ssh-agent -d` needs admin (`cannot create agent root reg key`),
  and libssh2 ignored `SSH_AUTH_SOCK` naming another pipe, so a throwaway C# file-based agent (outside the repo) served
  `\.\pipe\openssh-ssh-agent` with the key. No key file in `HOME`, server offering publickey,password,keyboard-interactive:
  `[SSH_AUTH_HOST_INIT] -> [SSH_AUTH_AGENT_INIT]`, (`SSH: trying publickey authentication via agent`),
  `[SSH_AUTH_AGENT_INIT] -> [SSH_AUTH_AGENT_LIST]`, `[SSH_AUTH_AGENT_LIST] -> [SSH_AUTH_AGENT]`, then
  `[SSH_AUTH_AGENT_LIST] auth user 'ftuser' for key '<comment>'` - twice for an identity the server refused at the query,
  three times for the accepted one (stable over 6 runs) - then (`SSH: agent authenticated user ...`),
  `[SSH_AUTH_AGENT] -> [SSH_AUTH_DONE]`, (`SSH: authentication complete`).
- Symbolic link in a listing (long names): `[SSH_SFTP_READDIR] -> [SSH_SFTP_READDIR_LINK]`,
  `[SSH_SFTP_READDIR_LINK] -> [SSH_SFTP_READDIR_BOTTOM]`, (`{ [N bytes data]`), `[SSH_SFTP_READDIR_BOTTOM] -> [SSH_SFTP_READDIR]`.
- `-I` on a directory: `[SSH_SFTP_QUOTE_INIT] -> [SSH_SFTP_GETINFO]`, `-> [SSH_SFTP_FILETIME]` (with a `block=1`: libssh2's
  `STAT`), `-> [SSH_SFTP_TRANS_INIT]`, `-> [SSH_SFTP_READDIR_INIT]`, `-> [SSH_STOP]`, rest, `DO phase is complete`, then the SFTP
  close as a listing. Exit 0, nothing on stdout. sshd's internal-sftp logged nothing at DEBUG3, so the `STAT` is inferred from
  the `block=1` in `SSH_SFTP_FILETIME` and curl's source, not seen on the server.
- Decided (ADR-0377): the agent line is written at each identity's start and after each server answer it reads; `-I` on an
  SFTP directory now sends that `STAT` (answer ignored), so the trace and the wire agree. Two tests that pinned no request
  after `REALPATH` were renamed and now expect the `STAT`.
- `InMemorySshServer` gained `SymbolicLinks` (listed `lrwxrwxrwx`, mode 0120777, `READLINK` answers the file's bytes).
  Pinned in `SshProtocolHandlerTests.StateTraceAgentLinkAndHead.cs` (3 tests).
- Verified: Curl's built `curl.dll` and real curl wrote identical `[SSH]` lines for the listing with a link, `-I` on the
  directory, and the agent login with a refused identity first.
- `--ai-help`: no option changed; nothing to update.

## Log

- 2026-10-02: Created.
- 2026-10-02: Backlog -> Doing.
- 2026-10-02: Doing -> Done. --trace-config ssh writes curl's measured lines for an agent login, a listed symbolic link and -I on an SFTP directory
