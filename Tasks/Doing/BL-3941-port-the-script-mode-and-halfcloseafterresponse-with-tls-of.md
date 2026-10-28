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
completed:
---
# BL-3941 — Port the -Script mode and -HalfCloseAfterResponse with -Tls of Record-CurlExchange to Record-CurlExchange.sh

## Goal

`Record-CurlExchange.sh` handles -Script and -HalfCloseAfterResponse with -Tls like `Record-CurlExchange.ps1`.

## Context

Split from BL-3931 (part of BL-3934); follows BL-3929 and BL-3930 in the same file. Mode: scripted binary sessions (LDAP, SMB); if -Script has no Unix counterpart for the Windows-only extra-connection handling, say so rather than faking it. Test in WSL Ubuntu (socat, curl 8.18.0, openssl present) against the .ps1 under pwsh. Platform-neutral output only.

## Acceptance criteria

- [ ] The mode no longer exits 2 with "not ported yet", and the "not ported" header block no longer lists it (or states why it cannot exist off Windows).
- [ ] At least one case per mode records byte-identical `request.bin`, `stdout.bin`, `stderr.txt` and `exitcode.txt` to the .ps1; cases and results are in the Log.
- [ ] `bash -n Record-CurlExchange.sh` passes and the HTTP, HTTPS, FTP and mail cases still match.

## Notes

## Log

- 2026-10-28: Created.
- 2026-10-28: Backlog -> Doing. Criteria at claim: ebe547a6 49b8818c 541414b4.
