---
id: BL-655
title: Decide whether HTTP/2 is offered on Linux and macOS, revisiting ADR-0017
priority: Normal
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Planning/Decisions]
requirement: none
created: 2026-09-28
completed:
---
# BL-655 — Decide whether HTTP/2 is offered on Linux and macOS, revisiting ADR-0017

## Goal

A new ADR revisits ADR-0017 (no HTTP/2 or HTTP/3 in Milestone 1; `--http2`, `--http2-prior-knowledge` and `--http3` refused everywhere as the Windows reference build refuses them) and decides, per platform, whether Curl now offers HTTP/2 over TLS (ALPN `h2`) and cleartext (`--http2-prior-knowledge`, `--http2` upgrade), given that the usual OpenSSL builds on Linux and macOS have HTTP/2 and negotiate it by default for `https://`.

## Context

- Conformance audit 2026-09-28, row 32 (Major, L; "revisit the ADR first"). ADR-0017 and the Roadmap's "Later / unscheduled" entry (hand-written HTTP/2 over `IConnection`, HPACK and framing; the BCL has none).
- Measure: `curl -V` on Linux or macOS (features list), `curl -v https://...` against an `h2`-capable server (does it offer `h2,http/1.1` in ALPN), and the Windows reference's refusal text for `--http2`, so the ADR states what each platform prints today and after.
- If the ADR decides to offer HTTP/2 off Windows, BL-656 to BL-660 implement it; if not, they are Deferred by their runner with the ADR as the reason. HTTP/3 stays out (QUIC is not in the BCL on every platform); say so.

## Acceptance criteria

- [ ] `Documentation/Planning/Decisions/ADR-<next free number>-<slug>.md` exists (number checked unused), Status Accepted, marked "Decided by Claude under Stewart's delegation", with the measurements, stating per platform what `https://` negotiates and what the three options do; ADR-0017 is marked amended or superseded accordingly.
- [ ] Consequences list BL-656 to BL-660, or state they are to be Deferred.
- [ ] `Documentation/Planning/Decisions/README.md` indexes the new ADR and updates ADR-0017's status column.

## Notes

## Log

- 2026-09-28: Created.
