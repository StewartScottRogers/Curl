---
id: BL-528
title: Add a -NoServer mode to Record-CurlExchange.ps1 for servers the caller runs
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [Record-CurlExchange.ps1]
requirement: none
created: 2026-09-28
completed:
---
# BL-528 — Add a -NoServer mode to Record-CurlExchange.ps1 for servers the caller runs

## Goal

`Record-CurlExchange.ps1 -NoServer` runs the reference curl with the given arguments against a server the caller started (for instance a local OpenSSH `sshd` or an LDAP server) and writes `stdout.bin`, `stderr.txt` and `exitcode.txt` exactly as the other modes do, without binding a port.

## Context

- Needed to measure SSH (`scp://`, `sftp://`, audit row 35), LDAP (row 37) and SMB (row 39) behaviour: a PowerShell loopback server cannot speak SSH, and the root `CLAUDE.md` says to extend this script rather than write another recorder.
- The script's header documents each mode (`.PARAMETER Ftp`, `.PARAMETER Tls` and so on) and its `-Curl` parameter picks the curl binary; follow that style.

## Acceptance criteria

- [ ] `.PARAMETER NoServer` is documented in the script header: what it records, that `Port`, `Response`, `Connections` and the server modes are ignored, and an example.
- [ ] Running `powershell -NoProfile -File Record-CurlExchange.ps1 -NoServer -CurlArgs '-sS','http://127.0.0.1:1/' -OutDirectory <scratch>` writes the three files, with exit code `7` in `exitcode.txt`.
- [ ] `-NoServer` combined with `-Ftp` or `-Tls` is refused with a clear message.
- [ ] The existing modes are unchanged: one HTTP recording and one `-Ftp` recording made before and after the change give identical files.

## Notes

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
