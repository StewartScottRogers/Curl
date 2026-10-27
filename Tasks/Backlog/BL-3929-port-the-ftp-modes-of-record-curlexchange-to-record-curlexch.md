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
completed:
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

- [ ] Every FTP-related parameter of `Record-CurlExchange.ps1` is handled by `Record-CurlExchange.sh`, and the "not ported" block no longer lists them.
- [ ] At least passive, active, FTPS (explicit) and CCC cases record byte-identical `request.bin`, `stdout.bin`, `stderr.txt` and `exitcode.txt` to the `.ps1`; the cases and results are in the Log. Any case that cannot match is listed with its reason at the top of the file.
- [ ] `bash -n Record-CurlExchange.sh` passes and the HTTP and HTTPS cases from the first pass still match.

## Notes

## Log

- 2026-10-27: Created.
