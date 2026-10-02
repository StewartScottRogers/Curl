---
id: BL-1166
title: Write the --trace-config ssh lines from the SSH protocol
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Ssh.UnitLibrary, Curl.Protocol.Ssh.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-10-02
completed:
---
# BL-1166 — Write the --trace-config ssh lines from the SSH protocol

## Goal

Under `-v --trace-config ssh` (and `protocol`, `all`) Curl writes the `* [SSH] ...` lines curl 8.21.0 writes for an SFTP and an SCP transfer, from its SSH library.

## Context

- Split from BL-1104 (ADR-0318). Follow BL-1102's pattern: `Curl.Console` decides whether the component is on (`ssh`, `protocol` or `all`) and hands the SSH handler an `ITransferEvents` sink.
- Not measured yet: the recorder has no SSH server. Measure first with `Record-CurlExchange.ps1 -NoServer` against a local sshd (Windows' OpenSSH Server, or sshd on Linux/macOS for the OpenSSL build), and record the lines in Notes. The reference build is libssh2 1.11.1; its `[SSH]` lines are state transitions of curl's `vssh` state machine.

## Acceptance criteria

- [ ] The `[SSH]` lines of an SFTP download and an SCP download under `-v --trace-config ssh` are measured and recorded in Notes.
- [ ] Tests pin them; `protocol` and `all` write the same; `-v` alone, another component and `ssh` without `-v` write none.
- [ ] `--ai-help` still describes `--trace-config` correctly.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean and the fast tests pass.

## Notes

## Log

- 2026-10-02: Created.
- 2026-10-02: Backlog -> Doing.
