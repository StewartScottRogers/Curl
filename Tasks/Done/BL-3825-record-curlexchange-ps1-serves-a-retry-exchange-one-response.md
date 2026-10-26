---
id: BL-3825
title: Record-CurlExchange.ps1 serves a --retry exchange: one response per attempt across connections, without hanging
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [Record-CurlExchange.ps1]
requirement: none
created: 2026-10-26
completed: 2026-10-26
---
# BL-3825 — Record-CurlExchange.ps1 serves a --retry exchange: one response per attempt across connections, without hanging

## Goal

`Record-CurlExchange.ps1 -Response <503>,<200> -CurlArgs '-sS','--retry','2','--retry-delay','1',...` records a retried HTTP exchange through real curl and through Curl and exits, so BL-3531 (CGR-002) can measure real curl.

## Context

BL-3531's run (2026-10-26) ran `Record-CurlExchange.ps1 -Port <n> -Response 'HTTP/1.1 503 X\r\nContent-Length: 2\r\n\r\ne\n','HTTP/1.1 200 OK\r\nContent-Length: 3\r\n\r\nok\n' -Curl C:\windows\system32\curl.exe -CurlArgs '-sS','--retry','2','--retry-delay','1','-T',<file>,http://127.0.0.1:<n>/p`: it never finished (over 10 minutes; with `Connection: close` on both responses, over 60 s), for `-T <file>`, `-F f=@<file>` and `-F f=value` alike, while Curl's `-F` runs finished in 2 s. The recorder must answer each attempt (a new connection after a 503 and on a kept-alive one) with the next `-Response` entry and write every request into `request.bin`.

## Acceptance criteria

- [x] The command above, with `-T`, `-F f=@<file>` and `-F f=value`, finishes through real curl 8.21.0 on Windows within 30 s and `request.bin` holds every attempt's request with its body.
- [x] Existing uses of `-Response` with one entry record the same bytes as before.

## Notes

Cause: the recorder served one connection by default, so the retry connection was never accepted. It now serves one connection per -Response entry unless -Connections is given or -AnswerHeldRequests is used; a single Response behaves as before. Measured through C:windowssystem32rl.exe: -T, -F f=@file and -F f=value each finished in under 3 s with both requests in request.bin.

## Log

- 2026-10-26: Created.
- 2026-10-26: Backlog -> Doing. Criteria at claim: 437073dc 3e4a4efe.
- 2026-10-26: Doing -> Done. Record-CurlExchange.ps1 serves one connection per -Response entry, so --retry exchanges record and exit
