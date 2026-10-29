---
id: BL-4246
title: Extension CoAP: make coap:// GET, POST, PUT and DELETE requests over UDP
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-4245]
touches: [Curl.Extension.Coap.UnitLibrary/Client, Curl.Extension.Coap.UnitTests/Client, Curl.Extension.Composition.UnitLibrary/ExtensionCatalogue.cs, Curl.Extension.Composition.UnitLibrary/ExtensionDescriptionCatalogue.cs, Documentation/Planning/Decisions/README.md]
requirement: none
created: 2026-10-29
completed:
---
# BL-4246 — Extension CoAP: make coap:// GET, POST, PUT and DELETE requests over UDP

## Goal

`curl coap://host/path` sends a confirmable GET over UDP and writes the response payload; `-X POST|PUT|DELETE` and `-d` make the other methods, as the extension `Curl.Extension.Coap.UnitLibrary`, registered and listed in `curl --extensions`.

## Context

Part of BL-4116 (extension 15 of 63, split for size). Extension rules: ADR-0999 and the lane rules "Extensions": it references only `Curl.Protocol.Abstractions`, the shared layers (`Curl.Tls`, `Curl.Cryptography`, `Curl.Networking`) and `Curl.Extension.Abstractions`, never a `Curl.Protocol.*` library or another extension, is tested against the RFCs, not curl, and never changes what curl does. Held to the four quality gates.

Builds on BL-4245's `Wire` codec. The handler gets `ProtocolHandlerServices.DatagramConnector` and `DnsResolver` (`Curl.Protocol.Abstractions.UnitLibrary/ProtocolHandlerServices.cs`): port 5683 by default, Uri-Host/Uri-Port/Uri-Path/Uri-Query from the URL (RFC 7252 section 6.4), confirmable messages retransmitted by `TimeProvider` (sections 4.2 and 4.8: ACK_TIMEOUT 2 s, ACK_RANDOM_FACTOR 1.5, MAX_RETRANSMIT 4), piggybacked and separate responses (5.2), token matching (5.3). Decide (ADR) how a 4.xx/5.xx response maps to the exit code and output, and how `-i`/`-v` show the response code. Register the handler with one line in `ExtensionCatalogue.cs` and its description in `ExtensionDescriptionCatalogue.cs`, and keep `curl --ai-help extensions` right. Namespace `Curl.Extension.Coap.Client`.

## Acceptance criteria

- [ ] The handler is registered by one line in `ExtensionCatalogue.cs` and its description appears in `curl --extensions` and `curl --ai-help extensions`.
- [ ] Unit tests over a fake `IDatagramConnector` check GET, POST, PUT and DELETE against RFC 7252 sections 4.2, 5.2, 5.3, 5.8 and 6.4, including retransmission with a fake `TimeProvider`, a separate response after an empty ACK, and a Reset.
- [ ] With `CURL_EXTENSIONS=0` a `coap://` URL fails exactly as curl fails an unsupported scheme (exit 1, same stderr bytes); unit test.
- [ ] An ADR marked "Decided by Claude under Stewart's delegation" records the scheme names, RFC 7252 as the version followed, the response-code mapping, and what BL-4247, BL-4248 and BL-4252 add later.
- [ ] The changed libraries meet the four quality gates by `Measure-CodeQuality.ps1`.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: 8489097a 22060ce0 75389724 70db4a03 478f867b 3d836d5e.
