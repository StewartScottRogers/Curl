---
id: BL-4252
title: Extension CoAP: make coaps:// requests over DTLS 1.2
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-4251, BL-4248, BL-4282]
touches: [Curl.Extension.Coap.UnitLibrary/Client, Curl.Extension.Coap.UnitTests/Client, Curl.Extension.Coap.UnitLibrary/Secure, Curl.Extension.Coap.UnitTests/Secure, Curl.Extension.Coap.UnitTests/Dtls/TestServer/DtlsTestServer.cs, Curl.Extension.Coap.UnitLibrary/Observe/CoapObserveTransferHook.cs, Curl.Extension.Coap.UnitTests/Observe/CoapObserveTransferHookTests.cs, Curl.Extension.Coap.UnitTests/Observe/CoapObservationTests.cs, Curl.Extension.Coap.UnitTests/Blockwise/CoapBlockwiseTransferTests.cs, Curl.Extension.Coap.UnitLibrary/CLAUDE.md, Documentation/Planning/Decisions/ADR-1047-the-coap-extension-follows-rfc-7252-and-maps-error-codes-as-http-does.md]
requirement: none
created: 2026-10-29
completed: 2026-10-29
---
# BL-4252 — Extension CoAP: make coaps:// requests over DTLS 1.2

## Goal

`curl coaps://host/path` makes every request `coap://` can (methods, block-wise, Observe) over DTLS 1.2 on port 5684, by RFC 7252 section 9.1.

## Context

Part of BL-4116 (extension 15 of 63, split for size). Extension rules: ADR-0999 and the lane rules "Extensions": it references only `Curl.Protocol.Abstractions`, the shared layers (`Curl.Tls`, `Curl.Cryptography`, `Curl.Networking`) and `Curl.Extension.Abstractions`, never a `Curl.Protocol.*` library or another extension, is tested against the RFCs, not curl, and never changes what curl does. Held to the four quality gates.

Builds on BL-4251's DTLS client and BL-4248's complete `coap://` client: run the same client flow over a DTLS-protected channel. RFC 7252 section 9.1 (DTLS-secured CoAP, `coaps` default port 5684, certificate mode), 9.1.3.3 (X.509 certificates). Update the ADR from BL-4246 to record DTLS 1.2 (RFC 6347) and what is left out (pre-shared keys, raw public keys) and why.

## Acceptance criteria

- [x] Unit tests over a fake `IDatagramConnector` and BL-4251's in-memory DTLS server make a GET and a block-wise transfer over `coaps://`, named after RFC 7252 section 9.1.
- [x] An untrusted server certificate fails with exit 60 and `-k` succeeds; unit tests.
- [x] With `CURL_EXTENSIONS=0` a `coaps://` URL fails exactly as curl fails an unsupported scheme (exit 1, same stderr bytes); unit test.
- [x] The ADR records DTLS 1.2 (RFC 6347) and what is left out with why.
- [x] The changed libraries meet the four quality gates by `Measure-CodeQuality.ps1`.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- 2026-10-29 (lane 1, first pass, no code edited): the GET and block-wise tests need `Dtls/TestServer/DtlsTestServer.cs` to answer application data with a CoAP response; today it only echoes the plaintext back (an echoed CON GET is not a response, and the test channel cannot decrypt it without the server's keys). Add a settable `Func<byte[], byte[]>` answering application data there (default: echo). BL-4282 (Doing) holds that file, so it is added to `touches` and `depends-on`. Plan for the next claim: a `Secure/` `IDatagramChannel` over `DtlsClientSession` (runs `DtlsClientHandshake` with `ProtocolHandlerServices.TlsProvider`, then carries each CoAP datagram as one application data record), so `CoapExchange`, `CoapBlockwiseTransfer` and `CoapObservation` run unchanged; `CoapProtocolHandler` serves `coap` and `coaps`, port 5684 for `coaps`; the description lists both schemes; ADR-1042 or ADR-1078 gets the DTLS 1.2 and left-out (PSK, raw public keys) note.
- 2026-10-29 (lane 12, second claim): `Secure/DtlsDatagramChannel` runs `DtlsClientHandshake` over the UDP channel and carries each CoAP datagram as one application data record; `CoapProtocolHandler` serves `coap` and `coaps` (port 5684), the secure path in its own method so each `await using` is covered. Added to `touches` (no other Doing task names them): `Observe/CoapObserveTransferHook.cs` and its tests (`--coap-observe` marks `coaps://` too), `CoapObservationTests.cs` and `CoapBlockwiseTransferTests.cs` (the handler's new `ITlsProvider` constructor argument), the library's `CLAUDE.md`, and ADR-1047, which now records DTLS 1.2 (RFC 6347) and leaves out PSK, raw public keys, DTLS 1.0/1.3 and resumption, with why. Measure-CodeQuality: Curl.Extension.Coap.UnitLibrary 100% line, 100% branch, 0 failing. CI on Linux and macOS: the new tests use only in-memory channels and generated certificates, no paths or OS text, so they are platform-neutral.

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: 2db89dcf dd0657e0 13785854 da731085 478f867b 3d836d5e.
- 2026-10-29: Doing -> Backlog. Waits on BL-4282, which holds Curl.Extension.Coap.UnitTests/Dtls/TestServer/DtlsTestServer.cs; the coaps tests need it to answer application data with a CoAP response
- 2026-10-29: Backlog -> Doing. Criteria at claim: 2db89dcf dd0657e0 13785854 da731085 478f867b 3d836d5e.
- 2026-10-29: Doing -> Done. coaps:// makes GET, block-wise and Observe requests over DTLS 1.2 on port 5684, exit 60 for an untrusted certificate, -k accepts
