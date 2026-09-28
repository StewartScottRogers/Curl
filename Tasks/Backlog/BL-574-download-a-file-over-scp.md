---
id: BL-574
title: Download a file over SCP
priority: High
assignee: Claude
pipeline: protocol
depends-on: [BL-567]
touches: [Curl.Protocol.Ssh.UnitLibrary, Curl.Protocol.Ssh.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-574 — Download a file over SCP

## Goal

`scp://host/path` opens a session channel, runs `scp -f <path>` with `exec` as curl 8.21.0's SSH library does, reads the `C<mode> <size> <name>` header and the file bytes with the protocol's acknowledgements, writes the file, and maps a remote error line to curl's exit code and message.

## Context

- Conformance audit 2026-09-28, row 35. Builds on BL-567 (authenticated transport); the channel code from BL-569 is reused if it has landed, otherwise this task introduces the session channel and BL-569 reuses it (both touch only the SSH library, so they never run together).
- **BCL first.** Anything the BCL lacks is hand-built in its own library (standing rule, root `CLAUDE.md`, "Decisions", 2026-09-28): never a package, never a task blocked for a missing primitive.
- The SCP wire protocol is not an RFC; take it from the measurement (the command line curl's library sends appears in `sshd -ddd`'s log) and OpenSSH's `scp` behaviour.
- Measure with the reference curl against a local OpenSSH server through `Record-CurlExchange.ps1 -NoServer`: a file, an empty file, a missing file, a directory, `scp://h/~/file`, and `-w '%{size_download}'`.

## Acceptance criteria

- [ ] Measured first as above; stdout bytes, stderr and exit code copied into Notes, with the exec command curl sent.
- [ ] `Curl.Protocol.Ssh.UnitTests` pin the exec request, the acknowledgement bytes, and output and outcome for each case against the in-memory peer.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Protocol.Ssh.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-28: Created.
