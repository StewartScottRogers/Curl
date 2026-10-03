# ADR-0372 — `--trace-config ssh` writes curl's SSH state machine lines

- **Status:** Accepted
- **Date:** 2026-10-02
- **Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions").

## Context

BL-1166, split from BL-1104 (ADR-0318), following BL-1162's to BL-1164's pattern. curl 8.21.0
(Schannel, libssh2 1.11.1, WinCNG) writes `* [SSH] ...` lines under `-v --trace-config ssh`
(and `protocol`, `all`, so `-vv`). The recorder has no SSH server, so they were measured against
OpenSSH 10.2 (`sshd` in WSL Ubuntu on port 2222, key exchange widened for WinCNG) with an RSA
key, for an SFTP and an SCP download of an 11-byte file; BL-1166's Notes hold the lines.

Most lines are state changes, `[FROM] -> [TO]`, and phase lines (`CONNECT phase done`,
`DO phase starts`, `DO phase is complete`, `SFTP DONE done`, `SCP DONE phase complete`), with
`[SSH_STOP] statemachine() -> 0, block=0` each time the state machine rests. Interleaved are
`[STATE] statemachine() -> 0, block=1` and `pollset, flags=1`, written each time libssh2 would
block on the socket: their count changed from run to run (one to twenty-five in `SSH_SFTP_CLOSE`
alone), because it depends on when the server's packets arrive.

## Decision

1. `SshStateTrace` writes the lines through `ITransferEvents.ReportInfo`; the handler's
   `TracesStateMachine` turns it on, and `CurlComposition.TracesSsh` sets it from the trace
   components. The handler writes the session's start, host-key and authentication-done
   states, `SshUserAuthentication` the `publickey` states, and `SftpFileDownload` and
   `ScpFileDownload` their transfer states.
2. The `block=1` and `pollset` lines are not written: Curl's transport does not poll, and no
   count of them is right for every run. The trace is what curl writes for a server whose
   every answer has arrived before curl asks for it.
3. Only the measured paths are traced: a `publickey` login (a denied key goes on to
   `SSH_AUTH_PASS_INIT`, as measured) and SFTP and SCP downloads. `no host key checksum given,
   checking knownhosts` is written only when neither `--hostpubsha256` nor `--hostpubmd5` is
   given. Uploads, directory listings, `-Q` commands, the password, agent and
   keyboard-interactive states and failures are left to a follow-up task that measures them.

## Consequences

- An SFTP or SCP download under `-v --trace-config ssh` matches real curl's stderr apart from
  the timing lines and the connector's port number.
- An unmeasured path writes only the state changes it shares with a measured one, so its `[SSH]` lines
  are incomplete until the follow-up lands.

## Alternatives considered

- **Write one `block=1` and one `pollset` line per network read.** Matches some runs, but real
  curl's count varies between identical runs, so no fixed rule is byte-exact; it adds lines for
  no gain.
- **Wait for a recorder SSH server.** The WSL `sshd` measured the same libssh2 build the
  reference uses; waiting would leave the component silent.
