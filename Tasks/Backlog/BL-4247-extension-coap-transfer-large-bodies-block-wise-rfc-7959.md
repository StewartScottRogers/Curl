---
id: BL-4247
title: Extension CoAP: transfer large bodies block-wise (RFC 7959)
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-4246]
touches: [Curl.Extension.Coap.UnitLibrary/Blockwise, Curl.Extension.Coap.UnitTests/Blockwise, Curl.Extension.Coap.UnitLibrary/Client, Curl.Extension.Coap.UnitTests/Client]
requirement: none
created: 2026-10-29
completed:
---
# BL-4247 — Extension CoAP: transfer large bodies block-wise (RFC 7959)

## Goal

A `coap://` download larger than one datagram arrives whole through Block2, and an upload (`-d`, `-T`) larger than one block goes out through Block1, by RFC 7959.

## Context

Part of BL-4116 (extension 15 of 63, split for size). Extension rules: ADR-0999 and the lane rules "Extensions": it references only `Curl.Protocol.Abstractions`, the shared layers (`Curl.Tls`, `Curl.Cryptography`, `Curl.Networking`) and `Curl.Extension.Abstractions`, never a `Curl.Protocol.*` library or another extension, is tested against the RFCs, not curl, and never changes what curl does. Held to the four quality gates.

Builds on BL-4246's client and BL-4245's block option codec. RFC 7959 sections 2.3 (Block options in requests and responses), 2.4 (Block2), 2.5 (Block1), 2.9 (2.31 Continue, 4.08, 4.13), 3 (examples). Default block size 1024 (SZX 6), following a smaller size the server asks for. Namespace `Curl.Extension.Coap.Blockwise`.

## Acceptance criteria

- [ ] Unit tests over a fake `IDatagramConnector` replay the exchanges in RFC 7959 section 3's figures for Block2 (including the server choosing a smaller size) and Block1 (including 2.31 Continue and 4.13 Request Entity Too Large), each named after its figure.
- [ ] A body of several blocks is written to the output in order and only once.
- [ ] The changed library meets the four quality gates by `Measure-CodeQuality.ps1`.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-10-29: Created.
