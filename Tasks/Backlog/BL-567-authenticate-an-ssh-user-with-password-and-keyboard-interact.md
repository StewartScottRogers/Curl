---
id: BL-567
title: Authenticate an SSH user with password and keyboard-interactive
priority: High
assignee: Claude
pipeline: protocol
depends-on: [BL-565, BL-566]
touches: [Curl.Protocol.Ssh.UnitLibrary, Curl.Protocol.Ssh.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-567 — Authenticate an SSH user with password and keyboard-interactive

## Goal

After the transport is up, the handler requests `ssh-userauth` and authenticates the `-u` user with `password` and `keyboard-interactive` (RFC 4252, RFC 4256) in the order curl 8.21.0 tries them, handles `SSH_MSG_USERAUTH_BANNER`, and maps a refusal to exit 67 (or the code curl gives) with curl's message.

## Context

- Conformance audit 2026-09-28, row 35. Needs BL-565 (encrypted transport) and BL-566 (trusted host).
- **BCL only.** No new primitive needed. If something needed cannot be built on the BCL, move the task to `Blocked` for Stewart naming what is missing; never add a package.
- Measure with the reference curl against a local OpenSSH server through `Record-CurlExchange.ps1 -NoServer`: right password, wrong password, `PasswordAuthentication no` with `KbdInteractiveAuthentication yes`, no `-u` at all, and a server with a banner; stderr and exit code.

## Acceptance criteria

- [ ] Measured first as above; stderr and exit code of each copied into Notes.
- [ ] `Curl.Protocol.Ssh.UnitTests` pin the client messages and outcome for each case against the in-memory peer, including partial success and the method order.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Protocol.Ssh.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-28: Created.
