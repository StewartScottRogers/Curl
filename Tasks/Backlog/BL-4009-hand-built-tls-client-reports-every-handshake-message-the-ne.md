---
id: BL-4009
title: Hand-built TLS client reports every handshake message, the negotiated group and the peer signature type (OpenSSL -v trace source)
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Tls.UnitLibrary/Tls13ClientHandshake.cs, Curl.Tls.UnitLibrary/Tls12ClientHandshake.cs, Curl.Tls.UnitLibrary/TlsClientConnection.cs, Curl.Networking.UnitLibrary/HandBuiltTlsProvider.cs, Curl.Networking.UnitLibrary/HandBuiltHandshake.cs]
requirement: none
created: 2026-10-28
completed:
---
# BL-4009 — Hand-built TLS client reports every handshake message, the negotiated group and the peer signature type (OpenSSL -v trace source)

## Goal

A handshake run by the hand-built TLS client (`HandBuiltTlsProvider`) reports, in the OpenSSL build, one `TlsMessageEvent` per handshake message sent and received (plaintext and the decrypted TLS 1.3 ones), and sets `TlsHandshakeEvent.NegotiatedGroupName` and `PeerSignatureTypeName` to OpenSSL's names.

## Context

Split out of BL-3997 (CGR-226, BL-3755). `SslStream` hides every TLS 1.3 handshake message after ServerHello, so OpenSSL's message-callback trace (`TLSv1.3 (IN), TLS handshake, Encrypted Extensions (8):` and its `{ [n bytes data]`) can only come from a client that sees them: the hand-built one in `Curl.Tls.UnitLibrary`. Today it reports only `NewSessionTicket` (`HandBuiltTlsConnection.cs:81`). `Curl.Output.UnitLibrary/OpenSslMessageText.cs` already words `TlsMessageEvent`s. Names: group as `SSL_get0_group_name` (`x25519`, `secp256r1`, `X25519MLKEM768`); signature as `OBJ_nid2sn(SSL_get_peer_signature_type_nid)` (`RSASSA-PSS`, `rsaEncryption`, `id-ecPublicKey`, `ED25519`). Measure curl 8.18+ OpenSSL with `Record-CurlExchange.ps1` before pinning text. The handshake classes should take an observer callback rather than know about `ITransferEvents` (Curl.Tls does not reference the abstractions; check that before deciding).

## Acceptance criteria

- [ ] A unit test pins the `TlsMessageEvent`s a hand-built TLS 1.3 handshake reports, in order, with the bytes of each message.
- [ ] A unit test pins the `TlsMessageEvent`s a hand-built TLS 1.2 handshake reports.
- [ ] The hand-built path's `TlsHandshakeEvent` carries the negotiated group and peer signature type names.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-10-28: Created.
