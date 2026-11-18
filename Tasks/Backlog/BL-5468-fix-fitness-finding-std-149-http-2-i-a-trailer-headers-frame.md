---
id: BL-5468
title: Fix fitness finding STD-149: HTTP/2 -I: a trailer HEADERS frame right after the response head (no DATA) is dropped from -I, -i and -D output
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Original.Protocol.Http.UnitLibrary/Http2StreamConnection.cs]
requirement: none
created: 2026-11-18
completed:
---
# BL-5468 — Fix fitness finding STD-149: HTTP/2 -I: a trailer HEADERS frame right after the response head (no DATA) is dropped from -I, -i and -D output

## Goal

STD-149 from the outside fitness review of 2026-11-18 06:33 no longer reproduces: HTTP/2 -I: a trailer HEADERS frame right after the response head (no DATA) is dropped from -I, -i and -D output. Curl matches the reference curl as the finding's acceptance check states.

## Context

From the outside fitness review `Curl-Fitness-Review-2026-11-18T0633` (commit `ec53afc`, preview release binaries). In the finding, PORT is this Curl, REF is curl 8.22.0 Schannel on Windows (Git for Windows `curl.exe`), REF2 is curl 8.18.0 OpenSSL on WSL Ubuntu. Mock servers bind 127.0.0.1 and port numbers are examples. Where the finding refers to another finding's setup (e.g. "the setup from STD-116"), find the task titled with that finding ID. Measure real curl with `Record-CurlExchange.ps1` before pinning output text. The finding, verbatim:

- **Severity:** Low
- **Confidence:** Confirmed (3 runs, Linux build; Windows not run because the h2 mock was in WSL)
- **Protocol/area:** HTTP/2 HEAD (neighbour of STD-098)
- **Repro:** a Python `h2` prior-knowledge server on 127.0.0.1:43101; for `/headtrail` it sends HEADERS `:status 200`, `content-length: 5` (no END_STREAM), then HEADERS `x-t: trailerval` with END_STREAM. `curl -sS --http2-prior-knowledge -I http://127.0.0.1:43101/headtrail -w "[%{http_code} %{num_headers} %{exitcode}]\n"`; also over TLS h2 and with `-I -i` or `-I -D hd.txt`.
- **Expected (REF2 8.18):** `HTTP/2 200`, `content-length: 5`, blank line, `x-t: trailerval`, `[200 1 0]`, exit 0; `hd.txt` contains `x-t: trailerval`.
- **Actual (PORT):** the trailer line is missing from stdout and from `hd.txt`; `[200 1 0]`. With DATA before the trailer the PORT matches.
- **Likely location:** `Curl.Original.Protocol.Http.UnitLibrary/Http2StreamConnection.cs`, the BL-5426 path that ends `-I` at the final head and stops before the trailer frame is written.
- **Acceptance check:** stdout includes `x-t: trailerval` before `[200 1 0]`.

## Acceptance criteria

- [ ] stdout includes `x-t: trailerval` before `[200 1 0]`.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-18: Created from fitness review Curl-Fitness-Review-2026-11-18T0633, finding STD-149 (Low).