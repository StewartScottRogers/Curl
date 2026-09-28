---
id: BL-655
title: Decide how HTTP/2 is built and offered on every platform, superseding ADR-0017 for HTTP/2
priority: Normal
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Planning/Decisions]
requirement: none
created: 2026-09-28
completed:
---
# BL-655 — Decide how HTTP/2 is built and offered on every platform, superseding ADR-0017 for HTTP/2

## Goal

A new ADR supersedes ADR-0017's HTTP/2 half (its refusal of `--http2` and `--http2-prior-knowledge`) and decides how Curl offers HTTP/2 on Windows, Linux and macOS: HTTP/2 over TLS (ALPN `h2`), cleartext with prior knowledge and the `h2c` upgrade, the hand-built HPACK and framing in `Curl.Http2.UnitLibrary`, the default ALPN list per platform for `https://`, and whose output text each platform matches.

## Context

- Conformance audit 2026-09-28, row 32 (Major, L). ADR-0017 and the Roadmap's "Later / unscheduled" entry (hand-written HTTP/2 over `IConnection`, HPACK and framing; the BCL's are internal to `System.Net.Http`).
- Standing rule (root `CLAUDE.md`, "Decisions", Stewart 2026-09-28): if any official curl build supports a feature, Curl supports it on every platform; output text still matches the platform's curl where both do the same thing; the ADR decides HOW, never WHETHER. The usual Linux and macOS builds and curl.se's official Windows build of curl 8.22.0 (nghttp2 1.70.0, https://curl.se/windows/, checked 2026-09-28) all have HTTP/2, so `--http2` and `--http2-prior-knowledge` work everywhere. Whether a plain `https://` offers `h2` by default on Windows (the Schannel reference build offers only `http/1.1`; curl.se's build offers `h2,http/1.1`) is this ADR's call, stated with its reason.
- The hand-built pieces go in their own library (`Curl.Http2.UnitLibrary`, created by BL-715; HPACK BL-656, frames BL-657), which `Curl.Protocol.Http.UnitLibrary` references (BL-667, BL-668). HTTP/3 is decided separately by BL-718, which supersedes the rest of ADR-0017.
- Measure: `curl -V` on Linux or macOS and of curl.se's Windows build (features list), `curl -v https://...` against an `h2`-capable server with each (ALPN offered and the `-v` lines), `--http2-prior-knowledge` against a cleartext h2 server, and the Windows reference's current refusal text for the record.

## Acceptance criteria

- [ ] `Documentation/Planning/Decisions/ADR-<next free number>-<slug>.md` exists (number checked unused), Status Accepted, marked "Decided by Claude under Stewart's delegation", with the measurements, stating per platform what `https://` negotiates by default and what `--http2`, `--http2-prior-knowledge` and `--http1.1` do (all accepted everywhere), and which build's text each platform matches; ADR-0017 is marked superseded for HTTP/2.
- [ ] Consequences list BL-656 to BL-660, BL-715, BL-716 and BL-717.
- [ ] `Documentation/Planning/Decisions/README.md` indexes the new ADR and updates ADR-0017's status column.

## Notes

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
