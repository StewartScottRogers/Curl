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
completed: 2026-09-26
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

- [x] A new ADR under `Documentation/Planning/Decisions/` with the next free number, status Accepted, titled for the transfer event sink, states that it was decided by Claude under Stewart's delegation, and records the decision, the reasons and the alternatives rejected.
- [x] `Documentation/Planning/Decisions/README.md` lists the new ADR.
- [x] The ADR lists the event kinds (connection opened/reused, TLS handshake details, request header, response header, data sent, data received, info text) with their payloads, and states the relation to BL-133's progress sink.
- [x] The ADR states that the default sink does nothing, so handlers and tests that ignore it are unaffected.

## Notes

- Plan item: X7 in the Phase 1 HTTP plan (2026-09-26); plan keys in this file were replaced by their task IDs.
- Recorded ADR-0046 (next free number after ADR-0045). Measured curl 8.21.0 (mingw, Schannel) `-v`, `--trace` and `--trace-ascii` against `python -m http.server` on 127.0.0.1:18163, plus `-v https://example.com/`, `-v -d hi` and a refused connect; the commands and bytes are in the ADR's Context. The measurements fixed the event boundaries: the request head is one header-out event, each response header line its own header-in event.
- Decision: a sibling `ITransferEvents` on `ITransferContext.Events`, not an extension of ADR-0045's `ITransferProgress` (different consumers and options; counts are idempotent, bytes are not). Connection and TLS events are structured records rendered by `Curl.Output`; everything else is `ReportInfo` text. The connector reports through a new `ConnectTarget.Events`, because `Trying` and connect-failure lines happen inside the connect.
- Filed BL-311 (add the sink to `Curl.Protocol.Abstractions`) and added it to BL-228's and BL-229's `depends-on`, since the formatters need its types. Carrying `Events` across redirect hops is noted in the ADR beside BL-310.
- Pipeline `docs` was worked directly rather than through `align-and-document`: the whole deliverable is one ADR and an index row, and no `.cs` or project file changed, so the `verify` skill is not required.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Done. ADR-0046 decides the transfer event sink (ITransferEvents on ITransferContext.Events and ConnectTarget.Events) that -v and --trace render from
