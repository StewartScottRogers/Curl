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
completed: 2026-09-26
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

- [x] Rows from FR-052 cover: request line and Host (default port dropped, IPv6 bracketed), User-Agent and Accept defaults, `-X`, `-H` (replace, remove with `X:`, empty with `X;`, `@file`), `-A`, `-e`, `-d`/`--data-*`/`--json`/`-G`/`--url-query`, `-F`/`--form-string` and `-u` placement before User-Agent.
- [x] Every row cites the manpage section, has the version column `curl 8.21.0`, and quotes measured bytes or lines (re-measured with BL-165's script where the plan's bytes are not already quoted here).

## Notes

- Plan item: R1 in the Phase 1 HTTP plan (2026-09-26); plan keys in this file were replaced by their task IDs.
- Wrote FR-052 to FR-066 under a new "HTTP requests" section of `Requirements.md`.
- Measured 2026-09-26 with `/mingw64/bin/curl` (curl 8.21.0, Schannel) through
  `Record-CurlExchange.ps1 -Port <P> -CurlArgs <args> -OutDirectory out\<case>`, one
  port per case (18101-18121, 18201-18219, 18301-18302). Each case's `curl` arguments
  and the resulting `request.bin` (CRLF written `\r\n`):
  - `--connect-to example.com:80:127.0.0.1:P http://example.com/p` -> `GET /p HTTP/1.1\r\nHost: example.com\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\n\r\n`
  - `--connect-to [::1]:P:127.0.0.1:P http://[::1]:P/` -> `GET / HTTP/1.1\r\nHost: [::1]:P\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\n\r\n`
  - `-X PUT` -> `PUT / HTTP/1.1\r\nHost: 127.0.0.1:P\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\n\r\n`
  - `-H "Accept: text/html" -H "Host: h.example"` -> `GET / HTTP/1.1\r\nHost: h.example\r\nUser-Agent: curl/8.21.0\r\nAccept: text/html\r\n\r\n`
  - `-H "X-B: 1" -H "Accept: text/html" -H "Host: h.example" -e http://r/` -> `GET / HTTP/1.1\r\nHost: h.example\r\nUser-Agent: curl/8.21.0\r\nReferer: http://r/\r\nX-B: 1\r\nAccept: text/html\r\n\r\n`
  - `-H "X-B: 1" -H "User-Agent: U" -u u:p -d z` -> `POST / HTTP/1.1\r\nHost: 127.0.0.1:P\r\nAuthorization: Basic dTpw\r\nAccept: */*\r\nX-B: 1\r\nUser-Agent: U\r\nContent-Length: 1\r\nContent-Type: application/x-www-form-urlencoded\r\n\r\nz`
  - `-H "User-Agent:" -H "Accept:"` -> `GET / HTTP/1.1\r\nHost: 127.0.0.1:P\r\n\r\n`
  - `-H "X-Empty;" -H "X-Custom: v"` -> `...Accept: */*\r\nX-Empty:\r\nX-Custom: v\r\n\r\n`
  - `-H @hdrs.txt` (file `X-From-File: one\r\nX-Two: two\n`) -> `...Accept: */*\r\nX-From-File: one\r\nX-Two: two\r\n\r\n`
  - `-H "X-A: 1" -H "X-A: 2" -H "Content-Type: text/x" -d z` -> `...Accept: */*\r\nX-A: 1\r\nX-A: 2\r\nContent-Type: text/x\r\nContent-Length: 1\r\n\r\nz`
  - `-A Agent/1` -> `...User-Agent: Agent/1\r\nAccept: */*\r\n\r\n`
  - `-e http://r.example/` -> `...Accept: */*\r\nReferer: http://r.example/\r\n\r\n`
  - `-d a=1 -d b=2` -> `POST / ...Content-Length: 7\r\nContent-Type: application/x-www-form-urlencoded\r\n\r\na=1&b=2`
  - `-d @body.txt` (file `line1\r\nline2\n`) -> `Content-Length: 10`, body `line1line2`
  - `--data-binary @body.txt` -> `Content-Length: 13`, body `line1\r\nline2\n`
  - `--data-raw @body.txt` -> `Content-Length: 9`, body `@body.txt`
  - `--data-urlencode "a b&c" --data-urlencode "n=x y"` -> `Content-Length: 13`, body `a+b%26c&n=x+y`
  - `--json {"a":1}` -> `POST / HTTP/1.1\r\nHost: 127.0.0.1:P\r\nUser-Agent: curl/8.21.0\r\nContent-Type: application/json\r\nAccept: application/json\r\nContent-Length: 7\r\n\r\n{"a":1}`
  - `-G -d a=1 -d b=2 http://127.0.0.1:P/p?q` -> `GET /p?q&a=1&b=2 HTTP/1.1\r\n...Accept: */*\r\n\r\n`
  - `--url-query "a=b c" http://127.0.0.1:P/p` -> `GET /p?a=b+c HTTP/1.1...`; `--url-query "a b=c"` -> exit 3, stderr `curl: (3) URL rejected: Malformed input to a URL function`, no connection
  - `-X PUT -d x` -> `PUT / ...Content-Length: 1\r\nContent-Type: application/x-www-form-urlencoded\r\n\r\nx`
  - `--form-string a=@b -F c=d` -> `Content-Length: 247`, parts `a`=`@b`, `c`=`d`
  - `-u u:p -F a=b -F f=@up.txt` (file `hello`) -> `...Host: 127.0.0.1:P\r\nAuthorization: Basic dTpw\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\nContent-Length: 295\r\nContent-Type: multipart/form-data; boundary=------------------------1OfAPViW6PV7nnQDJpxK9u\r\n\r\n` then the parts as FR-065 quotes them
  - `-u u:p -H "X-A: 1"` -> `GET / HTTP/1.1\r\nHost: 127.0.0.1:P\r\nAuthorization: Basic dTpw\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\nX-A: 1\r\n\r\n`
- Default GET, `-d x=1`, `-I` and `-u u:p -F a=b -F f=@srv.py` are quoted from Context
  above, not re-measured.
- Finding: a `-H` value that names `User-Agent`, `Accept`, `Referer` or `Content-Type`
  is sent in `-H` order, not in curl's own slot; only `Host` keeps its slot. This
  matches `HttpRequestHeadFormatter` (BL-172), so FR-052 to FR-059 other than `-I` and
  `-H @file` are described as implemented by it; the rest say "Not yet implemented".
- Not measured: `-A ""`. `Record-CurlExchange.ps1` rejects an empty `-CurlArgs`
  element (PowerShell's `[string[]]` binding refuses an empty string). Left out of the
  rows rather than pinned unmeasured.
- `--compressed` is left to the BL-154 ADR, as Context says.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Done. Requirements.md has measured FR-052 to FR-066 for the HTTP request head, data options, -F and -u
