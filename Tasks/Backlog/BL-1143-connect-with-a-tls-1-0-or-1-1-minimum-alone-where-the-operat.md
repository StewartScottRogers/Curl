---
id: BL-1143
title: Connect with a TLS 1.0 or 1.1 minimum alone where the operating system's TLS stack refuses those versions
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-714]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests]
requirement: none
created: 2026-10-01
completed:
---
# BL-1143 — Connect with a TLS 1.0 or 1.1 minimum alone where the operating system's TLS stack refuses those versions

## Goal

`--tlsv1.0` or `--tlsv1.1` with no `--tls-max` completes a transfer against a server that speaks only TLS 1.0 or 1.1 on every platform, including where `SslStream`'s operating-system stack (OpenSSL 3 at its default security level, a Schannel with TLS 1.0 and 1.1 disabled) refuses them, while the same options against a modern server keep negotiating TLS 1.2 or 1.3 as they do today.

## Context

- ADR-0331 (BL-714): a TLS 1.0 or 1.1 ceiling already runs on the hand-built client and connects everywhere, as curl 8.21.0 with Schannel does (measured exit 0 for `--tlsv1.0` and `--tlsv1.1` alone against `openssl s_server -tls1` / `-tls1_1`). A minimum alone stays on `SslStream` (`TlsClientRouting`, `TlsClientRoutingTests.Choose_WithATls10MinimumAlone_IsSslStream`).
- Choose between routing every TLS 1.0/1.1 minimum to the hand-built client (changes the ClientHello for every such transfer to a modern server) and retrying on the hand-built client after `SslStream` fails with a protocol-version refusal; record the choice in an ADR.
- `LegacyTlsTestServer` in `Curl.Networking.UnitTests/Fakes` is the in-memory TLS 1.0/1.1 server to test against.

## Acceptance criteria

- [ ] A test in `Curl.Networking.UnitTests` completes a transfer with `MinimumVersion: Tls10` (and `Tls11`) and no ceiling against `LegacyTlsTestServer`, as both builds, with no operating-system TLS stack involved.
- [ ] A test shows the same options against a TLS 1.2-or-later server still negotiating TLS 1.2 or later.
- [ ] An ADR records the routing choice; `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-10-01: Created.
