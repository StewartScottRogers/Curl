---
id: BL-1147
title: Point ADR-0186 decision 5 at ADR-0334's stale Digest retry
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Planning/Decisions/ADR-0186-a-connect-tunnel-answers-a-407-through-the-injected-proxy-authenticator.md]
requirement: none
created: 2026-10-01
completed: 2026-10-02
---
# BL-1147 — Point ADR-0186 decision 5 at ADR-0334's stale Digest retry

## Goal

ADR-0186 decision 5 ("One answer per connect") says that a `407` carrying a Digest challenge with `stale=true` is answered again, and links ADR-0334.

## Context

BL-864 made `TcpConnector` answer a stale Digest `407` to CONNECT afresh, with at most five reconnects, as curl 8.21.0 does (ADR-0334, BL-864 Notes). ADR-0186 decision 5 still reads as if every `407` to a CONNECT that sent a credential ends with exit 7. BL-864 could not edit ADR-0186 because the file was outside its touches.

## Acceptance criteria

- [x] ADR-0186 decision 5 names the stale Digest exception and links `ADR-0334-a-stale-digest-407-to-connect-is-answered-again-five-reconnects-at-most.md`.
- [x] ADR-0186's Status line says it is amended by ADR-0333 and ADR-0334.

## Notes

- Docs only, no `.cs` or project file changed, so `verify` was not needed (task-run, docs pipeline). Decision 5 now names the `stale=true` exception and links ADR-0334; the Status line links ADR-0333 and ADR-0334 with one clause each. The Decisions README row for ADR-0186 is outside `touches` and still reads "Accepted"; left as is.

## Log

- 2026-10-01: Created.
- 2026-10-02: Backlog -> Doing.
- 2026-10-02: Doing -> Done. ADR-0186 decision 5 names the stale Digest exception and links ADR-0334; its Status says amended by ADR-0333 and ADR-0334
