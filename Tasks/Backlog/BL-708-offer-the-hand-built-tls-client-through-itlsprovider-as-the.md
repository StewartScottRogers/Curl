---
id: BL-708
title: Offer the hand-built TLS client through ITlsProvider as the TLS-options ADR routes it
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-700, BL-703, BL-617, BL-815]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-708 — Offer the hand-built TLS client through ITlsProvider as the TLS-options ADR routes it

## Goal

A second `ITlsProvider` in `Curl.Networking.UnitLibrary` runs the hand-built TLS client (TLS 1.3 records from BL-700, TLS 1.2/1.1/1.0 from BL-703) over the TCP connection, verifies the chain with the same code `SslStreamTlsProvider` uses, reports the same `-v` lines and failures (exits 35, 60 and the rest) as the `SslStream` path, and `Curl.Console` picks it for a transfer exactly when BL-617's and BL-695's ADRs say.

## Context

- Standing rule (root `CLAUDE.md`, "Decisions", 2026-09-28). Code: `Curl.Networking.UnitLibrary/SslStreamTlsProvider.cs`, `ITlsProvider`, `TlsClientOptions.cs`, `TlsFailureMessages.cs`, `OpenSslVerifyResult.cs`; composition in `Curl.Console/CurlTransports.cs` and `TlsClientOptionsMapping.cs`. `Curl.Networking.UnitLibrary` gains a reference to `Curl.Tls.UnitLibrary` (allowed: it is not a protocol library).
- The routing rule is the ADRs'; this task implements it as one pure function with data-row tests, so later option tasks (BL-709 to BL-714) only add rows.
- HTTPS proxies (ADR-0095) route the same way with their own options.
- ADR-0157 (BL-700): `Tls13ClientStream` returns 0 at a transport end without `close_notify`, as `SslStream` does; where that leaves a transfer unfinished, the provider fails it with exit 56 and the OpenSSL text in `TlsFailureMessages`, using `CloseNotifyReceived`.

## Acceptance criteria

- [ ] `Curl.Networking.UnitTests` complete an https exchange through the new provider against the in-memory TLS 1.3 and TLS 1.2 servers from `Curl.Tls.UnitTests` (or equivalent test servers here), and pin that a self-signed chain fails with the same exit and message as through `SslStreamTlsProvider`, per platform with `OSCondition` where messages differ.
- [ ] The routing function's tests show `SslStream` chosen for every plain option set and the hand-built client for each case the ADRs list.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

- 2026-09-28 (lane 1): returned to Backlog before any code. `Curl.Tls.UnitLibrary` has the TLS 1.2/1.1/1.0 handshake (`Tls12ClientHandshake`, BL-703) and record states (BL-702) but nothing that runs them over a byte stream; only TLS 1.3 has a connection and stream (`Tls13ClientConnection`, `Tls13ClientStream`, BL-700). The first criterion (a TLS 1.2 exchange through the new provider) and the legacy-versions row (which routes to TLS 1.0/1.1) both need that, and it belongs in `Curl.Tls.UnitLibrary` with its own tests, so it is filed as BL-815 and added to `depends-on`.
- Routing rows: `TlsClientOptions` today carries only the legacy-versions condition (`MaximumVersion` TLS 1.0 or 1.1); the other rows' options reach it with BL-618 (`--curves`, `--sigalgs`, `--tls-earlydata`, `--ech`, `--ssl-sessions`, `--tlsuser`/`--tlspassword`), BL-713 (`--no-sessionid`, `--ssl-allow-beast`) and BL-610 (`--cert-status`). As this task's Context says, those tasks add their rows; this task builds the function with the rows whose options exist when it runs.

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Backlog. Waits on BL-815: no TLS 1.2/1.1/1.0 connection over a byte stream exists in Curl.Tls.UnitLibrary yet
