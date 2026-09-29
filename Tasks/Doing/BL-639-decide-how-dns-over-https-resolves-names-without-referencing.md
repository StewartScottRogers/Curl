---
id: BL-639
title: Decide how DNS-over-HTTPS resolves names without referencing Curl.Protocol.Http
priority: Normal
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Planning/Decisions]
requirement: none
created: 2026-09-28
completed:
---
# BL-639 — Decide how DNS-over-HTTPS resolves names without referencing Curl.Protocol.Http

## Goal

An ADR fixes how a DoH resolver in `Curl.Networking.UnitLibrary` sends RFC 8484 queries over HTTPS (it cannot reference the HTTP protocol library, and `HttpClient` is barred from protocol code), which queries curl 8.21.0 sends (A and AAAA, in parallel or in turn, POST with `application/dns-message`), which TLS options apply (`--doh-insecure`, `--doh-cert-status`, and whether the transfer's own `-k` does not), and how failures map to exits 6 and others.

## Context

- Conformance audit 2026-09-28, row 27 (Major, L; split: this decision, BL-640 DNS messages, BL-641 resolver, BL-642 options and wiring).
- `IDnsResolver` lives in Abstractions and is implemented by `SystemDnsResolver`; the TCP connector and `ITlsProvider` are in Networking, so a minimal HTTP/1.1 POST writer and response reader inside Networking is one option, and a resolver in `Curl.Console` composing the HTTP handler is another; weigh them.
- Measure with `Record-CurlExchange.ps1 -Tls -k` acting as the DoH server (capture the POST curl sends for `--doh-url https://127.0.0.1:<P>/dns-query --doh-insecure http://example.test/`), and a DoH server that answers `500`.

## Acceptance criteria

- [ ] The measurements are recorded in the ADR's Context.
- [ ] `Documentation/Planning/Decisions/ADR-<next free number>-<slug>.md` exists (number checked unused), Status Accepted, marked "Decided by Claude under Stewart's delegation", stating where the DoH HTTP exchange lives, the query sequence, the TLS options, and the failure mapping.
- [ ] Consequences list BL-640 to BL-642.
- [ ] `Documentation/Planning/Decisions/README.md` indexes the new ADR.

## Notes

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
