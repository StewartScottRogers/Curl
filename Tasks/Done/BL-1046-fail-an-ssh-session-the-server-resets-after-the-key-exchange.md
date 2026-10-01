---
id: BL-1046
title: Fail an SSH session the server resets after the key exchange with curl's exit code
priority: Normal
assignee: Claude
pipeline: protocol
depends-on: []
touches: [Curl.Protocol.Ssh.UnitLibrary, Curl.Protocol.Ssh.UnitTests, Documentation/Planning/Decisions/ADR-0220-sftp-downloads-follow-libssh2s-requests-and-map-each-status-to-curls-exit-code.md, Documentation/Planning/Decisions/ADR-0225-scp-downloads-run-scp-pf-with-exec-and-parse-its-header-as-libssh2-does.md]
requirement: none
created: 2026-09-30
completed: 2026-10-01
---
# BL-1046 — Fail an SSH session the server resets after the key exchange with curl's exit code

## Goal

An `sftp://` or `scp://` session whose server resets the TCP connection after the key exchange - at the `ssh-userauth` service request, during SFTP start-up (channel open, subsystem, `SSH_FXP_INIT`) or mid-transfer - ends with the exit code and message the reference curl gives, not with an unhandled `System.IO.IOException`.

## Context

- Follow-up of BL-991 and ADR-0283, which map a reset during the identification exchange and the key exchange only. `NetworkStream` reports a reset as a plain `IOException`, while `SshUserAuthentication.ReadServiceAnswerAsync`, `SftpSession`'s `RequireAsync` and `SshConnectionFailure.Is` catch only `EndOfStreamException` (a subclass), so a reset there still escapes.
- Care: `SshConnectionFailure.Is` wraps SCP/SFTP steps that may also write to the local output; an `IOException` from the output (curl exit 23) must not turn into an SSH connection failure. Tell the two apart (e.g. catch around the connection reads only).
- Measure first with `Record-CurlExchange.ps1`: the loopback script server cannot run a key exchange, so use `-NoServer` against a real `sshd` (WSL OpenSSH) killed or firewalled mid-session, or extend the recorder; pin what is measured. The expected analogue is the measured close for each step (`-43, Failed to get response to ssh-userauth request`; `Failure initializing sftp session: ...`; exit 79 for SFTP requests).

## Acceptance criteria

- [x] The reference curl's exit code and stderr for a reset at the service request, during SFTP start-up and during an SFTP and an SCP download are measured and copied into Notes.
- [x] `Curl.Protocol.Ssh.UnitTests` has a test per measured case where the fake connection (`Fakes/ResettingConnection`) throws `IOException` from a read, pinning the exit code and message, and one showing a local output `IOException` is not reported as a connection failure.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Protocol.Ssh.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- Measured 2026-10-01: curl 8.21.0 (libssh2 1.11.1, Schannel) against OpenSSH 10.2 in WSL (BL-569's unpacked sshd, started with `LD_LIBRARY_PATH` for its libwrap; `aes128-ctr` with `hmac-sha2-256-etm@openssh.com` so packet lengths stay readable), `-sS -k --key clientkey --pubkey clientkey.pub -u <user>: <scheme>://127.0.0.1:<port>/<path> -o out`. A throwaway C# relay (a file-based app in `%TEMP%`, not committed) sat between curl and sshd, counted curl's packets after `NEWKEYS` and reset curl's socket (linger 0) at the chosen one, or after N server bytes; a "close" mode shut it down instead. Record-CurlExchange.ps1 was not used: `-NoServer` only runs curl, and this needed a relay between curl and sshd. Results (exit, stderr):
  - service request (sftp and scp): 2, `curl: (2) Failure establishing ssh session: -43, Failed to get response to ssh-userauth request`
  - auth `none`: 79 `Error in the SSH layer`; signed `publickey`: 67 `Authentication failure` (authentication already handles it; not in scope)
  - SFTP channel open: 2 `Failure initializing sftp session: Unable to startup channel`; subsystem: `... Unable to request SFTP subsystem`; `INIT`: `... Timeout waiting for response from SFTP subsystem` (a close gives the same three)
  - SFTP `REALPATH .`, `STAT`, first `READ`: 79 `curl: (79) Error in the SSH layer`
  - SFTP `OPEN`: exit 0, empty stderr, empty output file - reset and close alike, repeated
  - SFTP download reset after 300000 / 1500000 server bytes: 79 `Error in the SSH layer`, 270000 / 1470000 bytes written
  - SCP channel open: 79 `curl: (79) Unexpected error`; `exec`: 79 `Failed waiting for channel success`; first acknowledgement: 79 `Failed reading SCP response` (a close gives the same three)
  - SCP download reset after 300000 / 1500000 bytes: 79 `Error in the SSH layer`, 294912 / 1474560 bytes written
- Decision (ADR-0289): the transport wraps a connection `IOException` in `SshConnectionLostException` (in `SshConnectionReader` and `SshPacketWriter`), which `SshConnectionFailure.Is`, the service request and SFTP start-up accept; an output `IOException` is untouched and passes on. A broken SFTP download `OPEN` succeeds with 0 bytes, as measured; SCP download start-up gets libssh2's per-step messages, replacing ADR-0225's unmeasured `Error in the SSH layer` (its close test and the `-v` line test updated to match).
- Fakes: `ResettingConnection` can also wrap another connection (its close becomes a reset; a write after it is dropped, as a socket's send buffer takes it), and `InMemorySshServer.ResetsAt` / `ResetsAtOccurrence` reset at a named step (`sftp init`, `scp header` and `scp data` for steps that record no event).
- Touches widened to ADR-0220 and ADR-0225: each had an "exit 79, not measured" line this task measured. No task in Doing on `origin/work/dark-factory` names them.
- Left unmeasured and unchanged: a reset at an SFTP upload's or listing's `OPEN`, and at an SCP upload's start.

## Log

- 2026-09-30: Created.
- 2026-10-01: Backlog -> Doing.
- 2026-10-01: Doing -> Done. An SFTP or SCP session the server resets after the key exchange now ends with the measured curl exit code and message instead of an unhandled IOException
