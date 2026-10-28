---
id: BL-3929
title: Port the FTP modes of Record-CurlExchange to Record-CurlExchange.sh
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Record-CurlExchange.sh]
requirement: none
created: 2026-10-27
completed: 2026-10-28
---
# BL-3929 — Port the FTP modes of Record-CurlExchange to Record-CurlExchange.sh

## Goal

`Record-CurlExchange.sh -Ftp` (with its passive, active, TLS and CCC variants) records the same
request bytes, stdout, stderr and exit code as `Record-CurlExchange.ps1 -Ftp` for the same curl
command.

## Context

Part of BL-3934. `Record-CurlExchange.sh` (commit c43bdaf2f, 2026-10-27) is a bash port of the
loopback recorder covering HTTP, HTTPS, `-NoServer` and `-UnixSocket`; every other mode prints
"not ported yet" and exits 2, listed in a block near the top of the file. Its approach: socat for
plain listeners, `openssl s_server` for TLS, byte-by-byte reads in the C locale. The first port
attempt flagged CCC (dropping TLS from the control connection mid-session) as the hard part: the
bytes curl sends right after the TLS close have to be read from the raw socket. Test in WSL Ubuntu
with curl 8.18+ (a static socat may need downloading into a temp folder; do not `apt install`),
comparing with the `.ps1` run under PowerShell on Windows against the same WSL curl, as the HTTP
pass did.

## Acceptance criteria

- [x] Every FTP-related parameter of `Record-CurlExchange.ps1` is handled by `Record-CurlExchange.sh`, and the "not ported" block no longer lists them.
- [x] At least passive, active, FTPS (explicit) and CCC cases record byte-identical `request.bin`, `stdout.bin`, `stderr.txt` and `exitcode.txt` to the `.ps1`; the cases and results are in the Log. Any case that cannot match is listed with its reason at the top of the file.
- [x] `bash -n Record-CurlExchange.sh` passes and the HTTP and HTTPS cases from the first pass still match.

## Notes

Design: control session in the socat-started handler; passive data on a second socat listener (port 0, read from its log); active data dialled by socat; TLS control = openssl s_server on a loopback port reached through /dev/tcp, with a TLS-record-aware up pump so CCC finds the first plain byte; TLS data through s_server -unix and FIFOs as for HTTPS. New files only in Record-CurlExchange.sh (the task's touches).

Measured in WSL Ubuntu (curl 8.18.0, static socat 1.8.1.1 extracted with `apt-get download` into a temp folder), .sh vs Record-CurlExchange.ps1 under Windows PowerShell against the same WSL curl (ps1 bound to the WSL gateway address; ports, hosts and PASV/EPRT numbers normalised). Byte-identical request.bin, stdout.bin, stderr.txt, exitcode.txt, transcript.txt, upload.bin for: passive RETR, passive LIST, active (EPRT), explicit FTPS (--ssl-reqd, PROT P data), implicit ftps:// (-Tls), explicit FTPS with active data (TLS data dialled), CCC passive mode (no curl close_notify), CCC --ftp-ssl-ccc-mode active (close_notify), CCC refused (534), upload plain and over TLS (-T -), REST (-C 4), FtpReply overrides (PASS 430, multi-line GREETING, CWD in turn), HTTP and HTTPS spot checks still pass.
Only difference: a data connection curl opens that no transfer uses is closed with FIN, not RST (exit 18 vs 56 for an overridden RETR with a 4xx); listed at the top of the file. Decision (no ADR: a script port detail): accepted, since socat cannot reset a single accepted socket on demand.
Design: control session in the socat-started handler; passive data on a second socat listener (port 0, read from its log); active data dialled by socat; TLS control = openssl s_server on a loopback port reached through /dev/tcp, with a TLS-record-aware up pump so CCC finds the first plain byte; TLS data through s_server -unix and FIFOs as for HTTPS. New files only in Record-CurlExchange.sh (the task's touches).

Measured in WSL Ubuntu (curl 8.18.0, static socat 1.8.1.1 extracted with `apt-get download` into a temp folder), .sh vs Record-CurlExchange.ps1 under Windows PowerShell against the same WSL curl (ps1 bound to the WSL gateway address; ports, hosts and PASV/EPRT numbers normalised). Byte-identical request.bin, stdout.bin, stderr.txt, exitcode.txt, transcript.txt, upload.bin for: passive RETR, passive LIST, active (EPRT), explicit FTPS (--ssl-reqd, PROT P data), implicit ftps:// (-Tls), explicit FTPS with active data (TLS data dialled), CCC passive mode (no curl close_notify), CCC --ftp-ssl-ccc-mode active (close_notify), CCC refused (534), upload plain and over TLS (-T -), REST (-C 4), FtpReply overrides (PASS 430, multi-line GREETING, CWD in turn), HTTP and HTTPS spot checks still pass.
Only difference: a data connection curl opens that no transfer uses is closed with FIN, not RST (exit 18 vs 56 for an overridden RETR with a 4xx); listed at the top of the file. Decision (no ADR: a script port detail): accepted, since socat cannot reset a single accepted socket on demand.
## Log

- 2026-10-27: Created.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 6954d3e9 6a11b65a 32b5f73e.
- 2026-10-28: Doing -> Done. FTP modes ported to Record-CurlExchange.sh; passive, active, FTPS, CCC cases byte-identical to the .ps1
