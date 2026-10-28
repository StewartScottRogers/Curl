---
id: BL-3930
title: Port the SMTP, IMAP and POP3 modes of Record-CurlExchange to Record-CurlExchange.sh
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-3929]
touches: [Record-CurlExchange.sh]
requirement: none
created: 2026-10-27
completed:
---
# BL-3930 — Port the SMTP, IMAP and POP3 modes of Record-CurlExchange to Record-CurlExchange.sh

## Goal

`Record-CurlExchange.sh -Smtp`, `-Imap` and `-Pop3` (plain and TLS) record the same request
bytes, stdout, stderr and exit code as `Record-CurlExchange.ps1` for the same curl command.

## Context

Part of BL-3934; follows BL-3929 in the same file. See BL-3929's Context for the script's approach
and how to test in WSL against the `.ps1`. These modes are scripted conversations: the server
answers each command line with a canned reply, so the bash version reads line by line rather than
byte by byte where the `.ps1` does.

## Acceptance criteria

- [ ] Every SMTP, IMAP and POP3 parameter of `Record-CurlExchange.ps1` is handled by `Record-CurlExchange.sh`, and the "not ported" block no longer lists them.
- [ ] For each of the three protocols, at least one plain and one TLS (STARTTLS where the `.ps1` supports it) case records byte-identical `request.bin`, `stdout.bin`, `stderr.txt` and `exitcode.txt` to the `.ps1`; cases and results are in the Log. Any case that cannot match is listed with its reason at the top of the file.
- [ ] `bash -n Record-CurlExchange.sh` passes and the HTTP, HTTPS and FTP cases still match.

## Notes

## Log

- 2026-10-27: Created.
- 2026-10-28: Backlog -> Doing. Criteria at claim: dffab3ef bc812ec6 b55ab035.
