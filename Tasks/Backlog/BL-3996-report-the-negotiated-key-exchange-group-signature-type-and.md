---
id: BL-3996
title: Report the negotiated key-exchange group, signature type and handshake messages from the Linux SslStream TLS path (CGR-226 source)
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/SslStreamTlsProvider.cs, Curl.Networking.UnitTests/SslStreamTlsProviderTests.HandshakeEvent.cs]
requirement: none
created: 2026-10-28
completed:
---
# BL-3996 — Report the negotiated key-exchange group, signature type and handshake messages from the Linux SslStream TLS path (CGR-226 source)

## Goal

<!-- One sentence: the observable outcome once this task is done. -->

## Context

<!-- Why this matters, and where to start: requirement IDs, ADRs, projects, files, upstream curl links with the curl version. -->

## Acceptance criteria

- [ ] <!-- A statement someone else can check from the repository without asking a question. -->

## Notes

## Log

- 2026-10-28: Created.

## Goal

On Linux, `-v` over the SslStream path prints `SSL connection using TLSv1.3 / TLS_AES_256_GCM_SHA384 / X25519MLKEM768 / RSASSA-PSS` and the OpenSSL handshake message trace (`TLSv1.3 (OUT), TLS handshake, Client hello (1):` and `} [n bytes data]`), as curl 8.18.0 OpenSSL does (CGR-226, BL-3755).

## Context

`OpenSslHandshakeText.ConnectionLine` already prints `TlsHandshakeEvent.NegotiatedGroupName` and `PeerSignatureTypeName`, but `SslStreamTlsProvider.DescribeHandshake` never sets them (the test pins them null), so Linux prints `[blank] / UNDEF`. SslStream does not expose them; the source must be the hand-built TLS stack (`HandBuiltTlsConnection`) or another route. Decide and record an ADR.

## Acceptance criteria

- [ ] Group and signature names are reported for a Linux handshake.
- [ ] Handshake message trace lines appear under `-v`.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

Filed by BL-3755, which depends on this.
