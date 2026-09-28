---
id: BL-579
title: Decide how the WebSocket handler upgrades an HTTP request without referencing Curl.Protocol.Http
priority: High
assignee: Claude
pipeline: docs
depends-on: [BL-498, BL-515]
touches: [Documentation/Planning/Decisions]
requirement: none
created: 2026-09-28
completed:
---
# BL-579 — Decide how the WebSocket handler upgrades an HTTP request without referencing Curl.Protocol.Http

## Goal

An ADR fixes how `Curl.Protocol.Ws.UnitLibrary` sends curl 8.21.0's upgrade request and reads the `101` reply while protocol libraries may not reference each other, which HTTP options apply to `ws://`/`wss://` (`-H`, `-A`, `-u`, `-b`, proxies, `-i`, `-D`), what the curl tool does with the connection once upgraded (what it writes, whether it sends anything from standard input or `-d`, when it ends), and how the random `Sec-WebSocket-Key` and masking keys are injected for tests.

## Context

- Conformance audit 2026-09-28, row 36 (Blocker, L: Ws project empty). `Curl.Protocol.Ws.UnitLibrary/CLAUDE.md`: Abstractions only, `IConnection`.
- In curl, WebSocket lives inside its HTTP code; here `ws` has its own library (Product Overview). Options: a small upgrade-request writer and head reader inside the Ws library; a shared head reader moved into Abstractions; or a request-writer contract in Abstractions that `Curl.Console` fills from the HTTP library. Weigh duplication against a new contract; `HttpRequestOptions` already sits in Abstractions (ADR-0014).
- Measure before deciding, with `Record-CurlExchange.ps1` (HTTP mode answers a canned `101` with `Upgrade: websocket`, a correct or wrong `Sec-WebSocket-Accept`, then frames; `-HoldOpenMilliseconds` keeps it open): `curl ws://127.0.0.1:<P>/`, with `-H`, `-u`, `-i`, `-v`, with standard input, a text frame, a binary frame, a close frame, and a `200` instead of `101`; record request bytes, stdout, stderr, exit code.
- RFC 6455. Depends on BL-498 (timeouts) and BL-515 (endpoints).

## Acceptance criteria

- [ ] The measurements above are recorded in the ADR's Context.
- [ ] `Documentation/Planning/Decisions/ADR-<next free number>-<slug>.md` exists (number checked unused), Status Accepted, marked "Decided by Claude under Stewart's delegation", with alternatives weighed, stating where the upgrade request and reply code live, which options apply, the tool's post-upgrade behaviour, the randomness seam, and any Abstractions change (so a task can be filed for it before BL-580 if needed).
- [ ] Consequences list BL-580 to BL-584.
- [ ] `Documentation/Planning/Decisions/README.md` indexes the new ADR.

## Notes

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
