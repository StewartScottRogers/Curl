---
id: BL-1046
title: Fail an SSH session the server resets after the key exchange with curl's exit code
priority: Normal
assignee: Claude
pipeline: protocol
depends-on: []
touches: [Curl.Protocol.Ssh.UnitLibrary, Curl.Protocol.Ssh.UnitTests]
requirement: none
created: 2026-09-30
completed:
---
# BL-1046 — Fail an SSH session the server resets after the key exchange with curl's exit code

## Goal

An `sftp://` or `scp://` session whose server resets the TCP connection after the key exchange - at the `ssh-userauth` service request, during SFTP start-up (channel open, subsystem, `SSH_FXP_INIT`) or mid-transfer - ends with the exit code and message the reference curl gives, not with an unhandled `System.IO.IOException`.

## Context

- Follow-up of BL-991 and ADR-0283, which map a reset during the identification exchange and the key exchange only. `NetworkStream` reports a reset as a plain `IOException`, while `SshUserAuthentication.ReadServiceAnswerAsync`, `SftpSession`'s `RequireAsync` and `SshConnectionFailure.Is` catch only `EndOfStreamException` (a subclass), so a reset there still escapes.
- Care: `SshConnectionFailure.Is` wraps SCP/SFTP steps that may also write to the local output; an `IOException` from the output (curl exit 23) must not turn into an SSH connection failure. Tell the two apart (e.g. catch around the connection reads only).
- Measure first with `Record-CurlExchange.ps1`: the loopback script server cannot run a key exchange, so use `-NoServer` against a real `sshd` (WSL OpenSSH) killed or firewalled mid-session, or extend the recorder; pin what is measured. The expected analogue is the measured close for each step (`-43, Failed to get response to ssh-userauth request`; `Failure initializing sftp session: ...`; exit 79 for SFTP requests).

## Acceptance criteria

- [ ] The reference curl's exit code and stderr for a reset at the service request, during SFTP start-up and during an SFTP and an SCP download are measured and copied into Notes.
- [ ] `Curl.Protocol.Ssh.UnitTests` has a test per measured case where the fake connection (`Fakes/ResettingConnection`) throws `IOException` from a read, pinning the exit code and message, and one showing a local output `IOException` is not reported as a connection failure.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Protocol.Ssh.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-30: Created.
- 2026-10-01: Backlog -> Doing.
