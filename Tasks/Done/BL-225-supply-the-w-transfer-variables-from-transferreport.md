---
id: BL-225
title: Supply the -w transfer variables from TransferReport
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-224, BL-160]
touches: [Curl.Output.UnitLibrary, Curl.Output.UnitTests]
requirement: none
created: 2026-09-26
completed: 2026-09-26
---
# BL-225 — Supply the -w transfer variables from TransferReport

## Goal

The renderer's variable source reads response, size, count, URL, method, scheme, IP, error message and exit code variables from `TransferReport` and the transfer result.

## Context

- Filed from the Phase 1 HTTP plan (protocol-architect, 2026-09-26), item O2. Upstream references: https://curl.se/docs/manpage.html and https://curl.se/libcurl/c/libcurl-errors.html; behaviour measured on curl 8.21.0.
- The BL-158 ADR maps each field to its variables.
- Where a criterion says *measured*, run curl 8.21.0 - the mingw build `/mingw64/bin/curl`, first on PATH, which is the Windows reference (ADR-0009 and the BL-153 ADR) - against a loopback server (the BL-165 recording script `Record-CurlExchange.ps1` once it exists), record the exact command and the bytes it produced in `Notes`, then pin them in a test. Never pin text that was not measured.

## Acceptance criteria

- [x] Each variable's formatting is measured on curl 8.21.0 and pinned.
- [x] `dotnet build Curl.Output.UnitLibrary -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes and no new test needs `TestCategory=Integration`; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Output`.

## Notes

- Plan item: O2 in the Phase 1 HTTP plan (2026-09-26); plan keys in this file were replaced by their task IDs.

- Delivered: `TransferWriteOutVariables : IWriteOutVariableSource` in `Curl.Output.UnitLibrary`, built from `(TransferResult, url, urlNumber, requestUrl, scheme)`. It supplies `response_code`, `http_code`, `http_connect`, `http_version`, `method`, `content_type`, `redirect_url`, `url_effective`, `num_redirects`, `size_header`, `size_request`, `size_download`, `size_upload`, `num_connects`, `num_headers`, `local_ip`, `local_port`, `remote_ip`, `remote_port`, `exitcode`, `errormsg`, `url`, `urlnum`, `scheme`, and `%header{name}` from `ResponseHeaders`. Pinned in `TransferWriteOutVariablesTests`.
- Measured 2026-09-26 with curl 8.21.0 (mingw, Schannel, `C:\Program Files\Git\mingw64\bin\curl.exe`), template `W` = `rc=%{response_code}|hc=%{http_code}|hcon=%{http_connect}|hv=%{http_version}|m=%{method}|ct=%{content_type}|ru=%{redirect_url}|ue=%{url_effective}|nr=%{num_redirects}|sh=%{size_header}|sr=%{size_request}|sd=%{size_download}|su=%{size_upload}|nc=%{num_connects}|lip=%{local_ip}|lp=%{local_port}|rip=%{remote_ip}|rp=%{remote_port}|ec=%{exitcode}|em=%{errormsg}|u=%{url}|un=%{urlnum}|s=%{scheme}|nh=%{num_headers}\n`, each run as `curl -s -o NUL -w W <url>`:
  - `Record-CurlExchange.ps1 -Port 18225 -Response 'HTTP/1.1 302 Found\r\nLocation: /next?q=1\r\nContent-Type: text/plain; charset=utf-8\r\nX-A: 1\r\nContent-Length: 5\r\n\r\nhello'`, url `http://127.0.0.1:18225/a?b` -> `rc=302|hc=302|hcon=000|hv=1.1|m=GET|ct=text/plain; charset=utf-8|ru=http://127.0.0.1:18225/next?q=1|ue=http://127.0.0.1:18225/a?b|nr=0|sh=111|sr=82|sd=5|su=0|nc=1|lip=127.0.0.1|rip=127.0.0.1|rp=18225|ec=0|em=|u=http://127.0.0.1:18225/a?b|un=0|s=http|nh=4` (this run's template had no `lp`).
  - `file:///C:/Windows/win.ini` -> `rc=000|hc=000|hcon=000|hv=0|m=GET|ct=|ru=|ue=file://C:/Windows/win.ini|nr=0|sh=0|sr=0|sd=92|su=0|nc=0|lip=|lp=-1|rip=|rp=-1|ec=0|em=|u=file:///C:/Windows/win.ini|un=0|s=file|nh=3`.
  - `http://127.0.0.1:1/x` -> `rc=000|hc=000|hcon=000|hv=0|m=GET|ct=|ru=|ue=http://127.0.0.1:1/x|nr=0|sh=0|sr=0|sd=0|su=0|nc=0|lip=|lp=-1|rip=|rp=-1|ec=7|em=Failed to connect to 127.0.0.1:1 after 2043 ms: Could not connect to server|u=http://127.0.0.1:1/x|un=0|s=http|nh=0`.
  - `nope://x/` -> `...|ec=1|em=Protocol "nope" not supported|u=nope://x/|un=0|s=|nh=0`.
  - `file:///C:/Windows/win.ini FILE:///C:/Windows/nosuch.ini` -> second line `...|ue=file://C:/Windows/nosuch.ini|...|ec=37|em=Could not open file C:/Windows/nosuch.ini|u=FILE:///C:/Windows/nosuch.ini|un=1|s=file|nh=0`.
  - An `HTTP/1.0 200` response, url `HTTP://127.0.0.1:18225/` -> `hv=1`, `u=HTTP://127.0.0.1:18225/`, `s=http`.
  - `-f -d abcdef` against a 404 -> `rc=404|hc=404|hv=1.1|m=POST|sh=45|sr=155|sd=0|su=6|ec=22|em=The requested URL returned error: 404`.
  - `-p -x http://127.0.0.1:18225 http://example.invalid/` against a `407` CONNECT reply -> `rc=000|hc=000|hcon=407|ec=7|em=CONNECT tunnel failed, response 407`.
  - `-X PATCH` -> `m=PATCH`. Against a TcpListener on `[::1]:18226` -> `lip=::1|rip=::1|rp=18226|ue=http://[::1]:18226/`.
- Decision: an unknown status code prints `000` (curl's `%03ld`), an unknown HTTP version `0`, HTTP/1.0 `1` (measured, not `1.0`), HTTP/2 and 3 their major number; an unknown port `-1`; a missing method `GET` (curl printed `GET` for file:// and for failed connects); missing text values print nothing.
- Decision: `%{scheme}` comes from a constructor argument, the lower-case scheme of the handler that ran, `null` when none, because curl printed `file` for `FILE://` and nothing for an unsupported `nope://`; the URL text alone cannot tell those apart. `%{url}` is the URL as typed; `%{url_effective}` is the report's `EffectiveUrl`, else the URL handed to the handler (ADR-0015).
- Decision: without a report, `size_download` is `BytesTransferred` (ADR-0015) and `size_upload` is `0`, what curl printed for every download measured.
- Decision: header values are trimmed of spaces and tabs at both ends, BL-224's measurement (`X-Lf: a b  ` -> `a b`).
- Decision: no new ADR. Every choice above is a measured curl behaviour or ADR-0015's existing mapping; the constructor shape is recorded here.
- Not covered, and still reported unknown: `%{time_*}`/`%{speed_*}` (BL-226), `%{json}`/`%{header_json}` (BL-227), `%{onerror}`/`%time{}` (BL-279). Filed BL-281 for the other variables curl knows (`referer`, `filename_effective`, `url.*`, certificate and connection ids, ...) and BL-282 for the file:// handler's pseudo-headers (curl prints `nh=3`; we print 0 because the file handler returns no report).
- Gates: `dotnet build -warnaserror` clean; every fast test project green (Curl.Output.UnitTests 47 passed); `Measure-CodeQuality.ps1 -Library Curl.Output*` 100% line, 100% branch, 0 failing members, worst CRAP 8.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Done. TransferWriteOutVariables supplies the -w response, size, count, URL, method, scheme, IP, error message and exit code variables as curl 8.21.0 prints them
