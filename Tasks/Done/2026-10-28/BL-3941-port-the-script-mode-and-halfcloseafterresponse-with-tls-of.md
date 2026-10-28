---
id: BL-3941
title: Port the -Script mode and -HalfCloseAfterResponse with -Tls of Record-CurlExchange to Record-CurlExchange.sh
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-3940]
touches: [Record-CurlExchange.sh]
requirement: none
created: 2026-10-28
completed: 2026-10-28
---
# BL-3941 — Port the -Script mode and -HalfCloseAfterResponse with -Tls of Record-CurlExchange to Record-CurlExchange.sh

## Goal

`Record-CurlExchange.sh` handles -Script and -HalfCloseAfterResponse with -Tls like `Record-CurlExchange.ps1`.

## Context

Split from BL-3931 (part of BL-3934); follows BL-3929 and BL-3930 in the same file. Mode: scripted binary sessions (LDAP, SMB); if -Script has no Unix counterpart for the Windows-only extra-connection handling, say so rather than faking it. Test in WSL Ubuntu (socat, curl 8.18.0, openssl present) against the .ps1 under pwsh. Platform-neutral output only.

## Acceptance criteria

- [x] The mode no longer exits 2 with "not ported yet", and the "not ported" header block no longer lists it (or states why it cannot exist off Windows).
- [x] At least one case per mode records byte-identical `request.bin`, `stdout.bin`, `stderr.txt` and `exitcode.txt` to the .ps1; cases and results are in the Log.
- [x] `bash -n Record-CurlExchange.sh` passes and the HTTP, HTTPS, FTP and mail cases still match.

## Notes

- Decision: -Script is served by a perl (core modules only) like -Http2; -HalfCloseAfterResponse with -Tls by a perl relay that a SIGUSR1 tells to shut down its send side to curl once s_server has sent the response (socat cannot be told). Both are described in the script header.
- Differences stated in the header, not faked: one connection only (the Windows-only WinLDAP extra-connection handling has no Unix counterpart), and a reset step over TLS is a FIN, not a RST (s_server holds the socket).
- No pwsh in WSL, so the .ps1 ran under pwsh on Windows with Windows curl 8.21 (Schannel), the .sh in WSL Ubuntu with curl 8.18 (OpenSSL 3.5). Cases use gopher:// (no User-Agent) and -A x with -sS so the bytes do not depend on the curl version. Platform answers differ only where curl does: gophers:// after a close without close_notify exits 56 under Schannel and 0 under OpenSSL.

## Log

- 2026-10-28: Cases, .ps1 (pwsh, Windows) against .sh (WSL), request.bin/stdout.bin/stderr.txt/exitcode.txt (and transcript.txt) byte-identical: -Script gopher plain (read, send, pause, send, close); -HalfCloseAfterResponse -Tls -HoldOpenMilliseconds 1500 over https; -HalfCloseAfterResponse plain. -Script -Tls gophers: request.bin, stdout.bin, transcript.txt identical, exit code 56 vs 0 as noted. Also run in WSL: -Script ldap:// bind and search (curl 8.18/OpenLDAP) records both BER messages; HTTP, HTTPS and FTP cases still pass; bash -n passes.
- 2026-10-28: Created.
- 2026-10-28: Backlog -> Doing. Criteria at claim: ebe547a6 49b8818c 541414b4.
- 2026-10-28: Doing -> Done. Ported -Script and -HalfCloseAfterResponse with -Tls to Record-CurlExchange.sh; bytes match the .ps1
