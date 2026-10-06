---
id: BL-708
title: Offer the hand-built TLS client through ITlsProvider as the TLS-options ADR routes it
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-700, BL-703, BL-617, BL-815]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Console, Curl.Console.UnitTests, Documentation/Planning/Decisions]
requirement: none
created: 2026-09-28
completed: 2026-09-28
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

- [x] `Curl.Networking.UnitTests` complete an https exchange through the new provider against the in-memory TLS 1.3 and TLS 1.2 servers from `Curl.Tls.UnitTests` (or equivalent test servers here), and pin that a self-signed chain fails with the same exit and message as through `SslStreamTlsProvider`, per platform with `OSCondition` where messages differ.
- [x] The routing function's tests show `SslStream` chosen for every plain option set and the hand-built client for each case the ADRs list.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

- 2026-09-28 (lane 1): returned to Backlog before any code. `Curl.Tls.UnitLibrary` has the TLS 1.2/1.1/1.0 handshake (`Tls12ClientHandshake`, BL-703) and record states (BL-702) but nothing that runs them over a byte stream; only TLS 1.3 has a connection and stream (`Tls13ClientConnection`, `Tls13ClientStream`, BL-700). The first criterion (a TLS 1.2 exchange through the new provider) and the legacy-versions row (which routes to TLS 1.0/1.1) both need that, and it belongs in `Curl.Tls.UnitLibrary` with its own tests, so it is filed as BL-815 and added to `depends-on`.
- Routing rows: `TlsClientOptions` today carries only the legacy-versions condition (`MaximumVersion` TLS 1.0 or 1.1); the other rows' options reach it with BL-618 (`--curves`, `--sigalgs`, `--tls-earlydata`, `--ech`, `--ssl-sessions`, `--tlsuser`/`--tlspassword`), BL-713 (`--no-sessionid`, `--ssl-allow-beast`) and BL-610 (`--cert-status`). As this task's Context says, those tasks add their rows; this task builds the function with the rows whose options exist when it runs.
- 2026-09-28 (lane 3), plan and outcome (ADR-0162):
  - `TlsClientRouting.Choose` (pure, `TlsClientRoute`) with the legacy-versions row only; `CurlComposition.CreateTlsProvider` builds `HandBuiltTlsProvider` or `SslStreamTlsProvider` for the origin and the HTTPS proxy; both implement the new `ITlsProviderWithWarnings`, which `CurlTransports` now holds.
  - The verification moved out of `SslStreamTlsProvider` into `ServerCertificateVerification` (with `PeerVerification`); `HandBuiltCertificateVerifier` builds the chain and `SslPolicyErrors` `SslStream` would and calls it. `--cert` loading moved to `ClientCertificateLoader.Load` for both.
  - Test servers: the criterion allows equivalents, and the `Curl.Tls.UnitTests` servers are internal to that project (reaching them would mean `InternalsVisibleTo` in `Curl.Tls.UnitLibrary`, outside `touches`). A server-side `SslStream` over `Fakes/InMemoryDuplexStream` is used instead, the same server the `SslStream` provider's tests use, which also tests interop with a real TLS stack: TLS 1.2 on every platform, TLS 1.3 excluded on macOS (`OSCondition`; no TLS 1.3 server there). The self-signed and name-mismatch failures are compared with `SslStreamTlsProvider`'s result against the same server in both builds, so no per-platform text is pinned twice.
  - Sensible defaults taken: a range reaching TLS 1.3 runs the TLS 1.3 client only (one hello for both is BL-821); `server_name` left out for IP literals; `Curl.Tls`'s default suites and groups until the profiles (BL-787, then BL-820); OpenSSL alert error strings from `ssl_err.c` where not measured.
  - The Context's exit 56 without `close_notify` is not wired: the `SslStream` path lacks it too and the HTTP handler has no typed failure to print curl's text, so both paths go to BL-819 (ADR-0162 decision 7).
  - `touches` gained `Documentation/Planning/Decisions` for ADR-0162 and its index row; no task in Doing names it.
  - Quality: `Curl.Networking.UnitLibrary` 100% line, 100% branch, 475 members, 0 failing (worst CRAP 10); `Curl.Console` 100%/100%, 0 failing.
  - Filed: BL-821, BL-819, BL-820.

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Backlog. Waits on BL-815: no TLS 1.2/1.1/1.0 connection over a byte stream exists in Curl.Tls.UnitLibrary yet
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Done. A --tls-max of TLS 1.0 or 1.1 now runs the hand-built TLS client through HandBuiltTlsProvider, with the SslStream provider's verifier, events and failure text; TlsClientRouting.Choose picks the provider
