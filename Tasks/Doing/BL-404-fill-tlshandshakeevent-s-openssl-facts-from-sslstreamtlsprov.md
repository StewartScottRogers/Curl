---
id: BL-404
title: Report TlsHandshakeEvent, with its OpenSSL facts, from SslStreamTlsProvider
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-356]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests]
requirement: none
created: 2026-09-27
completed:
---
# BL-404 — Report TlsHandshakeEvent, with its OpenSSL facts, from SslStreamTlsProvider

## Goal

After a successful handshake, `SslStreamTlsProvider` reports a `TlsHandshakeEvent` through
the transfer's `ITransferEvents`, filling `PeerCertificateChain` and
`CertificateVerifyResult` (and `NegotiatedGroupName` and `PeerSignatureTypeName` where
.NET exposes them), so `-v` on Linux and macOS prints the OpenSSL build's lines.

## Context

- BL-356 and ADR-0085 added the optional OpenSSL facts to
  `Curl.Protocol.Abstractions.UnitLibrary/TlsHandshakeEvent.cs` and the wording in
  `Curl.Output.UnitLibrary/OpenSslHandshakeText.cs`; nothing reports the event yet
  (`grep ReportTlsHandshake` finds only Output and the sinks).
- The chain is the verified chain (`X509Chain.ChainElements`) when verification passed,
  else the certificates the server sent. `CertificateVerifyResult` is an OpenSSL `X509_V_`
  code: map the `X509ChainStatusFlags` and `SslPolicyErrors` seen (self-signed leaf is
  18, `X509_V_ERR_DEPTH_ZERO_SELF_SIGNED_CERT`; untrusted root 19; expired 10); record the
  mapping in an ADR amendment. Leave a fact `null` rather than guess it.

## Acceptance criteria

- [ ] A test in `Curl.Networking.UnitTests` shows a loopback handshake against a
      self-signed certificate reports a `TlsHandshakeEvent` with that certificate as
      `ServerCertificate` and `PeerCertificateChain[0]`, and `CertificateVerifyResult` 18
      under `-k`.
- [ ] `dotnet build -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"`
      passes; `Measure-CodeQuality.ps1` reports no failing member for `Curl.Networking`.

## Notes

- Filed from BL-356 (2026-09-27).

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
