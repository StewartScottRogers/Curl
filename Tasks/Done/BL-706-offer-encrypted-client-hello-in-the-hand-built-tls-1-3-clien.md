---
id: BL-706
title: Offer Encrypted Client Hello in the hand-built TLS 1.3 client
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-699, BL-677]
touches: [Curl.Tls.UnitLibrary, Curl.Tls.UnitTests, Documentation/Planning/Decisions]
requirement: none
created: 2026-09-28
completed: 2026-09-29
---
# BL-706 — Offer Encrypted Client Hello in the hand-built TLS 1.3 client

## Goal

The hand-built TLS 1.3 client offers Encrypted Client Hello from an ECHConfigList (outer and inner ClientHello, HPKE-sealed payload, `encrypted_client_hello` extension, acceptance confirmation, retry configs on rejection) and sends GREASE ECH when asked, so `--ech` (BL-711) works in every mode curl offers.

## Context

- curl: `--ech` "Specify how to do ECH (Encrypted Client Hello). Supports options including false, grease, true, hard, and configuration values." (https://curl.se/docs/manpage.html, curl 8.23.0, checked 2026-09-28). Standing rule (root `CLAUDE.md`, "Decisions", 2026-09-28).
- Design: BL-695's ADR (which cites the current ECH specification). Builds on BL-699; HPKE from BL-677.
- Test vectors: the ECH specification's test vectors if it has them; otherwise an in-memory ECH-capable server in the tests built from the same pieces with a fixed HPKE key, and the inner/outer ClientHello bytes pinned for fixed randoms.

## Acceptance criteria

- [x] `Curl.Tls.UnitTests` complete an accepted ECH handshake with the in-memory server (the server sees the inner SNI), handle rejection with retry configs and without, and pin GREASE ECH bytes for fixed randoms; a malformed ECHConfigList is a typed failure.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Tls.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- Design decided in ADR-0233 (Decided by Claude under Stewart's delegation): offer to
  `EchConfigList.SupportedConfig`; inner and outer hellos share key shares, suites and
  legacy session ID; the inner hello is sent whole (no `ech_outer_extensions`), TLS 1.3
  alone, padded by RFC 9849 section 6.1.3; confirmation checked in the HelloRetryRequest and
  ServerHello; a rejection verifies the chain for the public name, keeps the retry configs
  and fails with the new `TlsAlertDescription.EchRequired` (121) after the server's
  Finished; GREASE is BoringSSL's shape (random `config_id`, HKDF-SHA256 + AES-128-GCM, a
  real X25519 `enc`, the payload a real one would have with `maximum_name_length` 0).
- RFC 9849 publishes no end-to-end test vectors, so the tests use an in-memory
  client-facing server (`EchTestFrontEnd`, `EchTestConfig` with a fixed X25519 key or a
  P-256 key) in front of `Tls13TestServer` (`ConfirmEch`, `EchRetryConfigs`). The GREASE
  extension is pinned byte for byte from `ReplayTlsRandomSource`; the inner and outer
  hellos themselves are checked field by field (SNI, session ID, versions, padding to 32).
- Sensible default taken: an ECH offer offers no session to resume; filed as BL-954.
- `touches` gained `Documentation/Planning/Decisions` for ADR-0233 and its index row; no
  task in Doing names it.
- Results: 1163 `Curl.Tls.UnitTests` pass (57 new); `Measure-CodeQuality.ps1 -Library
  Curl.Tls.UnitLibrary`: 100% line, 100% branch, 943 members, 0 failing, worst CRAP 10.

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. The hand-built TLS 1.3 client offers ECH to RFC 9849 (accepted, rejected with retry configs, through HRR) and sends GREASE ECH
