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
completed: 2026-09-27
---
# BL-164 — Record the ADR for connection reuse across requests and URLs

## Goal

An Accepted ADR decides how a handler hands back a reusable `IConnection` and what the pool key is.

## Context

- Filed from the Phase 1 HTTP plan (protocol-architect, 2026-09-26), item X8. Upstream references: https://curl.se/docs/manpage.html and https://curl.se/libcurl/c/libcurl-errors.html; behaviour measured on curl 8.21.0.
- curl reuses a connection across URLs on one command line and says `* Re-using existing connection` under `-v`; `%{num_connects}` counts new connections (https://curl.se/docs/manpage.html#-w, curl 8.21.0).
- BL-215 implements this.

## Acceptance criteria

- [x] A new ADR under `Documentation/Planning/Decisions/` with the next free number, status Accepted, titled for connection reuse, states that it was decided by Claude under Stewart's delegation, and records the decision, the reasons and the alternatives rejected.
- [x] `Documentation/Planning/Decisions/README.md` lists the new ADR.
- [x] The ADR states how a handler returns a still-usable `IConnection`, the pool key (scheme, host, port, TLS options, proxy), and when a connection is not reusable (`Connection: close`, read-to-close body, error).
- [x] The ADR explains how `%{num_connects}` and the `-v` "Re-using existing connection" line come out right.

## Notes

- Plan item: X8 in the Phase 1 HTTP plan (2026-09-26); plan keys in this file were replaced by their task IDs.
- 2026-09-27: Delivered as ADR-0050 (`Documentation/Planning/Decisions/ADR-0050-connections-are-reused-across-requests-and-urls-through-a-pooling-connector.md`), with behaviour measured on curl 8.21.0 (mingw, Schannel) against local HTTP/1.1 servers and a forward proxy.
- Measured: the `-v` line is `* Reusing existing http: connection with host <name>` (or `with proxy <name>`), not "Re-using existing connection" as the Context says; the ADR pins the measured text. The pool holds five connections in total; `--no-keepalive` does not stop reuse.
- Follow-ups filed: BL-326 (two ADRs share number 0040), BL-325 (BL-215 touches only `Curl.Networking`, but ADR-0050 also needs Abstractions, Http and Console changes).
- ADR-0046 line 99 still shows the old reuse-line wording; left as is because accepted ADRs are not edited, and ADR-0050 records the correction.

## Log

- 2026-09-26: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. ADR-0050 decides connection reuse: MarkReusable hand-back, pool key, non-reuse cases, num_connects and -v reuse lines
