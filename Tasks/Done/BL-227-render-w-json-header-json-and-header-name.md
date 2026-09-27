---
id: BL-227
title: Render -w %{json}, %{header_json} and %header{name}
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-225]
touches: [Curl.Output.UnitLibrary, Curl.Output.UnitTests, Documentation]
requirement: none
created: 2026-09-26
completed: 2026-09-27
---
# BL-227 — Render -w %{json}, %{header_json} and %header{name}

## Goal

`%{json}` and `%{header_json}` render hand-written JSON with curl's key order and escaping.

## Context

- Filed from the Phase 1 HTTP plan (protocol-architect, 2026-09-26), item O4. Upstream references: https://curl.se/docs/manpage.html and https://curl.se/libcurl/c/libcurl-errors.html; behaviour measured on curl 8.21.0.
- Hand-written JSON; no JSON library.
- Where a criterion says *measured*, run curl 8.21.0 - the mingw build `/mingw64/bin/curl`, first on PATH, which is the Windows reference (ADR-0009 and the BL-153 ADR) - against a loopback server (the BL-165 recording script `Record-CurlExchange.ps1` once it exists), record the exact command and the bytes it produced in `Notes`, then pin them in a test. Never pin text that was not measured.

## Acceptance criteria

- [x] Key order and escaping match curl 8.21.0 (measured).
- [x] `dotnet build Curl.Output.UnitLibrary -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes and no new test needs `TestCategory=Integration`; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Output`.

## Notes

- Plan item: O4 in the Phase 1 HTTP plan (2026-09-26); plan keys in this file were replaced by their task IDs.
- Resumed 2026-09-27 after a cut-off run that had only claimed the task; started from the claim commit.
- `%header{name}` was already rendered by `WriteOutTemplateRenderer` (BL-225/BL-235 work); this task added `%{json}` and `%{header_json}`.
- `touches` gained `Documentation` for ADR-0063 and its index row; no task in Doing names it.
- Decision (ADR-0063, decided by Claude under Stewart's delegation): each variable formatter returns a `WriteOutValue` (text and JSON form); `json` and `header_json` are variables of `TransferWriteOutVariables`; JSON is hand-written in `WriteOutJson`; `curl_version` is `LibraryVersion`, defaulting to this tool's own `-V` library text (`libcurl/8.21.0 Schannel` on Windows), not the reference build's zlib/zstd/libidn2 list; `size_delivered` (which `%{json}` lists) prints `size_download`.
- Measured with curl 8.21.0 (mingw, Schannel, `/mingw64/bin/curl`), 2026-09-27:
  - `Record-CurlExchange.ps1 -Port 18227 -Response 'HTTP/1.1 200 OK\r\nContent-Type: text/plain\r\nContent-Length: 5\r\nX-A: 1\r\n\r\nhello' -CurlArgs '-s','-o','NUL','-w','%{json}','http://127.0.0.1:18227/j?q=1'` printed `{"certs":"","conn_id":0,"content_type":"text/plain","errormsg":null,"exitcode":0,"filename_effective":"NUL","ftp_entry_path":null,"http_code":200,"http_connect":0,"http_version":"1.1","local_ip":"127.0.0.1","local_port":62095,"method":"GET","num_certs":0,"num_connects":1,"num_headers":3,"num_redirects":0,"num_retries":0,"proxy_ssl_verify_result":0,"proxy_used":0,"redirect_url":null,"referer":null,"remote_ip":"127.0.0.1","remote_port":18227,"response_code":200,"scheme":"http","size_delivered":5,"size_download":5,"size_header":72,"size_request":84,"size_upload":0,"speed_download":155,"speed_upload":0,"ssl_verify_result":0,"time_appconnect":0.000000,"time_connect":0.000708,"time_namelookup":0.000071,"time_posttransfer":0.000784,"time_pretransfer":0.000784,"time_queue":0.000055,"time_redirect":0.000000,"time_starttransfer":0.032135,"time_total":0.032216,"tls_earlydata":0,"url":"http://127.0.0.1:18227/j?q=1","url.fragment":null,"url.host":"127.0.0.1","url.options":null,"url.password":null,"url.path":"/j","url.port":"18227","url.query":"q=1","url.scheme":"http","url.user":null,"url.zoneid":null,"url_effective":"http://127.0.0.1:18227/j?q=1","urle.fragment":null,"urle.host":"127.0.0.1","urle.options":null,"urle.password":null,"urle.path":"/j","urle.port":"18227","urle.query":"q=1","urle.scheme":"http","urle.user":null,"urle.zoneid":null,"urlnum":0,"xfer_id":0,"curl_version":"libcurl/8.21.0 Schannel zlib/1.3.2 brotli/1.2.0 zstd/1.5.7 libidn2/2.3.8 libpsl/0.21.5 libssh2/1.11.1 WinLDAP"}`, no line ending. Pinned in `TransferWriteOutVariablesJsonTests` with `time_queue` 0.000001 (ADR-0035) and the reference `curl_version` set through `LibraryVersion`.
  - `curl -s -w '%{json}' foo://x/` (exit 1) printed `"errormsg":"Protocol \"foo\" not supported"`, `"exitcode":1`, `"filename_effective":null`, `"scheme":null`, `"url.port":null`, `"url.host":"x"`, `"conn_id":-1`, everything else as for no transfer. Pinned whole, with this tool's zero timings.
  - `curl -s -o NUL -e $'a\rb\nc\x02' -w '%{json}' file:///nonexist` printed `"referer":"a\rb\nc\u0002"`; a response `Content-Type: a"b\c<TAB>d<0x1F>` printed `"content_type":"a\"b\\c\td\u001f"`.
  - `Record-CurlExchange.ps1 -Port 18227 -Response 'HTTP/1.1 200 OK\r\nContent-Length: 5\r\nX-Dup: one\r\nSet-Cookie: a=1\r\nx-dup:  two  \r\nX-Quote: a"b\\c\x01\x7f\xe9\r\nContent-Type: text/plain\r\nEmpty:\r\n\r\nhello' -CurlArgs '-s','-o','NUL','-w','%{header_json}|%header{x-dup}|%header{Empty}|','http://127.0.0.1:18227/w'` printed the bytes `{"content-length":["5"],` CR LF `"x-dup":["one","two"],` CR LF `"set-cookie":["a=1"],` CR LF `"x-quote":["a\"b\\c\u0001` 7F E9 `"],` CR LF `"content-type":["text/plain"],` CR LF `"empty":[""]` CR LF `}|one||`. The raw E9 byte is not pinned: this tool writes header text as UTF-8 (recorded in ADR-0063).
  - A header `X-C: a<TAB>b<BS>c<FF>d<0x1F>e/f` printed `"x-c":["a\tb\bc\fd\u001fe/f"]`.
  - `curl -s -o /dev/null -w '%{header_json}' file:///tmp/a.txt` printed `{` CR LF `}`.
- `Measure-CodeQuality.ps1 -Library Curl.Output.UnitLibrary`: 100% line, 100% branch, 243 members, 0 failing, worst CRAP 10. The first cut of the escape switch measured complexity 11; the two-character escapes became a lookup table.

## Log

- 2026-09-26: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. -w %{json} and %{header_json} print curl 8.21.0's key order, types, nulls and escaping; %header{name} already rendered
