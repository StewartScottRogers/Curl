---
id: BL-163
title: Record the ADR for a transfer event sink that feeds -v and --trace
priority: Normal
assignee: Claude
pipeline: docs
depends-on: [BL-133]
touches: [Documentation/Planning/Decisions]
requirement: none
created: 2026-09-26
completed:
---
# BL-163 — Record the ADR for a transfer event sink that feeds -v and --trace

## Goal

An Accepted ADR decides how handlers and connectors report connection, TLS, header and data events so `Curl.Output` can render `-v`, `--trace` and `--trace-ascii`.

## Context

- Filed from the Phase 1 HTTP plan (protocol-architect, 2026-09-26), item X7. Upstream references: https://curl.se/docs/manpage.html and https://curl.se/libcurl/c/libcurl-errors.html; behaviour measured on curl 8.21.0.
- BL-133 records the progress-sink ADR for the progress meter; this ADR decides whether the event sink extends that sink or is a sibling, and says why.
- `-v` prints `* ` info lines, `> ` request headers and `< ` response headers; `--trace` dumps hex and ASCII (https://curl.se/docs/manpage.html#-v, #--trace; curl 8.21.0).
- BL-228, BL-229 and BL-242 depend on this.

## Acceptance criteria

- [ ] A new ADR under `Documentation/Planning/Decisions/` with the next free number, status Accepted, titled for the transfer event sink, states that it was decided by Claude under Stewart's delegation, and records the decision, the reasons and the alternatives rejected.
- [ ] `Documentation/Planning/Decisions/README.md` lists the new ADR.
- [ ] The ADR lists the event kinds (connection opened/reused, TLS handshake details, request header, response header, data sent, data received, info text) with their payloads, and states the relation to BL-133's progress sink.
- [ ] The ADR states that the default sink does nothing, so handlers and tests that ignore it are unaffected.

## Notes

- Plan item: X7 in the Phase 1 HTTP plan (2026-09-26); plan keys in this file were replaced by their task IDs.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
