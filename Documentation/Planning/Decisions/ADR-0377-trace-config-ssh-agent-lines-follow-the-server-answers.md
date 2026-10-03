# ADR-0377 — `--trace-config ssh` agent lines follow the server's answers

- **Status:** Accepted
- **Date:** 2026-10-02
- **Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions").

## Context

ADR-0374 wrote three `[SSH]` paths from curl's source because they could not be measured
then. BL-1207 measured them with curl 8.21.0 (Schannel, libssh2 1.11.1, WinCNG) against
OpenSSH 10.2 in WSL (BL-1207 Notes). The agent was a small test agent on
`\\.\pipe\openssh-ssh-agent`, since Windows' `ssh-agent` service is disabled and cannot be
run without administrator rights.

- An agent login writes `[SSH_AUTH_AGENT_INIT] -> [SSH_AUTH_AGENT_LIST]`,
  `[SSH_AUTH_AGENT_LIST] -> [SSH_AUTH_AGENT]`, then
  `[SSH_AUTH_AGENT_LIST] auth user '<user>' for key '<comment>'` once each time curl calls
  libssh2 for that identity. curl names `SSH_AUTH_AGENT_LIST` in this line even though it is
  in `SSH_AUTH_AGENT`. Across six runs the count was always one to start plus one per server
  answer libssh2 waited for: two for a key the server refused at the query, and three for the
  key it accepted (query, then signed request).
- A symbolic link in a listing goes `[SSH_SFTP_READDIR] -> [SSH_SFTP_READDIR_LINK]`, then
  `[SSH_SFTP_READDIR_LINK] -> [SSH_SFTP_READDIR_BOTTOM]`.
- `-I` on a directory goes `SSH_SFTP_GETINFO -> SSH_SFTP_FILETIME -> SSH_SFTP_TRANS_INIT ->
  SSH_SFTP_READDIR_INIT -> SSH_STOP`, then ends the DO phase and closes as a listing does.
  curl's tool turns on `CURLOPT_FILETIME` for `-I`. In `SSH_SFTP_FILETIME`, libssh2 sends
  `STAT` for the path and waits for the answer (that state writes a `block=1` line), and curl
  ignores the result. Nothing is written to stdout.

## Decision

- The agent's `auth user` line is written when an identity's attempt starts, and again after
  each server answer the attempt reads. This is the measured count for a server that answers
  after curl asks. It is the same timing assumption ADR-0372 makes when it leaves out
  `block=1`.
- A link's line enters `SSH_SFTP_READDIR_LINK` before its `READLINK`.
- `-I` on an SFTP directory sends `STAT` for the directory, ignores the answer, and traces the
  measured states. It still sends no `OPENDIR`.

## Consequences

- For the three command lines (a listing with a link, `-I` on a directory, and an agent whose
  first identity is refused), Curl's own build writes the same `[SSH]` lines as real curl,
  once `block=1` and `pollset` are removed.
- With `-I` on a directory, the server now sees one more request (`STAT`), as it does from real
  curl.
- A server that answers before libssh2 asks would make curl write fewer `auth user` lines than
  Curl writes. This is accepted for the reason ADR-0372 gives.

## Alternatives considered

- **One `auth user` line per identity:** simpler, but no measured run wrote it that way.
- **Keep `-I` with no `STAT`:** the trace would have to show `SSH_SFTP_FILETIME` with nothing
  sent, so the trace and the wire would disagree.
