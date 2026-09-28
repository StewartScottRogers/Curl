---
id: BL-404
title: Report TlsHandshakeEvent, with its OpenSSL facts, from SslStreamTlsProvider
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-356]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Documentation/Planning/Decisions/ADR-0085-v-words-a-tls-handshake-as-the-platforms-curl-build-with-openssl-facts-on-the-event.md]
requirement: none
created: 2026-09-27
completed: 2026-09-27
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

- [x] A test in `Curl.Networking.UnitTests` shows a loopback handshake against a
      self-signed certificate reports a `TlsHandshakeEvent` with that certificate as
      `ServerCertificate` and `PeerCertificateChain[0]`, and `CertificateVerifyResult` 18
      under `-k`.
- [x] `dotnet build -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"`
      passes; `Measure-CodeQuality.ps1` reports no failing member for `Curl.Networking`.

## Notes

- Filed from BL-356 (2026-09-27).
- Added ADR-0085 to `touches`: the task asks for the mapping as an ADR amendment, and no
  task in Doing names that file.
- Design: `ITlsProvider` lives in Abstractions, which BL-373 (Doing) touches, so the
  contract stays as it is. `SslStreamTlsProvider` gains a four-argument overload taking
  `ITransferEvents` (internal interface `IHandshakeReportingTlsProvider`), and
  `TcpConnector` passes `ConnectTarget.Events` to it. The HTTPS proxy handshake is not
  reported (it would print as `Server certificate:`).
- Group name and peer signature type stay `null`: `SslStream` exposes neither. No ALPN is
  offered, so the Schannel wording (ALPN lines only) prints nothing new on Windows.
- `OpenSslVerifyResult` maps 0, 9, 10, 18, 19, 20, else `null`; a date error wins over a
  trust error, read from OpenSSL source (not measurable on this Windows checkout).
- Verified: `dotnet build -warnaserror` clean; fast tests green (Networking 701 passed, 6
  skipped); `Measure-CodeQuality.ps1` shows Curl.Networking 100% line and branch, 0 failing.
  The one failing member it lists, `DiskWriteOutFileOpener.TryOpen` in Curl.Console, is
  outside this task.

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. SslStreamTlsProvider reports TlsHandshakeEvent with the OpenSSL verify code and peer chain through the target's events
