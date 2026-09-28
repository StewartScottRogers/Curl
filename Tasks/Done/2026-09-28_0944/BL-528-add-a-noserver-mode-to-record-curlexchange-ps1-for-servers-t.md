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
completed: 2026-09-28
---
# BL-528 — Add a -NoServer mode to Record-CurlExchange.ps1 for servers the caller runs

## Goal

`Record-CurlExchange.ps1 -NoServer` runs the reference curl with the given arguments against a server the caller started (for instance a local OpenSSH `sshd` or an LDAP server) and writes `stdout.bin`, `stderr.txt` and `exitcode.txt` exactly as the other modes do, without binding a port.

## Context

- Needed to measure SSH (`scp://`, `sftp://`, audit row 35), LDAP (row 37) and SMB (row 39) behaviour: a PowerShell loopback server cannot speak SSH, and the root `CLAUDE.md` says to extend this script rather than write another recorder.
- The script's header documents each mode (`.PARAMETER Ftp`, `.PARAMETER Tls` and so on) and its `-Curl` parameter picks the curl binary; follow that style.

## Acceptance criteria

- [x] `.PARAMETER NoServer` is documented in the script header: what it records, that `Port`, `Response`, `Connections` and the server modes are ignored, and an example.
- [x] Running `powershell -NoProfile -File Record-CurlExchange.ps1 -NoServer -CurlArgs '-sS','http://127.0.0.1:1/' -OutDirectory <scratch>` writes the three files, with exit code `7` in `exitcode.txt`.
- [x] `-NoServer` combined with `-Ftp` or `-Tls` is refused with a clear message.
- [x] The existing modes are unchanged: one HTTP recording and one `-Ftp` recording made before and after the change give identical files.

## Notes

- `-NoServer` binds no listener, starts no server runspace and makes no certificate; it writes only `stdout.bin`, `stderr.txt` and `exitcode.txt` (no `request.bin`, since the script sees none of the traffic).
- `-Port` is no longer mandatory (default 0, range 0-65535); without `-NoServer` a missing port is refused with a message saying it is required. Chosen over a parameter set because the server-mode switches are many and a single check reads more plainly.
- `powershell -File` binds `-CurlArgs '-sS','http://...'` as the one string `-sS,http://...` (measured: curl exited 2, "option -sS,http://127.0.0.1:1/: is unknown"). The acceptance command uses `-File`, so under `-File` (detected by an empty `$MyInvocation.Line`, true only there) a single `CurlArgs` string is split at its commas. An argument holding a comma must be passed from a PowerShell call (`&` or `.\`), which is unaffected; the header says so.
- Verified 2026-09-28: the acceptance command wrote the three files with `7` in `exitcode.txt` (stderr `curl: (7) Failed to connect to 127.0.0.1:1 ...`). `-NoServer -Ftp` and `-NoServer -Tls` are refused. HTTP recording (`request.bin`, `stdout.bin`, `stderr.txt`, `exitcode.txt`) and `-Ftp` recording (the same plus `upload.bin`) are byte-identical before and after; FTP `transcript.txt` differs only in the EPSV data port, which the OS assigns fresh on every run (zero differences once the port is masked).

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Done. Record-CurlExchange.ps1 -NoServer records curl's stdout, stderr and exit code against a server the caller runs
