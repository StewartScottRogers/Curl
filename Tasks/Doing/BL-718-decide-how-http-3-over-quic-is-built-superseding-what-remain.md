---
id: BL-718
title: Decide how HTTP/3 over QUIC is built, superseding what remains of ADR-0017
priority: Normal
assignee: Claude
pipeline: docs
depends-on: [BL-655, BL-695]
touches: [Documentation/Planning/Decisions]
requirement: none
created: 2026-09-28
completed:
---
# BL-718 — Decide how HTTP/3 over QUIC is built, superseding what remains of ADR-0017

## Goal

An ADR fixes how Curl speaks HTTP/3 over a hand-built QUIC on every platform: the library split (`Curl.Quic.UnitLibrary` for RFC 9000/9001/9002, `Curl.Http3.UnitLibrary` for RFC 9114/9204, TLS from `Curl.Tls.UnitLibrary`), the contracts added to `Curl.Protocol.Abstractions.UnitLibrary`, where the UDP socket lives, how `--http3` races HTTP/3 against HTTP/2 and HTTP/1.1 and falls back while `--http3-only` does not, the transport parameters and congestion control curl's official build uses, and the exit codes; it supersedes ADR-0017's remaining HTTP/3 refusal.

## Context

- Standing rule (root `CLAUDE.md`, "Decisions", 2026-09-28): QUIC and HTTP/3 are built by hand, never refused. `System.Net.Quic` needs `msquic`, which is not on every platform, and cannot be driven through `IConnection` or reproduce curl's bytes, so it is not used.
- curl: `--http3` "Use HTTP/3. For HTTPS, this negotiates HTTP/3 in the QUIC handshake."; `--http3-only` "Use HTTP/3 only, and do not fall back to earlier HTTP versions if the server does not support HTTP/3." (https://curl.se/docs/manpage.html, curl 8.23.0, checked 2026-09-28). curl.se's official Windows build of curl 8.22.0 has HTTP/3 through ngtcp2 1.25.0 and nghttp3 1.18.0 (https://curl.se/windows/, checked 2026-09-28): that build, and a Linux build with ngtcp2, are the reference for bytes, `-v` text and timing. ADR-0017 refused `--http3` and `--http3-only` with exit 2; BL-655 supersedes its HTTP/2 half.
- Measure with the official build through `Record-CurlExchange.ps1 -NoServer` against a local HTTP/3 server (for example `nghttpx`/`h2o`/`quiche` server, or a public one if no local one can be run; record which): `--http3 -v`, `--http3-only -v`, `--http3` against a server without HTTP/3 (fallback), `--http3-only` against one (exit code), and capture the client's transport parameters (a QUIC-aware packet capture with the key log, `SSLKEYLOGFILE`, decoded by Wireshark or similar).
- Protocol libraries stay off the network and never reference each other: `Curl.Protocol.Http.UnitLibrary` gets QUIC streams through Abstractions contracts (BL-721); `Curl.Networking.UnitLibrary` owns the UDP adapter and composes `Curl.Quic` (BL-728). Reference rules: BL-667's ADR (add `Curl.Quic` and `Curl.Http3` to its list if not already there).
- Exit codes: 95 `CURLE_HTTP3`, 96 `CURLE_QUIC_CONNECT_ERROR` (https://curl.se/libcurl/c/libcurl-errors.html, checked 2026-09-28), 28 for timeouts; the ADR maps each QUIC and HTTP/3 error to one.

## Acceptance criteria

- [ ] `Documentation/Planning/Decisions/ADR-<next free number>-<slug>.md` exists (number checked unused), Status Accepted, marked "Decided by Claude under Stewart's delegation", with the measurements, the library and contract design, the racing and fallback rules, the transport parameters and congestion controller, and the error mapping.
- [ ] ADR-0017 is marked superseded (by this ADR and BL-655's), and Consequences list BL-719 to BL-735.
- [ ] `Documentation/Planning/Decisions/README.md` indexes the new ADR and updates ADR-0017's status column.

## Notes

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
