---
id: BL-164
title: Record the ADR for connection reuse across requests and URLs
priority: Low
assignee: Claude
pipeline: docs
depends-on: [BL-157]
touches: [Documentation/Planning/Decisions]
requirement: none
created: 2026-09-26
completed:
---
# BL-164 — Record the ADR for connection reuse across requests and URLs

## Goal

An Accepted ADR decides how a handler hands back a reusable `IConnection` and what the pool key is.

## Context

- Filed from the Phase 1 HTTP plan (protocol-architect, 2026-09-26), item X8. Upstream references: https://curl.se/docs/manpage.html and https://curl.se/libcurl/c/libcurl-errors.html; behaviour measured on curl 8.21.0.
- curl reuses a connection across URLs on one command line and says `* Re-using existing connection` under `-v`; `%{num_connects}` counts new connections (https://curl.se/docs/manpage.html#-w, curl 8.21.0).
- BL-215 implements this.

## Acceptance criteria

- [ ] A new ADR under `Documentation/Planning/Decisions/` with the next free number, status Accepted, titled for connection reuse, states that it was decided by Claude under Stewart's delegation, and records the decision, the reasons and the alternatives rejected.
- [ ] `Documentation/Planning/Decisions/README.md` lists the new ADR.
- [ ] The ADR states how a handler returns a still-usable `IConnection`, the pool key (scheme, host, port, TLS options, proxy), and when a connection is not reusable (`Connection: close`, read-to-close body, error).
- [ ] The ADR explains how `%{num_connects}` and the `-v` "Re-using existing connection" line come out right.

## Notes

- Plan item: X8 in the Phase 1 HTTP plan (2026-09-26); plan keys in this file were replaced by their task IDs.

## Log

- 2026-09-26: Created.
