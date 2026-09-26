---
id: BL-167
title: Author the HTTP response, output and exit-code requirements in Requirements.md
priority: Normal
assignee: Claude
pipeline: docs
depends-on: [BL-166]
touches: [Documentation/Product/Requirements.md]
requirement: none
created: 2026-09-26
completed:
---
# BL-167 — Author the HTTP response, output and exit-code requirements in Requirements.md

## Goal

`Documentation/Product/Requirements.md` has FR rows, continuing after BL-166's last row, for response framing, `-i`, `-I`, `-D`, `-f`, `--fail-with-body`, `--compressed` decoding and every HTTP exit code and message.

## Context

- Filed from the Phase 1 HTTP plan (protocol-architect, 2026-09-26), item R2. Upstream references: https://curl.se/docs/manpage.html and https://curl.se/libcurl/c/libcurl-errors.html; behaviour measured on curl 8.21.0.
- Measured failures (curl 8.21.0, loopback, 2026-09-26):
  - no status line: exit 1, `curl: (1) Received HTTP/0.9 when not allowed`
  - empty reply: exit 52, `curl: (52) Empty reply from server`
  - short body: exit 18, `curl: (18) end of response with 7 bytes missing`
  - bad chunk: exit 56, `curl: (56) chunk hex-length char not a hex digit: 0x7a`
  - header without colon: exit 8, `curl: (8) Header without colon`
  - `-L --max-redirs 0`: exit 47, `curl: (47) Maximum (0) redirects followed`
  - bad gzip: exit 61, `curl: (61) Error while processing content unencoding: incorrect header check`
  - `-C` against a server ignoring ranges: exit 33, `curl: (33) HTTP server does not seem to support byte ranges. Cannot resume.`
  - `-p -x` answered 407: exit 7, `curl: (7) CONNECT tunnel failed, response 407`
  - `-f` on 404: exit 22, `curl: (22) The requested URL returned error: 404`
- Also in scope: exit 23 (write failed), 28 (timeout), 55/56 (send/receive), 63 (`--max-filesize`), 100 (headers too large), each measured before its row is written.
- Where a criterion says *measured*, run curl 8.21.0 - the mingw build `/mingw64/bin/curl`, first on PATH, which is the Windows reference (ADR-0009 and the BL-153 ADR) - against a loopback server (the BL-165 recording script `Record-CurlExchange.ps1` once it exists), record the exact command and the bytes it produced in `Notes`, then pin them in a test. Never pin text that was not measured.

## Acceptance criteria

- [ ] Every failure listed in Context is an FR row quoting its exit code, `CurlExitCode` name and exact message line.
- [ ] Rows for `-i`, `-I`, `-D`, `-f`, `--fail-with-body` and `--compressed` state stdout/stderr contents measured on curl 8.21.0.

## Notes

- Plan item: R2 in the Phase 1 HTTP plan (2026-09-26); plan keys in this file were replaced by their task IDs.

## Log

- 2026-09-26: Created.
