---
id: BL-4010
title: OpenSSL build -v prints the handshake message trace (Client hello (1) and its bytes) for a connection that would run on SslStream
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-4009]
touches: [Curl.Networking.UnitLibrary/TlsClientRouting.cs, Curl.Networking.UnitTests/TlsClientRoutingTests.cs, Curl.Networking.UnitLibrary/TlsClientOptions.cs, Curl.Networking.UnitTests/HandBuiltTlsProviderTests.HandshakeMessages.cs]
requirement: none
created: 2026-10-28
completed: 2026-10-28
---
# BL-4010 — OpenSSL build -v prints the handshake message trace (Client hello (1) and its bytes) for a connection that would run on SslStream

## Goal

On Linux and macOS, `curl -v https://...` prints OpenSSL's handshake message trace (`* TLSv1.3 (OUT), TLS handshake, Client hello (1):` then `} [n bytes data]`, through `Finished (20):`) as curl 8.18.0 OpenSSL does (CGR-226).

## Context

Split out of BL-3997. `SslStream` cannot report the encrypted TLS 1.3 handshake messages, so once BL-4009 has the hand-built client report them, decide in an ADR (Decided by Claude under Stewart's delegation) how a connection that today runs on `SslStream` in the OpenSSL build gets the trace: most likely a new ADR-0140 routing row in `TlsClientRouting` sending the OpenSSL build's handshakes to the hand-built client when `-v`/`--trace` asks for the trace. Weigh what `SslStream` gives that the hand-built client lacks (system client-certificate store, revocation) before choosing, and keep `TlsClientRouting.Reason` saying why. Do not fabricate lengths for messages nobody saw.

## Acceptance criteria

- [x] An ADR records how the OpenSSL build's `-v` trace is sourced for connections that would run on `SslStream`.
- [x] A unit test shows such a connection reports a `TlsMessageEvent` for its ClientHello.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Added `Curl.Networking.UnitLibrary/TlsClientOptions.cs` (the new `TracesHandshakeMessages` option the routing row reads) and `Curl.Networking.UnitTests/HandBuiltTlsProviderTests.HandshakeMessages.cs` (the provider test of the ClientHello event, beside BL-4009's) to `touches`; no task in Doing on origin/work/dark-factory names either.
- Decision (ADR-0984): a routing row, not routing every OpenSSL-build connection to the hand-built client, which would change every transfer's TLS stack off Windows at once, unverifiable from a Windows lane. `SslStream`'s client-certificate store is Schannel-only and curl's OpenSSL build checks revocation only through `--crlfile`/`--cert-status`, which the hand-built client already honours, so the OpenSSL build loses nothing on the row. The Schannel build never takes it.
- Setting the option from `-v`/`--trace` in the engine is filed as BL-4045 (Curl.Engine files, outside this task's touches).
- The full fast run had two `Cgr036Tests` failures in Curl.Engine.UnitTests that pass alone and that this change cannot reach (the option defaults to false); filed as BL-4046. Every other project green; Curl.Networking.UnitTests 3842 passed. Measure-CodeQuality for Curl.Networking.UnitLibrary: 0 failing members.

## Log

- 2026-10-28: Created.
- 2026-10-28: Backlog -> Doing. Criteria at claim: ead23c02 e0fbbd90 3d836d5e.
- 2026-10-28: Doing -> Done. In the OpenSSL build a connection whose handshake messages are traced routes to the hand-built client, which reports its ClientHello (ADR-0984)
