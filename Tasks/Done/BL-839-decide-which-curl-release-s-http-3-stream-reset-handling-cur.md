---
id: BL-839
title: Decide which curl release's HTTP/3 stream-reset handling Curl matches
priority: Normal
assignee: Claude
pipeline: docs
depends-on: [BL-731]
touches: [Documentation/Planning/Decisions]
requirement: none
created: 2026-09-28
completed: 2026-09-29
---
# BL-839 — Decide which curl release's HTTP/3 stream-reset handling Curl matches

## Goal

An ADR, marked "Decided by Claude under Stewart's delegation", states which curl release Curl's HTTP/3 stream-reset behaviour follows, and so whether a reset with `H3_REQUEST_REJECTED` is retried on a new connection and what text a reset prints; BL-834 implements what it decides.

## Context

- curl's handling of a reset HTTP/3 request stream (`recv_closed_stream` in `lib/vquic/curl_ngtcp2.c`) differs by release, checked against curl's source on 2026-09-28:
  - `curl-8_18_0` (the source of curl.se's Windows build 8.18.0 with ngtcp2 1.21.0 and nghttp3 1.15.0, the measured HTTP/3 reference of ADR-0144 and ADR-0172): every reset is `failf(data, "HTTP/3 stream %" PRId64 " reset by server", stream->id)`, returning `CURLE_PARTIAL_FILE` (18) once body bytes arrived and `CURLE_HTTP3` (95) before. There is no `H3_REQUEST_REJECTED` case and no retry. This is what BL-731 implemented (`HttpTransferMessages.Http3StreamReset`, used by `Curl.Protocol.Http.UnitLibrary/Http3StreamConnection.cs`).
  - `curl-8_19_0` and later (seen at 8.19.0, 8.20.0 and, in the renamed `lib/vquic/cf-ngtcp2.c`, 8.22.0): a reset with `CURL_H3_ERR_REQUEST_REJECTED` (0x10b) prints the info line `HTTP/3 stream <id> refused by server, try again on a new connection`, closes the connection, sets `data->state.refused_stream` and returns `CURLE_RECV_ERROR` so `Curl_retry_request` retries the request on a new connection (as `lib/http2.c` does for `NGHTTP2_REFUSED_STREAM`); a reset after complete response headers when no body was wanted (`-I`) is ignored; every other reset prints `HTTP/3 stream <id> reset by server (error 0x<hex> <name>)` with `vquic_h3_err_str`'s name.
- The standing rules (root `CLAUDE.md`, "Decisions"): match the platform's curl, measure real curl before pinning output text, simplest thing that stays a drop-in replacement. curl.se's current Windows release is 8.22.0 (https://curl.se/windows/, per ADR-0144), while WinGet's `cURL.cURL` installed 8.18.0 when ADR-0144 measured. HTTP/3 behaviour should follow one release throughout, so the ADR also says whether the choice moves the other `curl_ngtcp2.c` references in ADR-0144 section 7 and ADR-0172, and lists which rows change (each change then becomes its own follow-up task, filed by this task through `task-board.ps1 new`).
- If the decision is to follow 8.19.0 or later, the ADR also records, from the source at the chosen tag (and measured with that build where a server can be made to do it): whether the retry is independent of `--retry` (as `refused_stream` is in curl), how many times a refused request is retried before the transfer fails and with what exit and message, the exact reset text with its hex format and the error-name table, and the `-I` exemption.
- The ADR is a new file under `Documentation/Planning/Decisions/`, numbered with the next free number (0173 when this task was filed; check first), plus a sentence in ADR-0172 pointing to it. Add it to `Documentation/Planning/Decisions/README.md` if that file indexes ADRs.

## Acceptance criteria

- [x] `Documentation/Planning/Decisions/ADR-<next>-<slug>.md` exists, is marked "Decided by Claude under Stewart's delegation", names the curl tag whose `curl_ngtcp2.c`/`cf-ngtcp2.c` Curl's HTTP/3 reset handling follows, and states for that tag: the message and exit code for a reset before and after body bytes; what `H3_REQUEST_REJECTED` does (retried or not, how often, with which info line, and the final exit and message when every attempt is refused); and whether `-I` ignores a reset after headers.
- [x] `Documentation/Planning/Decisions/ADR-0172-http-3-requests-run-on-an-http3session-and-fall-back-to-tcp-only-when-quic-fails.md` links the new ADR where it describes the reset rows.
- [x] This task's Notes name the new ADR's file, so BL-834 can find it.
- [x] No `.cs` or project file changes.

## Notes

- The ADR is `Documentation/Planning/Decisions/ADR-0187-http-3-stream-resets-follow-curl-8-21-0-and-a-refused-stream-is-retried-on-a-new-connection.md`. BL-834 implements its Decision items 1 to 4.
- Decision: follow `curl-8_21_0`, because `-V`, the `User-Agent`, ADR-0018, ADR-0175 and ADR-0179 already make Curl curl 8.21.0; 8.18.0 was only the HTTP/3 build that happened to be installed, and 8.22.0 would put HTTP/3 one release ahead of the rest.
- Read from source at `curl-8_21_0` (2026-09-29): `lib/vquic/cf-ngtcp2.c`, `cf-ngtcp2-cmn.c`, `vquic.c`, `vquic_int.h`, `lib/transfer.c`, `lib/multi.c`. Not measured: no curl 8.21.0 HTTP/3 build is installed and no local HTTP/3 server resets on demand.
- Found in the source: a refused request is retried up to 5 times (`CONN_MAX_RETRIES`), independent of `--retry`; the sixth refusal ends with exit 56 (not 55: `multi.c` keeps the read's `CURLE_RECV_ERROR`) and `Connection died, tried 5 times before giving up`.
- Diffing every `failf` text between `curl_ngtcp2.c`@8.18.0 and `cf-ngtcp2.c`+`cf-ngtcp2-cmn.c`@8.21.0: only the reset text and `can get bidi streams` -> `cannot open bidi streams` changed; in `vquic.c` the HTTP-proxy refusal went and a Unix-socket message came. Follow-ups filed: BL-867 (Unix socket refusal), BL-868 (bidi stream open failure); the proxy change is BL-837's (note added there); the `:status` row rides on BL-834 (note added there).
- `Documentation/Planning/Decisions/README.md` was also missing ADR-0186's row; added it beside ADR-0187's.

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. ADR-0187 decides HTTP/3 follows curl-8_21_0: reset text with error code, H3_REQUEST_REJECTED retried up to 5 times then exit 56, -I ignores a reset after the head
