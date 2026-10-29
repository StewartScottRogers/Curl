---
id: BL-4252
title: Extension CoAP: make coaps:// requests over DTLS 1.2
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-4251, BL-4248]
touches: [Curl.Extension.Coap.UnitLibrary/Client, Curl.Extension.Coap.UnitTests/Client, Curl.Extension.Coap.UnitLibrary/Secure, Curl.Extension.Coap.UnitTests/Secure]
requirement: none
created: 2026-10-29
completed:
---
# BL-4252 — Extension CoAP: make coaps:// requests over DTLS 1.2

## Goal

`curl coaps://host/path` makes every request `coap://` can (methods, block-wise, Observe) over DTLS 1.2 on port 5684, by RFC 7252 section 9.1.

## Context

Part of BL-4116 (extension 15 of 63, split for size). Extension rules: ADR-0999 and the lane rules "Extensions": it references only `Curl.Protocol.Abstractions`, the shared layers (`Curl.Tls`, `Curl.Cryptography`, `Curl.Networking`) and `Curl.Extension.Abstractions`, never a `Curl.Protocol.*` library or another extension, is tested against the RFCs, not curl, and never changes what curl does. Held to the four quality gates.

Builds on BL-4251's DTLS client and BL-4248's complete `coap://` client: run the same client flow over a DTLS-protected channel. RFC 7252 section 9.1 (DTLS-secured CoAP, `coaps` default port 5684, certificate mode), 9.1.3.3 (X.509 certificates). Update the ADR from BL-4246 to record DTLS 1.2 (RFC 6347) and what is left out (pre-shared keys, raw public keys) and why.

## Acceptance criteria

- [ ] Unit tests over a fake `IDatagramConnector` and BL-4251's in-memory DTLS server make a GET and a block-wise transfer over `coaps://`, named after RFC 7252 section 9.1.
- [ ] An untrusted server certificate fails with exit 60 and `-k` succeeds; unit tests.
- [ ] With `CURL_EXTENSIONS=0` a `coaps://` URL fails exactly as curl fails an unsupported scheme (exit 1, same stderr bytes); unit test.
- [ ] The ADR records DTLS 1.2 (RFC 6347) and what is left out with why.
- [ ] The changed libraries meet the four quality gates by `Measure-CodeQuality.ps1`.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: 2db89dcf dd0657e0 13785854 da731085 478f867b 3d836d5e.
