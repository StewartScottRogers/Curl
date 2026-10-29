---
id: BL-4007
title: OpenSSL build -v prints the handshake message trace (Client hello (1) and its bytes) for a connection that would run on SslStream
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-4006]
touches: [Curl.Networking.UnitLibrary/TlsClientRouting.cs, Curl.Networking.UnitTests/TlsClientRoutingTests.cs]
requirement: none
created: 2026-10-28
completed:
---
# BL-4007 — OpenSSL build -v prints the handshake message trace (Client hello (1) and its bytes) for a connection that would run on SslStream

## Goal

On Linux and macOS, `curl -v https://...` prints OpenSSL's handshake message trace (`* TLSv1.3 (OUT), TLS handshake, Client hello (1):` then `} [n bytes data]`, through `Finished (20):`) as curl 8.18.0 OpenSSL does (CGR-226).

## Context

Split out of BL-3997. `SslStream` cannot report the encrypted TLS 1.3 handshake messages, so once BL-4006 has the hand-built client report them, decide in an ADR (Decided by Claude under Stewart's delegation) how a connection that today runs on `SslStream` in the OpenSSL build gets the trace: most likely a new ADR-0140 routing row in `TlsClientRouting` sending the OpenSSL build's handshakes to the hand-built client when `-v`/`--trace` asks for the trace. Weigh what `SslStream` gives that the hand-built client lacks (system client-certificate store, revocation) before choosing, and keep `TlsClientRouting.Reason` saying why. Do not fabricate lengths for messages nobody saw.

## Acceptance criteria

- [ ] An ADR records how the OpenSSL build's `-v` trace is sourced for connections that would run on `SslStream`.
- [ ] A unit test shows such a connection reports a `TlsMessageEvent` for its ClientHello.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-10-28: Created.
