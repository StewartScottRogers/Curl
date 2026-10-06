---
id: BL-168
title: Author the -L, proxy, auth, cookie and -w requirements and record HTTP in the Roadmap
priority: Normal
assignee: Claude
pipeline: docs
depends-on: [BL-167, BL-151]
touches: [Documentation/Product/Requirements.md, Documentation/Planning/Roadmap.md]
requirement: none
created: 2026-09-26
completed: 2026-09-26
---
# BL-168 — Author the -L, proxy, auth, cookie and -w requirements and record HTTP in the Roadmap

## Goal

`Documentation/Product/Requirements.md` has FR rows for `-L`, proxies, authentication, cookies and `-w`, and `Documentation/Planning/Roadmap.md` Milestone 1 lists the HTTP work with the exit criterion `curl http(s)://...`.

## Context

- Filed from the Phase 1 HTTP plan (protocol-architect, 2026-09-26), item R3. Upstream references: https://curl.se/docs/manpage.html and https://curl.se/libcurl/c/libcurl-errors.html; behaviour measured on curl 8.21.0.
- Continue numbering after BL-167. Authentication and Cookies are in Milestone 1 per the BL-151 ADR.
- Measured: `-L --max-redirs 0` exit 47 `curl: (47) Maximum (0) redirects followed`; `-p -x` answered 407 exit 7 `curl: (7) CONNECT tunnel failed, response 407`; `-u u:p` sends `Authorization: Basic dTpw` before `User-Agent`.
- `Documentation/Planning/Roadmap.md` "Milestone 1" says today "HTTP and the other Phase 1 option groups are part of Phase 1 but have no tasks yet".
- Where a criterion says *measured*, run curl 8.21.0 - the mingw build `/mingw64/bin/curl`, first on PATH, which is the Windows reference (ADR-0009 and the BL-153 ADR) - against a loopback server (the BL-165 recording script `Record-CurlExchange.ps1` once it exists), record the exact command and the bytes it produced in `Notes`, then pin them in a test. Never pin text that was not measured.

## Acceptance criteria

- [x] FR rows cover redirect limits, method rewriting and credential rules; proxy selection and environment variables; Basic, Bearer, Digest and `--anyauth`; `-b`, `-c`, `-j`; `-w` variables.
- [x] `Documentation/Planning/Roadmap.md` "Milestone 1": "Delivers" lists the HTTP handler, command-line groups, Core, Networking, Authentication, Cookies, Output and Console items by task ID; "Exit criteria" adds `curl http(s)://...` end to end with curl's request bytes, output and exit codes; the "no tasks yet" sentence is removed.

## Notes

- Plan item: R3 in the Phase 1 HTTP plan (2026-09-26); plan keys in this file were replaced by their task IDs.
- 2026-09-26 (align-and-document): added FR-086 to FR-106 to `Documentation/Product/Requirements.md`
  in five new sections: HTTP redirects FR-086 (`-L` follows / no `-L` writes the 3xx
  body), FR-087 (`--max-redirs`, default 50, exit 47), FR-088 (method rewriting on
  301/302/303 vs 307/308, `--post30x`, `-X` kept), FR-089 (credentials not sent to another
  host or port unless `--location-trusted`); HTTP proxies FR-090 (`-x` forward request,
  `-U`), FR-091 (`-p` and https `CONNECT`), FR-092 (environment variables, `-x` wins,
  `HTTP_PROXY`), FR-093 (`--noproxy`/`NO_PROXY`); HTTP authentication FR-094 (Basic,
  URL user info, `-H Authorization` replaces), FR-095 (Bearer), FR-096 (Digest), FR-097
  (`--anyauth`); HTTP cookies FR-098 (`-b` string, cookie engine), FR-099 (`-b` file),
  FR-100 (`-c` jar), FR-101 (`-j`); Write-out FR-102 (variables), FR-103 (escapes,
  `%header{}`, unknown variables, `%{stderr}`/`%{stdout}`/`%{onerror}`), FR-104 (time and
  speed format), FR-105 (`%{json}`, `%{header_json}`), FR-106 (`@file`, `@-`). FR-053's
  header order now names `Proxy-Authorization`, `Proxy-Connection` and `Cookie`; the
  TODO note at the top lists the groups authored. Existing rows are cross-referenced,
  not restated: FR-066 (Basic bytes), FR-079 (`--max-redirs 0`), FR-081 (407), FR-071
  (exit 22), FR-017/FR-018 (`-w` values for `file://`).
- Roadmap Milestone 1 "Delivers" now lists the HTTP plan by task ID (ADRs BL-151 to
  BL-158, BL-163, BL-164; contracts BL-159 to BL-162; BL-165; requirements BL-166 to
  BL-168; handler BL-169 to BL-186; Cli BL-187 to BL-202; Core BL-203 to BL-210;
  Networking BL-211 to BL-215; Output BL-224 to BL-229; Console BL-230 to BL-245;
  Authentication and Cookies BL-216 to BL-223 were already listed). Exit criteria add
  `curl http(s)://...`. The "no tasks yet" sentence is gone, and "Outstanding" no longer
  names BL-063 to BL-072 (all in `Done`) or says HTTP is unplanned; it points to the board.
- Measurement method: curl 8.21.0 (x86_64-w64-mingw32, Schannel), `/mingw64/bin/curl`,
  on 2026-09-26, against a throwaway Python loopback recorder on `127.0.0.1` (random port,
  written `<P>`) that answers each connection with the next canned response in a list
  (the last one repeated), records the request, and closes. `Record-CurlExchange.ps1`
  was not used because it sends the same response on every connection, and the redirect,
  Digest and `--anyauth` cases need a different second response. Unless given, the
  canned response is `HTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\nok`. Bytes are Python
  reprs of what was captured.
- Redirects: `-sS -L --max-redirs 0 http://127.0.0.1:<P>/a` on `302 Location: /b` ->
  stderr `curl: (47) Maximum (0) redirects followed\r\n`, exit 47. `-sS -L` on an endless
  302 -> `curl: (47) Maximum (50) redirects followed\r\n`, exit 47. `-sS -L --max-redirs 2`
  -> requests `GET /a`, `GET /b`, `GET /b`, then `curl: (47) Maximum (2) redirects
  followed\r\n`. `-sS` on `302 ... Content-Length: 4\r\n\r\nmove` -> stdout `move`, exit 0.
  `-sS -L` on that 302 then `200 ... ok` -> second request
  `GET /b HTTP/1.1\r\nHost: 127.0.0.1:<P>\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\n\r\n`,
  stdout `ok`. `-sS -L -d x=1` on 301/302/303 -> second request that same `GET /b`; on
  307/308 -> `POST /b HTTP/1.1\r\nHost: 127.0.0.1:<P>\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\nContent-Length: 3\r\nContent-Type: application/x-www-form-urlencoded\r\n\r\nx=1`;
  `--post301`/`--post302`/`--post303` with their status -> that same POST. `-sS -L -X POST
  -d x=1` on 302 -> `POST /b HTTP/1.1\r\nHost: 127.0.0.1:<P>\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\n\r\n`.
- Redirect credentials, `302 Location: http://localhost:<P>/b`: `-sS -L -u u:p -H "Cookie:
  c=1" -H "Authorization: Bearer h"` -> first
  `GET /a HTTP/1.1\r\nHost: 127.0.0.1:<P>\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\nCookie: c=1\r\nAuthorization: Bearer h\r\n\r\n`,
  second `GET /b HTTP/1.1\r\nHost: localhost:<P>\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\n\r\n`.
  `--location-trusted -u u:p -H "Cookie: c=1"` -> second
  `GET /b HTTP/1.1\r\nHost: localhost:<P>\r\nAuthorization: Basic dTpw\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\nCookie: c=1\r\n\r\n`.
  `-L --oauth2-bearer tok` -> first has `Authorization: Bearer tok` after `Host`, second
  none. `-L -u u:p` with `Location: /b` (same host) -> both carry `Authorization: Basic
  dTpw`. Two listeners, `Location: http://127.0.0.1:<PB>/b` from `<PA>` with `-L -u
  u:p` -> second request has no `Authorization`.
- Proxies: `-sS -x http://127.0.0.1:<P> http://example.com/p?q` ->
  `GET http://example.com/p?q HTTP/1.1\r\nHost: example.com\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\nProxy-Connection: Keep-Alive\r\n\r\n`.
  `-x 127.0.0.1:<P> -u u:p` -> same with `Authorization: Basic dTpw` after `Host`.
  `-x ... -U pu:pp` -> `Proxy-Authorization: Basic cHU6cHA=` after `Host`. `-x ... -U
  pu:pp -u u:p -e http://r/ -b a=1 -H "X-A: 1" -d z http://example.com/p` ->
  `POST http://example.com/p HTTP/1.1\r\nHost: example.com\r\nProxy-Authorization: Basic cHU6cHA=\r\nAuthorization: Basic dTpw\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\nReferer: http://r/\r\nProxy-Connection: Keep-Alive\r\nCookie: a=1\r\nX-A: 1\r\nContent-Length: 1\r\nContent-Type: application/x-www-form-urlencoded\r\n\r\nz`.
  `-sS -p -x http://127.0.0.1:<P> http://example.com/p` ->
  `CONNECT example.com:80 HTTP/1.1\r\nHost: example.com:80\r\nUser-Agent: curl/8.21.0\r\nProxy-Connection: Keep-Alive\r\n\r\n`
  (answered `407` -> `curl: (7) CONNECT tunnel failed, response 407\r\n`, exit 7).
  `-x http://127.0.0.1:1 --noproxy 127.0.0.1 http://127.0.0.1:<P>/d` -> direct
  `GET /d HTTP/1.1\r\nHost: 127.0.0.1:<P>\r\n...`, exit 0.
- Proxy environment (all other proxy variables unset with `env -u`): `http_proxy=`,
  `ALL_PROXY=` and upper-case `HTTP_PROXY=http://127.0.0.1:<P>` with `http://example.com/e`
  -> each sent `GET http://example.com/e HTTP/1.1\r\nHost: example.com\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\nProxy-Connection: Keep-Alive\r\n\r\n`
  to `<P>`, exit 0 (`HTTP_PROXY` ran with `--resolve example.com:80:127.0.0.1` so a direct
  connection could not reach the listener). `HTTPS_PROXY=http://127.0.0.1:<P>` with
  `https://example.com/e` -> `CONNECT example.com:443 HTTP/1.1\r\nHost: example.com:443\r\nUser-Agent: curl/8.21.0\r\nProxy-Connection: Keep-Alive\r\n\r\n`.
  `ALL_PROXY=http://127.0.0.1:1 NO_PROXY=127.0.0.1` with `http://127.0.0.1:<P>/n` -> direct.
  `http_proxy=http://127.0.0.1:1` with `-x http://127.0.0.1:<P>` -> sent to `<P>`.
- Authentication: `--digest -u u:p .../d`, responses `401` with `WWW-Authenticate: Digest
  realm="r", nonce="n"` then `200 ok` -> first request without `Authorization`, second
  `GET /d HTTP/1.1\r\nHost: 127.0.0.1:<P>\r\nAuthorization: Digest username="u",realm="r",nonce="n",uri="/d",response="3ce808f964d94d8ce8131d5025746bc6"\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\n\r\n`;
  the response equals MD5(MD5("u:r:p") ":n:" MD5("GET:/d")), checked in Python. With the
  `401` (body `nope`) repeated -> stdout `nope`, exit 0. `--anyauth -u u:p` with `Basic
  realm="r"` -> second request `Authorization: Basic dTpw`; with the Digest challenge ->
  `Authorization: Digest username="u",realm="r",nonce="n",uri="/y",response="b33bcda6f1ee802381f7dd490425077c"`.
  `http://u:p@127.0.0.1:<P>/url` -> `Authorization: Basic dTpw` after `Host`.
- Cookies: `-b "a=1; b=2" -e http://r/ -H "X-A: 1"` ->
  `GET /c HTTP/1.1\r\nHost: 127.0.0.1:<P>\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\nReferer: http://r/\r\nCookie: a=1; b=2\r\nX-A: 1\r\n\r\n`.
  `-u u:p -b a=1` -> `Authorization` after `Host`, `Cookie: a=1` after `Accept`. File
  `in.txt` = `# Netscape HTTP Cookie File\n127.0.0.1\tFALSE\t/\tFALSE\t0\tsess\t1\n127.0.0.1\tFALSE\t/\tFALSE\t4102444800\tkeep\t2\n`:
  `-b in.txt` -> `Cookie: keep=2; sess=1`; `-j -b in.txt` -> `Cookie: keep=2`; `-j -b
  in.txt -c jar2.txt` -> jar `# Netscape HTTP Cookie File\r\n# https://curl.se/docs/http-cookies.html\r\n# This file was generated by libcurl! Edit at your own risk.\r\n\r\n127.0.0.1\tFALSE\t/\tFALSE\t4102444800\tkeep\t2\r\n`.
  `-c jar.txt` on `Set-Cookie: s=v` and `Set-Cookie: p=w; Expires=Fri, 01 Jan 2100
  00:00:00 GMT; Path=/` -> the same three header lines and blank line, then
  `127.0.0.1\tFALSE\t/\tFALSE\t1825025340\tp\tw\r\n127.0.0.1\tFALSE\t/\tFALSE\t0\ts\tv\r\n`
  (run at 2026-09-26T23:28:53Z; 1825025340 is 2027-10-31T23:29:00Z). `-c jar3.txt` with
  no cookies -> the header lines and blank line only. `-b nosuchfile.txt` -> no `Cookie`,
  stderr empty, exit 0. `302` with `Set-Cookie: s=v`: `-L -b ""` -> second request has
  `Cookie: s=v`; `-L` alone -> no `Cookie`.
- Write-out, response `HTTP/1.1 200 OK\r\nContent-Type: text/plain; charset=utf-8\r\nX-A: 1\r\nContent-Length: 5\r\n\r\nhello`,
  `-sS -o NUL`: `-w '%{http_code}|%{response_code}|%{size_download}|%{url_effective}|%{num_redirects}|%{exitcode}|%{content_type}|%header{x-a}|%header{missing}|%{bogus}|%%|\n|\t|\\|'`
  -> stdout `200|200|5|http://127.0.0.1:<P>/w|0|0|text/plain; charset=utf-8|1|||%|\r\n|\t|\\|`,
  stderr `curl: unknown --write-out variable: 'bogus'\r\n`, exit 0. `-s -w '%{bogus}x'` ->
  stdout `x`, same stderr line. `-w '%{time_total} %{time_connect} %{speed_download}'` ->
  `0.002193 0.001739 2286`. `-w '%{http_code'` -> `%{http_code`. `-w '[%{size_download}]'`
  without `-o` -> `hello[5]`. `-w '%{onerror}err\n'` on success -> nothing.
  `-w '%{stderr}to-err\n%{stdout}to-out\n'` -> stderr `to-err\r\n`, stdout `to-out\r\n`.
  `-L -f -w '%{http_code} %{num_redirects} %{url_effective} %{exitcode} %{errormsg}\n'` on
  302 then `404` -> stdout `404 1 http://127.0.0.1:<P>/b 22 The requested URL returned error: 404\r\n`,
  stderr `curl: (22) The requested URL returned error: 404\r\n`, exit 22. `-w @wf4.txt`,
  file bytes `a=%{http_code}\nb\r\nc` (literal backslash-n, real CRLF) -> `a=200\r\nbc`;
  a file with real LFs `a=%{http_code}` LF `b` LF -> `a=200b`; `-w @-` with stdin
  `%{http_code}\n` -> `200\r\n`. `-w @nosuch.txt` -> stderr `curl: Failed to open nosuch.txt\r\ncurl: option -w: error encountered when reading a file\r\ncurl: try 'curl --help' or 'curl --manual' for more information\r\n`, exit 26.
  `-w '%{header_json}'` -> `{"content-type":["text/plain; charset=utf-8"],\r\n"x-a":["1"],\r\n"content-length":["5"]\r\n}`.
  `-w '%{json}'` -> `{"certs":"","conn_id":0,"content_type":"text/plain; charset=utf-8","errormsg":null,"exitcode":0,"filename_effective":"NUL","ftp_entry_path":null,"http_code":200,"http_connect":0,"http_version":"1.1","local_ip":"127.0.0.1","local_port":51462,"method":"GET","num_certs":0,"num_connects":1,"num_headers":3,"num_redirects":0,"num_retries":0,"proxy_ssl_verify_result":0,"proxy_used":0,"redirect_url":null,"referer":null,"remote_ip":"127.0.0.1","remote_port":<P>,"response_code":200,"scheme":"http","size_delivered":5,"size_download":5,"size_header":87,"size_request":80,"size_upload":0,"speed_download":3891,"speed_upload":0,"ssl_verify_result":0,"time_appconnect":0.000000,"time_connect":0.000924,"time_namelookup":0.000065,"time_posttransfer":0.001018,"time_pretransfer":0.001018,"time_queue":0.000045,"time_redirect":0.000000,"time_starttransfer":0.001221,"time_total":0.001288,"tls_earlydata":0,"url":"http://127.0.0.1:<P>/w","url.fragment":null,"url.host":"127.0.0.1","url.options":null,"url.password":null,"url.path":"/w","url.port":"<P>","url.query":null,"url.scheme":"http","url.user":null,"url.zoneid":null,"url_effective":"http://127.0.0.1:<P>/w","urle.fragment":null,"urle.host":"127.0.0.1","urle.options":null,"urle.password":null,"urle.path":"/w","urle.port":"<P>","urle.query":null,"urle.scheme":"http","urle.user":null,"urle.zoneid":null,"urlnum":0,"xfer_id":0,"curl_version":"libcurl/8.21.0 Schannel zlib/1.3.2 brotli/1.2.0 zstd/1.5.7 libidn2/2.3.8 libpsl/0.21.5 libssh2/1.11.1 WinLDAP"}`, no line ending.
- Decision: the task text says upper-case `HTTP_PROXY` is ignored. That holds on Linux and
  macOS, but the Windows reference build used it (the Windows environment ignores case;
  the manpage's ENVIRONMENT section says so, and ADR-0024 already records it), so FR-092
  states both rather than pinning "ignored". No new ADR: ADR-0024 covers it.
- Decision: FR-096 quotes the Digest value the reference build sent, but says it is
  WDigest's formatting and that Curl follows curl's own Digest code (ADR-0025), so no test
  should pin the Windows comma format for Curl's output.
- Not measured, and so stated without bytes: `--max-redirs -1`; a redirect that changes
  only the scheme; Digest with `qop` (random cnonce); a `-x` proxy given as `https://` or
  SOCKS; what `-c` writes for cookies from a `-b` string; the exact rule behind the
  ~400-day expiry cap in FR-100.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Done. Requirements.md has FR-086 to FR-106 for redirects, proxies, auth, cookies and -w; Roadmap Milestone 1 lists the HTTP tasks and the curl http(s) exit criterion
