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
completed: 2026-09-28
---
# BL-639 — Decide how DNS-over-HTTPS resolves names without referencing Curl.Protocol.Http

## Goal

An ADR fixes how a DoH resolver in `Curl.Networking.UnitLibrary` sends RFC 8484 queries over HTTPS (it cannot reference the HTTP protocol library, and `HttpClient` is barred from protocol code), which queries curl 8.21.0 sends (A and AAAA, in parallel or in turn, POST with `application/dns-message`), which TLS options apply (`--doh-insecure`, `--doh-cert-status`, and whether the transfer's own `-k` does not), and how failures map to exits 6 and others.

## Context

- Conformance audit 2026-09-28, row 27 (Major, L; split: this decision, BL-640 DNS messages, BL-641 resolver, BL-642 options and wiring).
- `IDnsResolver` lives in Abstractions and is implemented by `SystemDnsResolver`; the TCP connector and `ITlsProvider` are in Networking, so a minimal HTTP/1.1 POST writer and response reader inside Networking is one option, and a resolver in `Curl.Console` composing the HTTP handler is another; weigh them.
- Measure with `Record-CurlExchange.ps1 -Tls -k` acting as the DoH server (capture the POST curl sends for `--doh-url https://127.0.0.1:<P>/dns-query --doh-insecure http://example.test/`), and a DoH server that answers `500`.

## Acceptance criteria

- [x] The measurements are recorded in the ADR's Context.
- [x] `Documentation/Planning/Decisions/ADR-<next free number>-<slug>.md` exists (number checked unused), Status Accepted, marked "Decided by Claude under Stewart's delegation", stating where the DoH HTTP exchange lives, the query sequence, the TLS options, and the failure mapping.
- [x] Consequences list BL-640 to BL-642.
- [x] `Documentation/Planning/Decisions/README.md` indexes the new ADR.

## Notes

- Decided in ADR-0152 (number checked free across every local branch, remote ref and lane worktree): `DohDnsResolver` in `Curl.Networking.UnitLibrary` does its own minimal HTTP/1.1 POST, beside `HttpProxyTunnel`, over its own `TcpConnector` and a TLS provider built from the DoH options only.
- Measured curl 8.21.0 (Schannel) with `Record-CurlExchange.ps1 -Tls -Connections 2` on 2026-09-28: A then AAAA POSTed in parallel on two connections, five header lines, no `User-Agent`, 30-byte query ID 0 flags 0x0100; `-k` does not reach DoH; every DoH failure (500, bad cert, refused, NXDOMAIN, truncated) ends exit 6 `Could not resolve host: example.test`; plain `-v` shows no DoH lines. Full table in the ADR's Context.
- Pipeline `docs`: the ADR was written in-session rather than by `align-and-document`, because the work was the measurement; no `.cs` or project file changed.
- Left to BL-642 (not measured here): `--resolve` with `--doh-url`, a bogus DoH URL, and whether `-4`/`-6` stop the other query being sent.

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Done. ADR-0152 decides DoH: minimal HTTP/1.1 POST in Networking, A+AAAA in parallel, only --doh-insecure/--doh-cert-status reach DoH TLS, every failure exit 6; measured on curl 8.21.0
