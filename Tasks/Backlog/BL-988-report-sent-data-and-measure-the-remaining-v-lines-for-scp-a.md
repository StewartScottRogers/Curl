---
id: BL-988
title: Report sent data and measure the remaining -v lines for scp and sftp uploads
priority: Normal
assignee: Claude
pipeline: protocol
depends-on: [BL-578]
touches: [Curl.Protocol.Ssh.UnitLibrary, Curl.Protocol.Ssh.UnitTests, Curl.Console.UnitTests]
requirement: none
created: 2026-09-29
completed:
---
# BL-988 — Report sent data and measure the remaining -v lines for scp and sftp uploads

## Goal

`-v` and `--trace-ascii -` on a `-T` upload over `scp://` or `sftp://` write curl 8.21.0's `} [N bytes data]` and `=> Send data` output and its closing lines, and the `-v` cases ADR-0262 could not measure match curl.

## Context

- BL-578 (ADR-0262) made `SshProtocolHandler` report curl's session lines and received data; uploads report no sent data yet.
- Unmeasured in ADR-0262: a right password and `keyboard-interactive` succeeding (an unprivileged `sshd` cannot check passwords; lines taken from `lib/vssh/libssh2.c`), a short file's `PartialFile` outcome (treated as leaving the connection intact), and a connection lost during `publickey`.
- Measure with `Record-CurlExchange.ps1 -NoServer` against BL-569's unpacked OpenSSH 10.2 in WSL; a password needs an `sshd` that can read the password database, or a scripted server.

## Acceptance criteria

- [ ] Measured first: `-v` and `--trace-ascii -` for an `sftp` and an `scp` upload, a right password, `keyboard-interactive`, and a short `scp` file; output copied into Notes.
- [ ] `Curl.Console.UnitTests` pin the measured upload `-v` stderr and trace.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Protocol.Ssh.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-29: Created.
