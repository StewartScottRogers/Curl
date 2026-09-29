---
id: BL-641
title: Resolve names through a DNS-over-HTTPS server
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-640]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Documentation/Planning/Decisions/ADR-0152-dns-over-https-posts-a-and-aaaa-in-parallel-over-a-minimal-http-1-1-exchange-inside-networking.md]
requirement: none
created: 2026-09-28
completed: 2026-09-28
---
# BL-641 — Resolve names through a DNS-over-HTTPS server

## Goal

A `DohDnsResolver` implementing `IDnsResolver` resolves a host by POSTing BL-640's queries to the DoH URL over the connector and TLS provider as BL-639's ADR decides, and returns the addresses (or curl 8.21.0's failure, exit 6 with its message) without ever using the system resolver for the target host.

## Context

- Conformance audit 2026-09-28, row 27. Design: BL-639's ADR; codec: BL-640.
- The DoH server's own host name is resolved with the system resolver (as curl does); inject both so tests need no network.

## Acceptance criteria

- [x] `Curl.Networking.UnitTests` with fake connector and TLS provider pin the HTTP request bytes BL-639 recorded, the addresses returned for A and AAAA answers, and exit 6 with the measured message for a `500`, an NXDOMAIN and a malformed answer.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Networking.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- Built in-session: ADR-0152 is the plan. New types: `DohDnsResolver` (public, `IDnsResolver`, takes an `IConnector` and the DoH URL) and `DohResponseReader` (internal). `TcpConnector.IsLocalhost` became internal so the resolver shares it. `Fakes/FakeConnector` gained `BytesToRead` and `ReadException`.
- Measured before pinning (2026-09-28, curl 8.21.0 Schannel, `Record-CurlExchange.ps1 -Tls`): curl decodes the body whatever the status or `Content-Type` (a `500`, a `text/plain` and a Content-Type-less answer carrying a valid A answer all resolved), reads chunked bodies, and fails a close-delimited body with `DoH request Failure when receiving data from the peer`; IP literals, `localhost` and `*.localhost` never reach the DoH server. This contradicted ADR-0152 point 4's unmeasured guess about status and `Content-Type`, so ADR-0152 gained a "BL-641 amendment" (decided by Claude under Stewart's delegation). ADR-0152's file was added to `touches` for that; no task in Doing names it.
- Defaults taken from curl's `lib/doh.c`: a DoH body over 3000 bytes (`DYN_DOH_RESPONSE`) fails; response lines are capped at 100 KB (`CURL_MAX_HTTP_HEADER`). Interim `1xx` responses are not skipped (unmeasured; a DoH server does not send them to a POST without `Expect`).
- The DoH connection's target carries `PoolScheme` = the URL's scheme, so `TcpConnector` offers ALPN `http/1.1` as measured; the connections are never marked reusable, so each closes.
- `--trace-config doh` lines are not written yet: filed as BL-848.
- Tests: `DohDnsResolverTests` (26 incl. rows), `DohResponseReaderTests` (30 incl. rows). `Measure-CodeQuality.ps1 -Library Curl.Networking.UnitLibrary`: 100% line, 100% branch, 0 failing members, worst CRAP 10. Full fast run green.

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Done. DohDnsResolver POSTs curl's A and AAAA DoH queries in parallel over an injected connector and returns their addresses, or none so the transfer fails with exit 6
