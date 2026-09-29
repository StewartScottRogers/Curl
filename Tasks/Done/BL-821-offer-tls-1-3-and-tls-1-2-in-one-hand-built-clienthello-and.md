---
id: BL-821
title: Offer TLS 1.3 and TLS 1.2 in one hand-built ClientHello and continue on the version the ServerHello picks
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Tls.UnitLibrary, Curl.Tls.UnitTests, Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Documentation/Planning/Decisions/ADR-0200-the-hand-built-tls-client-offers-tls-1-3-and-tls-1-2-in-one-clienthello.md, Documentation/Planning/Decisions/README.md]
requirement: none
created: 2026-09-28
completed: 2026-09-29
---
# BL-821 — Offer TLS 1.3 and TLS 1.2 in one hand-built ClientHello and continue on the version the ServerHello picks

## Goal

A hand-built TLS connection whose range spans TLS 1.3 and TLS 1.2 (or lower) sends one ClientHello offering every version in the range, and completes the handshake as TLS 1.3 or as TLS 1.2 by what the ServerHello selects, the `TlsClientConnection` ADR-0140's class structure names; `HandBuiltTlsProvider` uses it in place of choosing `Tls13ClientConnection` or `Tls12ClientConnection` from the range alone.

## Context

- ADR-0162 decision 3 (BL-708): today a range reaching TLS 1.3 runs `Tls13ClientConnection` only, so a hand-built route with `--tlsv1.2` and no ceiling offers only TLS 1.3 and cannot talk to a TLS 1.2 server. No routing row sends such a range there yet, but BL-618's rows (`--curves`, `--sigalgs` and the rest) will.
- ADR-0140, "Class structure": `TlsClientConnection.ConnectAsync(Stream, TlsClientSettings, IServerCertificateVerifier, CancellationToken)` sends the hello and picks the TLS 1.3 or 1.2 path from the ServerHello. RFC 8446 section 4.1.3's downgrade sentinels must be checked when TLS 1.2 is chosen with TLS 1.3 offered.
- Code: `Curl.Tls.UnitLibrary/Tls13ClientConnection.cs`, `Tls12ClientConnection.cs`, `Tls13ClientHandshake.cs`, `Tls12ClientHandshake.cs`; `Curl.Networking.UnitLibrary/HandBuiltTlsProvider.cs` (`HandshakeAsync`, `OffersTls13`).

## Acceptance criteria

- [x] `Curl.Tls.UnitTests` complete a handshake with one ClientHello offering TLS 1.3 and TLS 1.2 against `Tls13RecordTestServer` (continuing as TLS 1.3) and against `Tls12RecordTestServer` (continuing as TLS 1.2), and a TLS 1.2 ServerHello carrying the TLS 1.3 downgrade sentinel fails with `illegal_parameter`.
- [x] `Curl.Networking.UnitTests` show `HandBuiltTlsProvider` with `MinimumVersion` TLS 1.2 and no ceiling completing against a TLS 1.2-only server-side `SslStream`.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Tls.UnitLibrary` and `Curl.Networking.UnitLibrary`.

## Notes

- Filed by BL-708 (ADR-0162).
- 2026-09-29, design (ADR-0200, decided by Claude under Stewart's delegation): the TLS 1.3
  client builds the one hello, with the TLS 1.2 half added through the internal
  `Tls13ClientSettings.LowerVersions`; `TlsClientConnection` reads the first server message
  through `ServerHelloReplayStream` and replays it to whichever record layer the ServerHello
  picks; `Tls12ClientHandshake.StartFrom(sent)` adopts the sent hello and refuses either
  downgrade sentinel. `TlsClientSettings` pairs the two settings records rather than inventing
  a third shape of options.
- Default taken: beside TLS 1.3 an unset minimum offers TLS 1.2, curl's default minimum since
  8.10.0; ADR-0162's TLS 1.0 floor under a lower ceiling is unchanged. When the cipher options
  leave no runnable suite for one side, the other side runs alone.
- Touches widened to the ADR and `Documentation/Planning/Decisions/README.md` for ADR-0200; no
  task in Doing names them.
- Also fixed on the way, inside the touched projects: `Tls13ClientSettings.Validate` (complexity
  12 since BL-786) split in two for the quality gate; the Networking fake TLS 1.3 server now
  throws `TlsAlertException` for a client alert, so the `--cert-status` tests' expected refusal
  is not an assertion failure (they failed on HEAD before this task too);
  `WithCiphersTheOpenSslBuildCannotOffer_FailsWithExit59`'s TLS 1.3 CCM row now sets
  `--tlsv1.3`, since the default range now also offers TLS 1.2.
- Verified: `dotnet build Curl.slnx -warnaserror` clean; fast tests green across the solution;
  `Measure-CodeQuality.ps1` 100% line and branch, 0 failing members, for both libraries.
  Tests: `TlsClientConnectionTests` (TLS 1.3 and TLS 1.2 continuations, both sentinels, the
  hello's contents), `ServerHelloReplayStreamTests`,
  `HandBuiltTlsProviderTests.AuthenticateAsClientAsync_Tls12MinimumWithNoCeilingAgainstATls12OnlyServer_ConnectsOverTls12`.

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. One hand-built ClientHello offers TLS 1.3 and TLS 1.2 and continues on the version the ServerHello picks; --tlsv1.2 with no ceiling reaches a TLS 1.2-only server
