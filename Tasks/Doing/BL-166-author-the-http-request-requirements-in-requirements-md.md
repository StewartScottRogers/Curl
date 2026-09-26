---
id: BL-166
title: Author the HTTP request requirements in Requirements.md
priority: Normal
assignee: Claude
pipeline: docs
depends-on: [BL-165]
touches: [Documentation/Product/Requirements.md]
requirement: none
created: 2026-09-26
completed:
---
# BL-166 — Author the HTTP request requirements in Requirements.md

## Goal

`Documentation/Product/Requirements.md` has Must/Draft FR rows, numbered from FR-052, for the HTTP request line and headers, `-X`, `-H`, `-A`, `-e`, the data options and `-F`, each measured against curl 8.21.0.

## Context

- Filed from the Phase 1 HTTP plan (protocol-architect, 2026-09-26), item R1. Upstream references: https://curl.se/docs/manpage.html and https://curl.se/libcurl/c/libcurl-errors.html; behaviour measured on curl 8.21.0.
- FR-051 is the last row today. Follow the row format and the measure-first practice BL-033 used for the Phase 4 protocols.
- Measured bytes to start from: default GET `GET /a?b HTTP/1.1\r\nHost: 127.0.0.1:18081\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\n\r\n`; `-d x=1` `POST / HTTP/1.1\r\nHost: 127.0.0.1:18081\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\nContent-Length: 3\r\nContent-Type: application/x-www-form-urlencoded\r\n\r\nx=1`; `-I` `HEAD / HTTP/1.1\r\nHost: 127.0.0.1:18082\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\n\r\n`; `-u u:p -F a=b -F f=@srv.py` `POST / HTTP/1.1\r\nHost: 127.0.0.1:18082\r\nAuthorization: Basic dTpw\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\nContent-Length: 949\r\nContent-Type: multipart/form-data; boundary=------------------------H5US3YN5uWKNbfycvXmtss\r\n\r\n--------------------------H5US3YN5uWKNbfycvXmtss\r\nContent-Disposition: form-data; name="a"\r\n\r\nb\r\n--------------------------H5US3YN5uWKNbfycvXmtss\r\nContent-Disposition: form-data; name="f"; filename="srv.py"\r\nContent-Type: text/plain\r\n\r\n<the file's bytes>\r\n--------------------------H5US3YN5uWKNbfycvXmtss--\r\n`.
- `--compressed` is decided by the BL-154 ADR; `-F` boundary is `------------------------` plus 22 random alphanumerics.
- Where a criterion says *measured*, run curl 8.21.0 - the mingw build `/mingw64/bin/curl`, first on PATH, which is the Windows reference (ADR-0009 and the BL-153 ADR) - against a loopback server (the BL-165 recording script `Record-CurlExchange.ps1` once it exists), record the exact command and the bytes it produced in `Notes`, then pin them in a test. Never pin text that was not measured.

## Acceptance criteria

- [ ] Rows from FR-052 cover: request line and Host (default port dropped, IPv6 bracketed), User-Agent and Accept defaults, `-X`, `-H` (replace, remove with `X:`, empty with `X;`, `@file`), `-A`, `-e`, `-d`/`--data-*`/`--json`/`-G`/`--url-query`, `-F`/`--form-string` and `-u` placement before User-Agent.
- [ ] Every row cites the manpage section, has the version column `curl 8.21.0`, and quotes measured bytes or lines (re-measured with BL-165's script where the plan's bytes are not already quoted here).

## Notes

- Plan item: R1 in the Phase 1 HTTP plan (2026-09-26); plan keys in this file were replaced by their task IDs.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
