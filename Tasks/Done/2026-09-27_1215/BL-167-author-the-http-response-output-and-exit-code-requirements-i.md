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
completed: 2026-09-26
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

- [x] Every failure listed in Context is an FR row quoting its exit code, `CurlExitCode` name and exact message line.
- [x] Rows for `-i`, `-I`, `-D`, `-f`, `--fail-with-body` and `--compressed` state stdout/stderr contents measured on curl 8.21.0.

## Notes

- Plan item: R2 in the Phase 1 HTTP plan (2026-09-26); plan keys in this file were replaced by their task IDs.
- Rows written: FR-067 to FR-085 in `Documentation/Product/Requirements.md`, new section "HTTP responses, output and exit codes", after BL-166's FR-066.
- Measured 2026-09-26 with `C:/Program Files/Git/mingw64/bin/curl.exe` (curl 8.21.0, Schannel), driven by a throwaway Python loopback listener on `127.0.0.1` (ports 19101-19231) that read the request head, sent the canned response and closed. Every command ran with `-sS` unless noted. Canned response -> command -> exit, stdout, stderr:
  - `hello\r\n` -> `curl -sS http://127.0.0.1:<P>/` -> 1, empty, `curl: (1) Received HTTP/0.9 when not allowed\r\n`
  - nothing, then close -> same -> 52, empty, `curl: (52) Empty reply from server\r\n`
  - `HTTP/1.1 200 OK\r\nContent-Length: 12\r\n\r\nhello` -> same -> 18, `hello`, `curl: (18) end of response with 7 bytes missing\r\n`
  - `HTTP/1.1 200 OK\r\nTransfer-Encoding: chunked\r\n\r\n5\r\nhel` -> same -> 18, `hel`, `curl: (18) transfer closed with outstanding read data remaining\r\n`
  - `HTTP/1.1 200 OK\r\nTransfer-Encoding: chunked\r\n\r\nzz\r\nhello\r\n0\r\n\r\n` -> same -> 56, empty, `curl: (56) chunk hex-length char not a hex digit: 0x7a\r\n`
  - `HTTP/1.1 200 OK\r\nBadHeader\r\nContent-Length: 0\r\n\r\n` -> same -> 8, empty, `curl: (8) Header without colon\r\n`
  - `HTTP/1.1 200 OK\r\nX-Big: <400000 x a>\r\nContent-Length: 0\r\n\r\n` -> same -> 100, empty, `curl: (100) A value or data field grew larger than allowed\r\n`
  - `HTTP/1.1 200 OK\r\nContent-Length: 100\r\n\r\nhel` then a reset (SO_LINGER 0) -> same -> 56, `hel`, `curl: (56) Recv failure: Connection was reset\r\n`; a reset before any reply gives the same line with empty stdout
  - `HTTP/1.1 302 Found\r\nLocation: /b\r\nContent-Length: 0\r\n\r\n` -> `-sS -L --max-redirs 0` -> 47, empty, `curl: (47) Maximum (0) redirects followed\r\n`
  - `HTTP/1.1 200 OK\r\nContent-Encoding: gzip\r\nContent-Length: 5\r\n\r\nhello` -> `-sS --compressed` -> 61, empty, `curl: (61) Error while processing content unencoding: incorrect header check\r\n`
  - a real gzip of `hello gzip\n` -> `-sS --compressed` -> 0, `hello gzip\n`; without `--compressed` -> 0, the raw gzip bytes. The `--compressed` request head was `GET / HTTP/1.1\r\nHost: 127.0.0.1:19231\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\nAccept-Encoding: deflate, gzip, br, zstd\r\n\r\n`
  - `HTTP/1.1 200 OK\r\nContent-Length: 5\r\n\r\nhello`, with a 3-byte `part.txt` -> `-sS -C - -o part.txt` -> 33, empty, `curl: (33) HTTP server does not seem to support byte ranges. Cannot resume.\r\n`
  - `HTTP/1.1 407 Proxy Authentication Required\r\nContent-Length: 0\r\n\r\n` -> `-sS -p -x http://127.0.0.1:<P>/ http://example.com/` -> 7, empty, `curl: (7) CONNECT tunnel failed, response 407\r\n`
  - `HTTP/1.1 404 Not Found\r\nContent-Length: 4\r\n\r\nnope` -> `-sS -f` -> 22, empty, `curl: (22) The requested URL returned error: 404\r\n`; `-s -f` -> 22, empty, empty; `-f` on a 500 -> 22, the progress meter then `\r\ncurl: (22) The requested URL returned error: 500\r\n`
  - the same 404 -> `-sS --fail-with-body` -> 22, `nope`, `curl: (22) The requested URL returned error: 404\r\n`
  - `HTTP/1.1 200 OK\r\nContent-Length: 5\r\nX-A: 1\r\n\r\nhello` -> `-sS -i` and `-sS -D -` -> 0, `HTTP/1.1 200 OK\r\nContent-Length: 5\r\nX-A: 1\r\n\r\nhello`, empty; `-sS -D hdr.txt` -> 0, `hello`, empty, and `hdr.txt` held the head
  - `HTTP/1.1 200 OK\r\nContent-Length: 5\r\nX-A: 1\r\n\r\n` -> `-sS -I` -> 0, those bytes, empty
  - that 200 with body -> `-sS -o nodir/x/out.txt` -> 23, empty, `curl: (23) client returned ERROR on write of 5 bytes\r\n`; without `-sS` the progress meter, `Warning: Failed to open the file nodir/x/out.txt: No such file or directory\r\n`, then `\r\ncurl: (23) …`
  - that 200 sent after 3 s -> `-sS -m 1` -> 28, empty, `curl: (28) Operation timed out after 1006 milliseconds with 0 bytes received\r\n`
  - that 200 -> `-sS --max-filesize 2` -> 63, empty, `curl: (63) Maximum file size exceeded\r\n`; `HTTP/1.1 200 OK\r\nConnection: close\r\n\r\nhello` -> 63, `he`, `curl: (63) Exceeded the maximum allowed file size (2) with 2 bytes\r\n`
- Exit 55 could not be provoked on Windows loopback: resetting the connection (after 0.5 s or 1.5 s, with a 1 KB receive buffer and no read) during a 50 MB `--data-binary @big.bin -H "Expect:"` upload gave exit 56 `Recv failure: Connection was reset` each time. Per the rule never to pin text that was not measured, FR-085 names the code and leaves its message unpinned. Default taken: a `Could` row rather than an invented message.
- Messages vary with the case: `<status>` in exits 22 and 7, `<ms>` and `<n>` in 28, the bad byte in 56, `<limit>` and `<n>` in 63. Rows state the pattern and the measured instance.
- `Record-CurlExchange.ps1` exists, but a throwaway Python listener was used because this task pins no fixture. Tasks that implement these rows should re-record with the script before pinning bytes in tests.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Done. Requirements.md FR-067 to FR-085 pin measured curl 8.21.0 response framing, -i/-I/-D/-f/--fail-with-body/--compressed output and every HTTP exit code and message
