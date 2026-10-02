# ADR-0374 — `--trace-config ssh` failures leave through the freeing state

- **Status:** Accepted
- **Date:** 2026-10-02
- **Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions").

## Context

BL-1204 extends ADR-0372's `[SSH]` lines past a `publickey` download. curl 8.21.0 (Schannel,
libssh2 1.11.1, WinCNG) was measured against OpenSSH 10.2 in WSL for SFTP and SCP uploads, SFTP
listings with and without `-l`, `-Q` commands before and after the transfer (and failing),
password and `keyboard-interactive` logins (and failing), a failed agent connection, matching and
mismatched `--hostpubsha256`/`--hostpubmd5`, and failed downloads, listings and uploads (BL-1204
Notes). Every failure ended the same way: curl's failure line, a change from the failed state to
the state that frees what it held, and `[THAT] statemachine() -> <exit code>, block=0`.

## Decision

`SshStateTrace.Fail` writes that ending after the failure line, from the state the trace is in:
an `SSH_SFTP_*` state goes to `SSH_SFTP_CLOSE`, an `SSH_SCP_*` state to `SSH_SCP_CHANNEL_FREE`,
`SSH_AUTH_KEY` (a denied `keyboard-interactive` login) returns from itself, and any other state
goes to `SSH_SESSION_FREE`. A session resting in `SSH_STOP` failed outside curl's state machine
(during the bytes) and writes nothing. A failed SFTP upload open is the one measured case where
curl enters `SSH_SFTP_CLOSE` before its failure line, so the upload enters it before rethrowing.

Paths no server here could show are written the way curl's `lib/vssh/libssh2.c` steps through
them, not measured: a successful agent login (`SSH_AUTH_AGENT_LIST`, `SSH_AUTH_AGENT`; Windows'
`ssh-agent` service is disabled on the measuring machine), a symbolic link in a listing (no
`SSH_SFTP_READDIR_LINK` is written yet), and `-I` on a directory (`SSH_SFTP_GETINFO` to
`SSH_SFTP_CLOSE`). BL-1205 measures them.

## Consequences

- Curl's own build wrote stderr identical to real curl's for all 22 measured command lines, apart from `{ [N bytes data]` lines when stdout rather than `-o` goes to the null device, a difference older than the trace
  (`block=1` and `pollset` removed, ADR-0372).
- A new failure path needs no trace code of its own unless curl changes state before its line.
