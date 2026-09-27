---
id: BL-284
title: Supply the remaining -w variables curl 8.21.0 knows
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-225]
touches: [Curl.Output.UnitLibrary, Curl.Output.UnitTests, Documentation/Planning/Decisions]
requirement: none
created: 2026-09-26
completed: 2026-09-26
---
# BL-284 — Supply the remaining -w variables curl 8.21.0 knows

## Goal

`TransferWriteOutVariables` supplies every `-w` variable curl 8.21.0 knows that no other task covers, so none of them prints the unknown-variable warning.

## Context

- Found by BL-225, which supplied the response, size, count, URL, method, scheme, IP, error message and exit code variables. `%{time_*}`/`%{speed_*}` are BL-226, `%{json}`/`%{header_json}` BL-227, `%{onerror}`/`%time{}` BL-279.
- Still reported unknown today: `%{referer}`, `%{filename_effective}`, `%{url.<part>}` and `%{urle.<part>}`, `%{certs}`, `%{num_certs}`, `%{ssl_verify_result}`, `%{proxy_ssl_verify_result}`, `%{proxy_used}`, `%{num_retries}`, `%{conn_id}`, `%{xfer_id}`, `%{time_queue}`, `%{tls_earlydata}`, `%{ftp_entry_path}`. ADR-0015 ("What the report does not carry") says each needs a later ADR for its source; the ones from the command line need a constructor input, not a `TransferReport` member.
- Where a criterion says *measured*, run curl 8.21.0 - the mingw build `/mingw64/bin/curl`, first on PATH, which is the Windows reference (ADR-0009, ADR-0018) - against a loopback server (`Record-CurlExchange.ps1`), record the exact command and the bytes it produced in `Notes`, then pin them in a test. Never pin text that was not measured.

## Acceptance criteria

- [x] Each listed variable renders as curl 8.21.0 does for a file:// and an http:// transfer (measured, pinned in `TransferWriteOutVariablesTests`), or is recorded in an ADR as deliberately left unknown with the reason.
- [x] `dotnet build Curl.Output.UnitLibrary -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes and no new test needs `TestCategory=Integration`; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Output`.

## Notes

- Touches widened to `Documentation/Planning/Decisions`: the criterion allows an ADR for variables left unknown, and ADR-0041 records them. No task in Doing names that folder.
- Scope decided in ADR-0041 (Decided by Claude under Stewart's delegation). Supplied now: `ssl_verify_result`, `proxy_ssl_verify_result`, `tls_earlydata`, `num_retries` (all `0`), `ftp_entry_path` (empty) and `time_queue` (handler start; `0.000001` with timings, `0.000000` without, by ADR-0035's rule). Left unknown with a follow-up task each: `url.`/`urle.` parts (BL-300, needs BL-292's `CurlUrl`), `referer`/`filename_effective`/`conn_id`/`xfer_id` (BL-301, constructor inputs from `Curl.Console`, after BL-235), `proxy_used` (BL-302, report member), `certs`/`num_certs` (BL-303, TLS chain).
- The pipeline's plan and review stages were done in-session rather than by subagents: the change is six dictionary entries and their tests.
- Measured 2026-09-26, curl 8.21.0 (x86_64-w64-mingw32, Schannel) at `/mingw64/bin/curl`, one variable per run: `curl -s -o out.bin -w "%{<name>}" <url>`.
  - `file:///Z:/bl284tmp/wo.txt` (exit 0): referer `` · filename_effective `out.bin` · url.scheme `file` · url.user/password/options/host `` · url.port `0` · url.path `Z:/bl284tmp/wo.txt` · url.query/fragment/zoneid `` · urle.scheme `file` · urle.host `` · urle.port `0` · urle.path `Z:/bl284tmp/wo.txt` · certs `` · num_certs `0` · ssl_verify_result `0` · proxy_ssl_verify_result `0` · proxy_used `0` · num_retries `0` · conn_id `0` · xfer_id `0` · time_queue `0.000083` · tls_earlydata `0` · ftp_entry_path `` · `url.bogus` -> `curl: unknown --write-out variable: 'url.bogus'`.
  - `http://u:p@127.0.0.1:18284/wo.txt?q=1#frag` against `python -m http.server 18284 --bind 127.0.0.1` (exit 0): url.* and urle.* scheme..zoneid `http|u|p||127.0.0.1|18284|/wo.txt|q=1|frag|`; certs `` · num_certs `0` · ssl_verify_result `0` · proxy_ssl_verify_result `0` · proxy_used `0` · num_retries `0` · conn_id `0` · xfer_id `0` · time_queue `0.000038` · tls_earlydata `0` · ftp_entry_path ``. With `-e http://ref.example/x`: referer `http://ref.example/x`. `127.0.0.1:18284` (no scheme): url.port/urle.port `18284`, url.path `/`, url.host `127.0.0.1`, url.scheme `http`. Two URLs in one run: `[0][0]` then `[1][1]` for `[%{conn_id}][%{xfer_id}]`. With stdout output filename_effective is empty; with `-O` it is `wo.txt`.
  - `https://self-signed.badssl.com/` (exit 60): `[%{ssl_verify_result}][%{num_certs}][%{certs}][%{conn_id}]` -> `[0][0][][0]`.
  - `http://127.0.0.1:1/` (exit 7): `[%{conn_id}][%{xfer_id}][%{time_queue}][%{proxy_used}]` -> `[0][0][0.000034][0]`.
  - `https://example.com/` (exit 0): `[%{ssl_verify_result}][%{num_certs}][%{tls_earlydata}]` -> `[0][4][0]`; certs begins `Subject:CN=example.com`.
- Gates: `dotnet build` clean; fast tests green (Curl.Output 95 passed); `Measure-CodeQuality.ps1 -Library Curl.Output.UnitLibrary` 100% line, 100% branch, 0 failing, worst CRAP 10.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Done. -w prints ssl_verify_result, proxy_ssl_verify_result, tls_earlydata, num_retries, ftp_entry_path and time_queue as curl 8.21.0; the rest are ADR-0041 with BL-300..BL-303
